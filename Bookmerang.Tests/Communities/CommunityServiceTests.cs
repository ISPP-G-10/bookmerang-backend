using Bookmerang.Api.Data;
using Bookmerang.Api.Exceptions;
using Bookmerang.Api.Models.DTOs;
using Bookmerang.Api.Models.DTOs.Communities;
using Bookmerang.Api.Models.Entities;
using Bookmerang.Api.Models.Enums;
using Bookmerang.Api.Services.Implementation.Communities;
using Bookmerang.Api.Services.Interfaces.Chats;
using Bookmerang.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Moq;
using NetTopologySuite.Geometries;
using Xunit;

namespace Bookmerang.Tests.Communities;

public class CommunityServiceTests : IAsyncLifetime
{
    private AppDbContext _db = null!;
    private Mock<IChatService> _chatServiceMock = null!;
    private CommunityService _service = null!;

    public Task InitializeAsync()
    {
        _db = DbContextFactory.CreateInMemory();
        _chatServiceMock = new Mock<IChatService>();
        _service = new CommunityService(_db, _chatServiceMock.Object);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _db.Database.EnsureDeletedAsync();
        await _db.DisposeAsync();
    }

    private static Point MakePoint(double lon, double lat) =>
        new GeometryFactory(new PrecisionModel(), 4326).CreatePoint(new Coordinate(lon, lat));

    private User SeedUser(Guid id, string email, PricingPlan plan = PricingPlan.FREE)
    {
        var baseUser = new BaseUser
        {
            Id = id,
            SupabaseId = $"sup-{id}",
            Email = email,
            Username = email.Split('@')[0],
            Name = email.Split('@')[0],
            ProfilePhoto = string.Empty,
            UserType = BaseUserType.USER,
            Location = MakePoint(0, 0),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Users.Add(baseUser);

        var user = new User
        {
            Id = id,
            Plan = plan
        };
        _db.RegularUsers.Add(user);
        return user;
    }

    private Bookspot SeedBookspot(int id, Point location)
    {
        var bs = new Bookspot
        {
            Id = id,
            Nombre = $"BookSpot {id}",
            Location = location,
            IsBookdrop = false,
            Status = BookspotStatus.ACTIVE,
            CreatedAt = DateTime.UtcNow
        };
        _db.Bookspots.Add(bs);
        return bs;
    }

    [Fact]
    public async Task JoinCommunity_FreeUser_MaxOneActiveCommunity_ThrowsForbidden()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SeedUser(userId, "free@test.com", PricingPlan.FREE);
        
        var bs = SeedBookspot(1, MakePoint(-5.98, 37.38));
        
        var comm1 = new Community { Name = "Comm1", ReferenceBookspotId = bs.Id, Status = CommunityStatus.ACTIVE };
        var comm2 = new Community { Name = "Comm2", ReferenceBookspotId = bs.Id, Status = CommunityStatus.ACTIVE };
        _db.Communities.AddRange(comm1, comm2);
        await _db.SaveChangesAsync();

        _db.CommunityMembers.Add(new CommunityMember { CommunityId = comm1.Id, UserId = userId, Role = CommunityRole.MEMBER });
        await _db.SaveChangesAsync();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ForbiddenException>(() => _service.JoinCommunityAsync(userId, comm2.Id));
        Assert.Contains("solo pueden pertenecer a una comunidad no archivada", ex.Message);
    }

    [Fact]
    public async Task JoinCommunity_PremiumUser_MultipleCommunities_Succeeds()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SeedUser(userId, "premium@test.com", PricingPlan.PREMIUM);
        
        var bs = SeedBookspot(1, MakePoint(-5.98, 37.38));
        
        var comm1 = new Community { Name = "Comm1", ReferenceBookspotId = bs.Id, Status = CommunityStatus.ACTIVE };
        var comm2 = new Community { Name = "Comm2", ReferenceBookspotId = bs.Id, Status = CommunityStatus.ACTIVE };
        _db.Communities.AddRange(comm1, comm2);
        await _db.SaveChangesAsync();

        _db.CommunityMembers.Add(new CommunityMember { CommunityId = comm1.Id, UserId = userId, Role = CommunityRole.MEMBER });
        await _db.SaveChangesAsync();

        // Act
        var result = await _service.JoinCommunityAsync(userId, comm2.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(comm2.Id, result.Id);
        
        var memberships = await _db.CommunityMembers.Where(cm => cm.UserId == userId).CountAsync();
        Assert.Equal(2, memberships);
    }

    [Fact]
    public async Task JoinCommunity_CommunityIsFull_ThrowsValidationException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SeedUser(userId, "newuser@test.com", PricingPlan.PREMIUM);
        
        var bs = SeedBookspot(1, MakePoint(-5.98, 37.38));
        var comm = new Community { Name = "FullComm", ReferenceBookspotId = bs.Id, Status = CommunityStatus.ACTIVE };
        _db.Communities.Add(comm);
        await _db.SaveChangesAsync();

