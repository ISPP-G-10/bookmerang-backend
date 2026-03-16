using Bookmerang.Api.Data;
using Bookmerang.Api.Exceptions;
using Bookmerang.Api.Models.DTOs.Communities;
using Bookmerang.Api.Models.Entities;
using Bookmerang.Api.Models.Enums;
using Bookmerang.Api.Services.Interfaces.Communities;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace Bookmerang.Api.Services.Implementation.Communities;

public class MeetupService(AppDbContext db) : IMeetupService
{
    private readonly AppDbContext _db = db;

    public async Task<MeetupDto> CreateMeetupAsync(Guid creatorId, int communityId, CreateMeetupRequest request)
    {
        var community = await _db.Communities.FindAsync(communityId);
        if (community == null) throw new NotFoundException("Comunidad no encontrada.");

        var isMember = await _db.CommunityMembers.AnyAsync(cm => cm.CommunityId == communityId && cm.UserId == creatorId);
        if (!isMember) throw new ForbiddenException("Debes ser miembro de la comunidad para crear una quedada.");

        Point? otherLocation = null;
        if (request.OtherLocation != null && request.OtherLocation.Length >= 2)
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
            ScheduledAt = request.ScheduledAt,
            Status = MeetupStatus.SCHEDULED,
            CreatorId = creatorId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Meetups.Add(meetup);
        await _db.SaveChangesAsync();

        return MapToDto(meetup);
    }

    public async Task<List<MeetupDto>> GetMeetupsByCommunityAsync(int communityId)
    {
        var meetups = await _db.Meetups
            .Where(m => m.CommunityId == communityId && m.Status == MeetupStatus.SCHEDULED)
            .OrderBy(m => m.ScheduledAt)
            .ToListAsync();

        return meetups.Select(MapToDto).ToList();
    }

    public async Task<MeetupAttendanceDto> AttendMeetupAsync(Guid userId, int meetupId, AttendMeetupRequest request)
    {
        var meetup = await _db.Meetups.FindAsync(meetupId);
        if (meetup == null) throw new NotFoundException("Quedada no encontrada.");

        if (meetup.Status != MeetupStatus.SCHEDULED)
            throw new ValidationException("No te puedes unir a una quedada que ya no está programada.");

        var isMember = await _db.CommunityMembers.AnyAsync(cm => cm.CommunityId == meetup.CommunityId && cm.UserId == userId);
        if (!isMember) throw new ForbiddenException("Debes ser miembro de la comunidad para asistir a la quedada.");

        var book = await _db.Books.FirstOrDefaultAsync(b => b.Id == request.SelectedBookId && b.OwnerId == userId);
        if (book == null) throw new NotFoundException("El libro seleccionado no se encuentra en tu biblioteca.");
        
        if (book.Status != BookStatus.PUBLISHED)
            throw new ValidationException("El libro seleccionado debe estar publicado para poder llevarlo a un intercambio.");

        var existingAttendance = await _db.MeetupAttendances.FirstOrDefaultAsync(ma => ma.MeetupId == meetupId && ma.UserId == userId);
        if (existingAttendance != null)
        {
            if (existingAttendance.Status == MeetupAttendanceStatus.CANCELLED)
            {
                existingAttendance.Status = MeetupAttendanceStatus.REGISTERED;
                existingAttendance.SelectedBookId = request.SelectedBookId;
                _db.MeetupAttendances.Update(existingAttendance);
            }
            else
            {
                throw new ValidationException("Ya estás apuntado a esta quedada.");
            }
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
        var attendance = await _db.MeetupAttendances.FirstOrDefaultAsync(ma => ma.MeetupId == meetupId && ma.UserId == userId);
        if (attendance == null) throw new NotFoundException("No estás apuntado a esta quedada.");

        attendance.Status = MeetupAttendanceStatus.CANCELLED;
        _db.MeetupAttendances.Update(attendance);
        await _db.SaveChangesAsync();
    }

    private static MeetupDto MapToDto(Meetup meetup)
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
            UpdatedAt = meetup.UpdatedAt
        };
    }
}