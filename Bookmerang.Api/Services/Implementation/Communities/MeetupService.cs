using Bookmerang.Api.Data;
using Bookmerang.Api.Exceptions;
using Bookmerang.Api.Models.DTOs.Communities;
using Bookmerang.Api.Models.Entities;
using Bookmerang.Api.Models.Enums;
using Bookmerang.Api.Services.Interfaces.Communities;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using AppValidationException = Bookmerang.Api.Exceptions.ValidationException;

namespace Bookmerang.Api.Services.Implementation.Communities;

public class MeetupService(AppDbContext db, IValidator<CreateMeetupRequest> createMeetupRequestValidator) : IMeetupService
{
    private readonly AppDbContext _db = db;
    private readonly IValidator<CreateMeetupRequest> _createMeetupRequestValidator = createMeetupRequestValidator;
    private const int MaxMeetupAssistants = 10; //Límite de asistentes a una quedada de comunidad

    public async Task<MeetupDto> CreateMeetupAsync(Guid creatorId, int communityId, CreateMeetupRequest request)
    {
        var scheduledAtUtc = DateTime.SpecifyKind(request.ScheduledAt, DateTimeKind.Utc);

        var community = await _db.Communities.FindAsync(communityId);
        if (community == null) throw new NotFoundException("Comunidad no encontrada.");
        if (community.Status != CommunityStatus.ACTIVE)
            throw new ForbiddenException("La comunidad debe estar activa para crear quedadas.");

        var membership = await _db.CommunityMembers
            .FirstOrDefaultAsync(cm => cm.CommunityId == communityId && cm.UserId == creatorId);
        if (membership == null)
            throw new ForbiddenException("Debes ser miembro de la comunidad para crear una quedada.");

        var isCreator = community.CreatorId == creatorId;
        var isModerator = membership.Role == CommunityRole.MODERATOR;
        if (!isCreator && !isModerator)
            throw new ForbiddenException("Solo el creador o los moderadores pueden crear quedadas.");

        var validationResult = await _createMeetupRequestValidator.ValidateAsync(request);
        if (!validationResult.IsValid)
            throw new AppValidationException(string.Join(" ", validationResult.Errors.Select(e => e.ErrorMessage)));

        if (await HasSchedulingConflictAsync(creatorId, scheduledAtUtc))
            throw new AppValidationException("No puedes crear esta quedada porque ya estás apuntado a otra en esa fecha y hora.");

        Point? otherLocation = null;
        if (request.OtherLocation != null)
        {
            var factory = NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
            otherLocation = factory.CreatePoint(new Coordinate(request.OtherLocation[0], request.OtherLocation[1]));
        }

        var meetup = new Meetup
        {
            CommunityId = communityId,
            Title = request.Title,
            Description = request.Description,
            OtherBookSpotId = request.OtherBookSpotId,
            OtherLocation = otherLocation,
            ScheduledAt = scheduledAtUtc,
            Status = MeetupStatus.SCHEDULED,
            CreatorId = creatorId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Meetups.Add(meetup);
        await _db.SaveChangesAsync();

        return MapToDto(meetup);
    }

    public async Task<MeetupDto> UpdateMeetupAsync(Guid userId, int communityId, int meetupId, CreateMeetupRequest request)
    {
        var community = await _db.Communities.FindAsync(communityId);
        if (community == null) throw new NotFoundException("Comunidad no encontrada.");

        var membership = await _db.CommunityMembers
            .FirstOrDefaultAsync(cm => cm.CommunityId == communityId && cm.UserId == userId);
        if (membership == null)
            throw new ForbiddenException("Debes ser miembro de la comunidad para modificar una quedada.");

        var isCreator = community.CreatorId == userId;
        var isModerator = membership.Role == CommunityRole.MODERATOR;
        if (!isCreator && !isModerator)
            throw new ForbiddenException("Solo el creador o los moderadores pueden modificar quedadas.");

        var meetup = await _db.Meetups
            .Include(m => m.Attendances)
                .ThenInclude(a => a.User)
                .ThenInclude(u => u.BaseUser)
            .Include(m => m.Attendances)
                .ThenInclude(a => a.SelectedBook)
            .FirstOrDefaultAsync(m => m.Id == meetupId && m.CommunityId == communityId);

        if (meetup == null)
            throw new NotFoundException("Quedada no encontrada.");

        if (HasMeetupStarted(meetup))
            throw new AppValidationException("No se puede modificar una quedada que ya ha empezado.");

        var validationResult = await _createMeetupRequestValidator.ValidateAsync(request);
        if (!validationResult.IsValid)
            throw new AppValidationException(string.Join(" ", validationResult.Errors.Select(e => e.ErrorMessage)));

        var scheduledAtUtc = DateTime.SpecifyKind(request.ScheduledAt, DateTimeKind.Utc);
        if (await HasSchedulingConflictAsync(userId, scheduledAtUtc, meetupId))
            throw new AppValidationException("No puedes programar esta quedada porque ya estás apuntado a otra en esa fecha y hora.");

        Point? otherLocation = null;
        if (request.OtherLocation != null)
        {
            var factory = NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
            otherLocation = factory.CreatePoint(new Coordinate(request.OtherLocation[0], request.OtherLocation[1]));
        }

        meetup.Title = request.Title;
        meetup.Description = request.Description;
        meetup.OtherBookSpotId = request.OtherBookSpotId;
        meetup.OtherLocation = otherLocation;
        meetup.ScheduledAt = scheduledAtUtc;
        meetup.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return MapToDto(meetup);
    }

    public async Task DeleteMeetupAsync(Guid userId, int communityId, int meetupId)
    {
        var community = await _db.Communities.FindAsync(communityId);
        if (community == null) throw new NotFoundException("Comunidad no encontrada.");

        var membership = await _db.CommunityMembers
            .FirstOrDefaultAsync(cm => cm.CommunityId == communityId && cm.UserId == userId);
        if (membership == null)
            throw new ForbiddenException("Debes ser miembro de la comunidad para eliminar una quedada.");

        var isCreator = community.CreatorId == userId;
        var isModerator = membership.Role == CommunityRole.MODERATOR;
        if (!isCreator && !isModerator)
            throw new ForbiddenException("Solo el creador o los moderadores pueden eliminar quedadas.");

        var meetup = await _db.Meetups
            .Include(m => m.Attendances)
            .FirstOrDefaultAsync(m => m.Id == meetupId && m.CommunityId == communityId);

        if (meetup == null)
            throw new NotFoundException("Quedada no encontrada.");

        if (HasMeetupStarted(meetup))
            throw new AppValidationException("No se puede eliminar una quedada que ya ha empezado.");

        if (meetup.Attendances.Count > 0)
        {
            _db.MeetupAttendances.RemoveRange(meetup.Attendances);
        }

        _db.Meetups.Remove(meetup);
        await _db.SaveChangesAsync();
    }

    public async Task<List<MeetupDto>> GetMeetupsByCommunityAsync(int communityId)
    {
        var community = await _db.Communities.FindAsync(communityId);
        if (community == null) throw new NotFoundException("Comunidad no encontrada.");
        if (community.Status != CommunityStatus.ACTIVE)
            throw new ForbiddenException("La comunidad debe estar activa para ver quedadas.");

        var meetups = await _db.Meetups
            .Where(m => m.CommunityId == communityId && m.Status == MeetupStatus.SCHEDULED)
            .Include(m => m.Attendances)
                .ThenInclude(a => a.User)
                .ThenInclude(u => u.BaseUser)
            .Include(m => m.Attendances)
                .ThenInclude(a => a.SelectedBook)
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync();

        var allAttendeeIds = meetups
            .SelectMany(m => m.Attendances.Select(a => a.UserId))
            .Distinct().ToList();

        var personalizationMap = allAttendeeIds.Count > 0
            ? await _db.UserProgresses
                .Where(p => allAttendeeIds.Contains(p.UserId))
                .ToDictionaryAsync(p => p.UserId, p => (p.ActiveFrameId, p.ActiveColorId))
            : new Dictionary<Guid, (string? ActiveFrameId, string? ActiveColorId)>();

        return meetups.Select(m => MapToDto(m, personalizationMap)).ToList();
    }

    public async Task<MeetupAttendanceDto> AttendMeetupAsync(Guid userId, int meetupId, AttendMeetupRequest request)
    {
        var meetup = await _db.Meetups.FindAsync(meetupId);
        if (meetup == null) throw new NotFoundException("Quedada no encontrada.");

        var community = await _db.Communities.FindAsync(meetup.CommunityId);
        if (community == null) throw new NotFoundException("Comunidad no encontrada.");
        if (community.Status != CommunityStatus.ACTIVE)
            throw new ForbiddenException("La comunidad debe estar activa para asistir a quedadas.");

        if (meetup.Status != MeetupStatus.SCHEDULED)
            throw new AppValidationException("No te puedes unir a una quedada que ya no está programada.");

        var meetupScheduledAtUtc = DateTime.SpecifyKind(meetup.ScheduledAt, DateTimeKind.Utc);
        if (await HasSchedulingConflictAsync(userId, meetupScheduledAtUtc, meetupId))
            throw new AppValidationException("Ya estás apuntado a otra quedada en esa fecha y hora.");

        var isMember = await _db.CommunityMembers.AnyAsync(cm => cm.CommunityId == meetup.CommunityId && cm.UserId == userId);
        if (!isMember) throw new ForbiddenException("Debes ser miembro de la comunidad para asistir a la quedada.");

        var book = await _db.Books.FirstOrDefaultAsync(b => b.Id == request.SelectedBookId && b.OwnerId == userId);
        if (book == null) throw new NotFoundException("El libro seleccionado no se encuentra en tu biblioteca.");
        
        if (book.Status != BookStatus.PUBLISHED)
            throw new AppValidationException("El libro seleccionado debe estar publicado para poder llevarlo a un intercambio.");

        var existingAttendance = await _db.MeetupAttendances.FirstOrDefaultAsync(ma => ma.MeetupId == meetupId && ma.UserId == userId);
        if (existingAttendance != null && existingAttendance.Status != MeetupAttendanceStatus.CANCELLED)
            throw new AppValidationException("Ya estás apuntado a esta quedada.");

        var activeAssistantsCount = await _db.MeetupAttendances.CountAsync(ma =>
            ma.MeetupId == meetupId
            && (ma.Status == MeetupAttendanceStatus.REGISTERED || ma.Status == MeetupAttendanceStatus.ATTENDED));

        if (activeAssistantsCount >= MaxMeetupAssistants)
            throw new AppValidationException("La quedada ya está completa.");

        if (existingAttendance != null)
        {
            existingAttendance.Status = MeetupAttendanceStatus.REGISTERED;
            existingAttendance.SelectedBookId = request.SelectedBookId;
            _db.MeetupAttendances.Update(existingAttendance);
        }
        else
        {
            var attendance = new MeetupAttendance
            {
                MeetupId = meetupId,
                UserId = userId,
                SelectedBookId = request.SelectedBookId,
                Status = MeetupAttendanceStatus.REGISTERED
            };
            _db.MeetupAttendances.Add(attendance);
        }

        await _db.SaveChangesAsync();

        var user = await _db.RegularUsers.Include(u => u.BaseUser).FirstOrDefaultAsync(u => u.Id == userId);

        return new MeetupAttendanceDto
        {
            MeetupId = meetupId,
            UserId = userId,
            Username = user?.BaseUser?.Username ?? "",
            ProfilePhoto = user?.BaseUser?.ProfilePhoto ?? "",
            SelectedBookId = book.Id,
            SelectedBookTitle = book.Titulo ?? "",
            Status = MeetupAttendanceStatus.REGISTERED
        };
    }

    public async Task CancelAttendanceAsync(Guid userId, int meetupId)
    {
        var meetup = await _db.Meetups.FindAsync(meetupId);
        if (meetup == null) throw new NotFoundException("Quedada no encontrada.");

        var community = await _db.Communities.FindAsync(meetup.CommunityId);
        if (community == null) throw new NotFoundException("Comunidad no encontrada.");
        if (community.Status != CommunityStatus.ACTIVE)
            throw new ForbiddenException("La comunidad debe estar activa para modificar asistencias.");

        var isMember = await _db.CommunityMembers.AnyAsync(cm => cm.CommunityId == meetup.CommunityId && cm.UserId == userId);
        if (!isMember) throw new ForbiddenException("Debes ser miembro de la comunidad para asistir a la quedada.");

        var attendance = await _db.MeetupAttendances.FirstOrDefaultAsync(ma => ma.MeetupId == meetupId && ma.UserId == userId);
        if (attendance == null) throw new NotFoundException("No estás apuntado a esta quedada.");

        attendance.Status = MeetupAttendanceStatus.CANCELLED;
        _db.MeetupAttendances.Update(attendance);
        await _db.SaveChangesAsync();
    }

    private async Task<bool> HasSchedulingConflictAsync(Guid userId, DateTime scheduledAt, int? excludedMeetupId = null)
    {
        var normalizedTarget = EnsureUtc(scheduledAt);
        var date = normalizedTarget.Date;
        var hour = scheduledAt.Hour;
        var minute = scheduledAt.Minute;

        return await _db.MeetupAttendances.AnyAsync(ma =>
            ma.UserId == userId
            && (ma.Status == MeetupAttendanceStatus.REGISTERED || ma.Status == MeetupAttendanceStatus.ATTENDED)
            && ma.Meetup.Status == MeetupStatus.SCHEDULED
            && (!excludedMeetupId.HasValue || ma.MeetupId != excludedMeetupId.Value)
            && ma.Meetup.ScheduledAt.Date == date
            && ma.Meetup.ScheduledAt.Hour == hour
            && ma.Meetup.ScheduledAt.Minute == minute);
    }

    private static bool HasMeetupStarted(Meetup meetup)
    {
        var scheduledAtUtc = EnsureUtc(meetup.ScheduledAt);
        return scheduledAtUtc <= DateTime.UtcNow;
    }

    private static DateTime EnsureUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };
    }

    private static MeetupDto MapToDto(Meetup meetup, Dictionary<Guid, (string? ActiveFrameId, string? ActiveColorId)>? personalization = null)
    {
        return new MeetupDto
        {
            Id = meetup.Id,
            CommunityId = meetup.CommunityId,
            Title = meetup.Title,
            Description = meetup.Description,
            OtherBookSpotId = meetup.OtherBookSpotId,
            OtherLocation = meetup.OtherLocation != null ? new[] { meetup.OtherLocation.X, meetup.OtherLocation.Y } : null,
            ScheduledAt = meetup.ScheduledAt,
            Status = meetup.Status,
            CreatorId = meetup.CreatorId,
            CreatedAt = meetup.CreatedAt,
            UpdatedAt = meetup.UpdatedAt,
            Attendees = meetup.Attendances
                .Where(a => a.Status == MeetupAttendanceStatus.REGISTERED || a.Status == MeetupAttendanceStatus.ATTENDED)
                .Select(a => {
                    var pers = personalization?.GetValueOrDefault(a.UserId) ?? default;
                    return new MeetupAttendeeDto
                    {
                        UserId = a.UserId,
                        Username = a.User?.BaseUser?.Username ?? string.Empty,
                        SelectedBookId = a.SelectedBookId,
                        SelectedBookTitle = a.SelectedBook?.Titulo ?? string.Empty,
                        ActiveFrameId = pers.ActiveFrameId,
                        ActiveColorId = pers.ActiveColorId
                    };
                })
                .ToList()
        };
    }
}