        for (int i = 0; i < 10; i++)
        {
            var dummyId = Guid.NewGuid();
            SeedUser(dummyId, $"dummy{i}@test.com");
            _db.CommunityMembers.Add(new CommunityMember { CommunityId = comm.Id, UserId = dummyId, Role = CommunityRole.MEMBER });
        }
        await _db.SaveChangesAsync();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => _service.JoinCommunityAsync(userId, comm.Id));
        Assert.Contains("comunidad está llena", ex.Message);
    }

    [Fact]
    public async Task CreateCommunity_FreeUser_MaxOneActiveCommunity_ThrowsForbidden()
    {
        var userId = Guid.NewGuid();
        SeedUser(userId, "free2@test.com", PricingPlan.FREE);
        var bs = SeedBookspot(1, MakePoint(0, 0));
        var comm = new Community { Name = "Existing", ReferenceBookspotId = bs.Id, Status = CommunityStatus.ACTIVE };
        _db.Communities.Add(comm);
        _db.CommunityMembers.Add(new CommunityMember { CommunityId = comm.Id, UserId = userId, Role = CommunityRole.MEMBER });
        await _db.SaveChangesAsync();

        var request = new CreateCommunityRequest { Name = "New Community", ReferenceBookspotId = bs.Id };

        var ex = await Assert.ThrowsAsync<ForbiddenException>(() => _service.CreateCommunityAsync(userId, request));
        Assert.Contains("solo pueden pertenecer a una comunidad no archivada", ex.Message);
    }

    [Fact]
    public async Task CreateCommunity_ValidRequest_CreatesCommunityAndModerator()
    {
        var userId = Guid.NewGuid();
        SeedUser(userId, "creator@test.com", PricingPlan.FREE); // First community is allowed
        var bs = SeedBookspot(1, MakePoint(0, 0));
        await _db.SaveChangesAsync();

        var request = new CreateCommunityRequest { Name = "My New Comm", ReferenceBookspotId = bs.Id };

        _chatServiceMock.Setup(c => c.CreateChat(ChatType.COMMUNITY, It.IsAny<List<Guid>>()))
            .ReturnsAsync(new ChatDto(1, "COMMUNITY", DateTime.UtcNow, new List<ChatParticipantDto>(), null));

        var result = await _service.CreateCommunityAsync(userId, request);

        Assert.NotNull(result);
        Assert.Equal("My New Comm", result.Name);
        Assert.Equal(CommunityStatus.CREATED, result.Status); // Should be CREATED until 3 members
        
        var member = await _db.CommunityMembers.FirstOrDefaultAsync(m => m.CommunityId == result.Id && m.UserId == userId);
        Assert.NotNull(member);
        Assert.Equal(CommunityRole.MODERATOR, member.Role);
    }

    [Fact]
    public async Task LeaveCommunity_RemovesMemberAndDependencies()
    {
        var userId = Guid.NewGuid();
        SeedUser(userId, "leaver@test.com", PricingPlan.FREE);
        var bs = SeedBookspot(1, MakePoint(0, 0));
        var comm = new Community { Name = "CommToLeave", ReferenceBookspotId = bs.Id, Status = CommunityStatus.ACTIVE };
        _db.Communities.Add(comm);
        
        var chat = new Chat { Type = ChatType.COMMUNITY };
        _db.Chats.Add(chat);
        await _db.SaveChangesAsync();

        _db.CommunityChats.Add(new CommunityChat { CommunityId = comm.Id, ChatId = chat.Id });
        _db.CommunityMembers.Add(new CommunityMember { CommunityId = comm.Id, UserId = userId, Role = CommunityRole.MEMBER });
        _db.ChatParticipants.Add(new ChatParticipant { ChatId = chat.Id, UserId = userId });
        
        var book = new Book { OwnerId = userId, Status = BookStatus.PUBLISHED };
        _db.Books.Add(book);
        await _db.SaveChangesAsync();

        _db.CommunityLibraryLikes.Add(new CommunityLibraryLike { CommunityId = comm.Id, UserId = userId, BookId = book.Id });
        
        var meetup = new Meetup { CommunityId = comm.Id, Title = "Meetup", ScheduledAt = DateTime.UtcNow.AddDays(1), Status = MeetupStatus.SCHEDULED };
        _db.Meetups.Add(meetup);
        await _db.SaveChangesAsync();

        _db.MeetupAttendances.Add(new MeetupAttendance { MeetupId = meetup.Id, UserId = userId, SelectedBookId = book.Id, Status = MeetupAttendanceStatus.REGISTERED });
        await _db.SaveChangesAsync();

        // Act
        await _service.LeaveCommunityAsync(userId, comm.Id);

        // Assert
        Assert.Empty(await _db.CommunityMembers.Where(m => m.UserId == userId && m.CommunityId == comm.Id).ToListAsync());
        Assert.Empty(await _db.ChatParticipants.Where(cp => cp.UserId == userId && cp.ChatId == chat.Id).ToListAsync());
        Assert.Empty(await _db.CommunityLibraryLikes.Where(l => l.UserId == userId && l.CommunityId == comm.Id).ToListAsync());
        Assert.Empty(await _db.MeetupAttendances.Where(ma => ma.UserId == userId && ma.MeetupId == meetup.Id).ToListAsync());
    }
}