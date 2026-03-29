using Bookmerang.Api.Data;
using Bookmerang.Api.Exceptions;
using Bookmerang.Api.Models.DTOs.Communities;
using Bookmerang.Api.Models.Entities;
using Bookmerang.Api.Models.Enums;
using Bookmerang.Api.Services.Implementation.Communities;
using Bookmerang.Api.Validators.Communities;
using Bookmerang.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Xunit;

namespace Bookmerang.Tests.Communities;

public class MeetupServiceTests : IAsyncLifetime
{
    private AppDbContext _db = null!;
    private MeetupService _service = null!;

    public Task InitializeAsync()
    {
        _db = DbContextFactory.CreateInMemory();
        _service = new MeetupService(_db, new CreateMeetupRequestValidator());
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _db.Database.EnsureDeletedAsync();
        await _db.DisposeAsync();
    }

    private static Point MakePoint(double lon, double lat) =>
        new GeometryFactory(new PrecisionModel(), 4326).CreatePoint(new Coordinate(lon, lat));

    private User SeedUser(Guid id)
    {
        var baseUser = new BaseUser { Id = id, SupabaseId = $"sup-{id}", Email = "test@test.com", Username = "test", Name = "test", Location = MakePoint(0,0) };
        _db.Users.Add(baseUser);
        var user = new User { Id = id };
        _db.RegularUsers.Add(user);
        return user;
    }

    private Book SeedBook(int id, Guid ownerId, BookStatus status)
    {
        var book = new Book { Id = id, OwnerId = ownerId, Status = status, Titulo = $"Book {id}" };
        _db.Books.Add(book);
        return book;
    }

    private Community SeedCommunity(int id, Guid memberId)
    {
        var bs = new Bookspot { Id = 1, Nombre = "BS", Location = MakePoint(0,0), Status = BookspotStatus.ACTIVE };
        _db.Bookspots.Add(bs);
        var comm = new Community { Id = id, Name = "Comm", ReferenceBookspotId = bs.Id, Status = CommunityStatus.ACTIVE, CreatorId = memberId };
        _db.Communities.Add(comm);
        _db.CommunityMembers.Add(new CommunityMember { CommunityId = comm.Id, UserId = memberId, Role = CommunityRole.MODERATOR });
        return comm;
    }

    [Fact]
    public async Task CreateMeetup_NotAMember_ThrowsForbidden()
    {
        var userId = Guid.NewGuid();
        SeedUser(userId);
        var commId = 1;
        // Community exists but user is not member
        var bs = new Bookspot { Id = 1, Nombre = "BS", Location = MakePoint(0,0), Status = BookspotStatus.ACTIVE };
        _db.Bookspots.Add(bs);
        var comm = new Community { Id = commId, Name = "Comm", ReferenceBookspotId = bs.Id, Status = CommunityStatus.ACTIVE };
        _db.Communities.Add(comm);
        await _db.SaveChangesAsync();

        var request = new CreateMeetupRequest { Title = "Test Meetup", ScheduledAt = DateTime.UtcNow.AddDays(2) };

        await Assert.ThrowsAsync<ForbiddenException>(() => _service.CreateMeetupAsync(userId, commId, request));
    }

    [Fact]
    public async Task CreateMeetup_EmptyTitle_ThrowsValidationException()
    {
        var userId = Guid.NewGuid();
        SeedUser(userId);
        var comm = SeedCommunity(1, userId);
        await _db.SaveChangesAsync();

        var request = new CreateMeetupRequest
        {
            Title = "",
            ScheduledAt = DateTime.UtcNow.AddDays(2)
        };

        var ex = await Assert.ThrowsAsync<ValidationException>(() => _service.CreateMeetupAsync(userId, comm.Id, request));
        Assert.Contains("título", ex.Message);
    }

    [Fact]
    public async Task CreateMeetup_DefaultDate_ThrowsValidationException()
    {
        var userId = Guid.NewGuid();
        SeedUser(userId);
        var comm = SeedCommunity(2, userId);
        await _db.SaveChangesAsync();

        var request = new CreateMeetupRequest
        {
            Title = "Quedada válida",
            ScheduledAt = default
        };

        var ex = await Assert.ThrowsAsync<ValidationException>(() => _service.CreateMeetupAsync(userId, comm.Id, request));
        Assert.Contains("fecha y hora", ex.Message);
    }

    [Fact]
    public async Task AttendMeetup_BookNotPublished_ThrowsValidationException()
    {
        var userId = Guid.NewGuid();
        SeedUser(userId);
        var comm = SeedCommunity(1, userId);
        var book = SeedBook(100, userId, BookStatus.DRAFT);
        var meetup = new Meetup { Id = 10, CommunityId = comm.Id, Title = "Meetup", ScheduledAt = DateTime.UtcNow.AddDays(2), Status = MeetupStatus.SCHEDULED };
        _db.Meetups.Add(meetup);
        await _db.SaveChangesAsync();

        var request = new AttendMeetupRequest { SelectedBookId = book.Id };

        var ex = await Assert.ThrowsAsync<ValidationException>(() => _service.AttendMeetupAsync(userId, meetup.Id, request));
        Assert.Contains("debe estar publicado", ex.Message);
    }

    [Fact]
    public async Task AttendMeetup_Valid_CreatesAttendance()
    {
        var userId = Guid.NewGuid();
        SeedUser(userId);
        var comm = SeedCommunity(1, userId);
        var book = SeedBook(100, userId, BookStatus.PUBLISHED);
        var meetup = new Meetup { Id = 10, CommunityId = comm.Id, Title = "Meetup", ScheduledAt = DateTime.UtcNow.AddDays(2), Status = MeetupStatus.SCHEDULED };
        _db.Meetups.Add(meetup);
        await _db.SaveChangesAsync();

        var request = new AttendMeetupRequest { SelectedBookId = book.Id };

        var result = await _service.AttendMeetupAsync(userId, meetup.Id, request);

        Assert.Equal(MeetupAttendanceStatus.REGISTERED, result.Status);
        Assert.Equal(book.Id, result.SelectedBookId);
        
        var attendance = await _db.MeetupAttendances.FirstOrDefaultAsync(ma => ma.MeetupId == meetup.Id && ma.UserId == userId);
        Assert.NotNull(attendance);
    }

    [Fact]
    public async Task CancelAttendance_UserNotCommunityMember_ThrowsForbiddenException()
    {
        var userId = Guid.NewGuid();
        SeedUser(userId);

        var creatorId = Guid.NewGuid();
        SeedUser(creatorId);

        var comm = SeedCommunity(3, creatorId);
        var meetup = new Meetup
        {
            Id = 30,
            CommunityId = comm.Id,
            Title = "Meetup",
            ScheduledAt = DateTime.UtcNow.AddDays(2),
            Status = MeetupStatus.SCHEDULED
        };
        _db.Meetups.Add(meetup);
        await _db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<ForbiddenException>(() => _service.CancelAttendanceAsync(userId, meetup.Id));
        Assert.Contains("Debes ser miembro de la comunidad", ex.Message);
    }
}