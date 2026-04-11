using Bookmerang.Api.Data;
using Bookmerang.Api.Exceptions;
using Bookmerang.Api.Models.DTOs;
using Bookmerang.Api.Models.DTOs.Communities;
using Bookmerang.Api.Models.Entities;
using Bookmerang.Api.Models.Enums;
using Bookmerang.Api.Services.Implementation.Communities;
using Bookmerang.Api.Validators.Communities;
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
        _service = new CommunityService(_db, _chatServiceMock.Object, new CreateCommunityRequestValidator());
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
    public async Task JoinCommunity_FromOneMemberToTwo_KeepsCreatedAndReturnsMemberCountTwo()
    {
        // Arrange
        var creatorId = Guid.NewGuid();
        var joinerId = Guid.NewGuid();
        SeedUser(creatorId, "creator2@test.com", PricingPlan.PREMIUM);
        SeedUser(joinerId, "joiner@test.com", PricingPlan.PREMIUM);

        var bs = SeedBookspot(1, MakePoint(-5.98, 37.38));
        var comm = new Community
        {
            Name = "CommCreated",
            ReferenceBookspotId = bs.Id,
            Status = CommunityStatus.CREATED,
            CreatorId = creatorId,
            CreatedAt = DateTime.UtcNow
        };
        _db.Communities.Add(comm);
        await _db.SaveChangesAsync();

        _db.CommunityMembers.Add(new CommunityMember
        {
            CommunityId = comm.Id,
            UserId = creatorId,
            Role = CommunityRole.MODERATOR,
            JoinedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        // Act
        var result = await _service.JoinCommunityAsync(joinerId, comm.Id);

        // Assert
        Assert.Equal(2, result.MemberCount);
        Assert.Equal(CommunityStatus.CREATED, result.Status);
    }

    [Fact]
    public async Task CreateCommunity_DuplicateNameNearby_ThrowsValidationException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SeedUser(userId, "test@test.com", PricingPlan.PREMIUM);

        var bs1 = SeedBookspot(10, MakePoint(-5.98, 37.38));
        var bs2 = SeedBookspot(11, MakePoint(-5.981, 37.381)); // Very close to bs1

        var existingComm = new Community 
        { 
            Name = "Duplicate Name", 
            ReferenceBookspotId = bs1.Id, 
            Status = CommunityStatus.ACTIVE 
        };
        _db.Communities.Add(existingComm);
        await _db.SaveChangesAsync();

        var request = new CreateCommunityRequest 
        { 
            Name = "DUPLICATE NAME", 
            ReferenceBookspotId = bs2.Id 
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => _service.CreateCommunityAsync(userId, request));
        Assert.Contains("Ya existe una comunidad con ese nombre en esta zona", ex.Message);
    }

    [Fact]
    public async Task CreateCommunity_EmptyName_ThrowsValidationException()
    {
        var userId = Guid.NewGuid();
        SeedUser(userId, "empty-name@test.com", PricingPlan.PREMIUM);
        var bs = SeedBookspot(30, MakePoint(0, 0));
        await _db.SaveChangesAsync();

        var request = new CreateCommunityRequest { Name = "", ReferenceBookspotId = bs.Id };

        var ex = await Assert.ThrowsAsync<ValidationException>(() => _service.CreateCommunityAsync(userId, request));
        Assert.Contains("nombre de la comunidad es obligatorio", ex.Message);
    }

    [Fact]
    public async Task CreateCommunity_NameTooShort_ThrowsValidationException()
    {
        var userId = Guid.NewGuid();
        SeedUser(userId, "short-name@test.com", PricingPlan.PREMIUM);
        var bs = SeedBookspot(31, MakePoint(0, 0));
        await _db.SaveChangesAsync();

        var request = new CreateCommunityRequest { Name = "ab", ReferenceBookspotId = bs.Id };

        var ex = await Assert.ThrowsAsync<ValidationException>(() => _service.CreateCommunityAsync(userId, request));
        Assert.Contains("al menos 3", ex.Message);
    }

    [Fact]
    public async Task CreateCommunity_DuplicateNameFarAway_Succeeds()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SeedUser(userId, "test2@test.com", PricingPlan.PREMIUM);

        var bs1 = SeedBookspot(20, MakePoint(-5.98, 37.38)); // Sevilla
        var bs2 = SeedBookspot(21, MakePoint(-3.70, 40.41)); // Madrid (Far away)

        var existingComm = new Community 
        { 
            Name = "Common Name", 
            ReferenceBookspotId = bs1.Id, 
            Status = CommunityStatus.ACTIVE 
        };
        _db.Communities.Add(existingComm);
        await _db.SaveChangesAsync();

        var request = new CreateCommunityRequest 
        { 
            Name = "Common Name", 
            ReferenceBookspotId = bs2.Id 
        };

        _chatServiceMock.Setup(c => c.CreateChat(ChatType.COMMUNITY, It.IsAny<List<Guid>>()))
            .ReturnsAsync(new ChatDto(1, "COMMUNITY", DateTime.UtcNow, new List<ChatParticipantDto>(), null));

        // Act
        var result = await _service.CreateCommunityAsync(userId, request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Common Name", result.Name);
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
    public async Task CreateCommunity_FreeUser_AlreadyHasOneCommunity_ThrowsForbidden()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SeedUser(userId, "free-create-limit@test.com", PricingPlan.FREE);

        var bs1 = SeedBookspot(50, MakePoint(0, 0));
        var bs2 = SeedBookspot(51, MakePoint(1, 1));

        var existingComm = new Community
        {
            Name = "Existing Community",
            ReferenceBookspotId = bs1.Id,
            Status = CommunityStatus.ACTIVE
        };
        _db.Communities.Add(existingComm);
        await _db.SaveChangesAsync();

        _db.CommunityMembers.Add(new CommunityMember
        {
            CommunityId = existingComm.Id,
            UserId = userId,
            Role = CommunityRole.MODERATOR
        });
        await _db.SaveChangesAsync();

        var request = new CreateCommunityRequest { Name = "Second Community", ReferenceBookspotId = bs2.Id };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ForbiddenException>(() => _service.CreateCommunityAsync(userId, request));
        Assert.Contains("solo pueden pertenecer a una comunidad no archivada", ex.Message);
    }

    [Fact]
    public async Task CreateCommunity_PremiumUser_AlreadyHasOneCommunity_Succeeds()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SeedUser(userId, "premium-create-multi@test.com", PricingPlan.PREMIUM);

        var bs1 = SeedBookspot(52, MakePoint(0, 0));
        var bs2 = SeedBookspot(53, MakePoint(10, 10)); // Far from bs1 to avoid duplicate name check

        var existingComm = new Community
        {
            Name = "First Community",
            ReferenceBookspotId = bs1.Id,
            Status = CommunityStatus.ACTIVE
        };
        _db.Communities.Add(existingComm);
        await _db.SaveChangesAsync();

        _db.CommunityMembers.Add(new CommunityMember
        {
            CommunityId = existingComm.Id,
            UserId = userId,
            Role = CommunityRole.MODERATOR
        });
        await _db.SaveChangesAsync();

        var request = new CreateCommunityRequest { Name = "Second Community", ReferenceBookspotId = bs2.Id };

        _chatServiceMock.Setup(c => c.CreateChat(ChatType.COMMUNITY, It.IsAny<List<Guid>>()))
            .ReturnsAsync(new ChatDto(2, "COMMUNITY", DateTime.UtcNow, new List<ChatParticipantDto>(), null));

        // Act
        var result = await _service.CreateCommunityAsync(userId, request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Second Community", result.Name);

        var memberships = await _db.CommunityMembers.Where(cm => cm.UserId == userId).CountAsync();
        Assert.Equal(2, memberships);
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

    [Fact]
    public async Task LeaveCommunity_CreatorLeavesWithRemainingMembers_TransfersCreatorAndModerator()
    {
        var creatorId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        SeedUser(creatorId, "creator-leave@test.com", PricingPlan.PREMIUM);
        SeedUser(memberId, "member-leave@test.com", PricingPlan.PREMIUM);

        var bs = SeedBookspot(2, MakePoint(0, 0));
        var comm = new Community
        {
            Name = "TransferComm",
            ReferenceBookspotId = bs.Id,
            Status = CommunityStatus.ACTIVE,
            CreatorId = creatorId,
            CreatedAt = DateTime.UtcNow
        };
        _db.Communities.Add(comm);
        await _db.SaveChangesAsync();

        _db.CommunityMembers.Add(new CommunityMember
        {
            CommunityId = comm.Id,
            UserId = creatorId,
            Role = CommunityRole.MODERATOR,
            JoinedAt = DateTime.UtcNow
        });
        _db.CommunityMembers.Add(new CommunityMember
        {
            CommunityId = comm.Id,
            UserId = memberId,
            Role = CommunityRole.MEMBER,
            JoinedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        await _service.LeaveCommunityAsync(creatorId, comm.Id);

        var updatedCommunity = await _db.Communities.FirstOrDefaultAsync(c => c.Id == comm.Id);
        Assert.NotNull(updatedCommunity);
        Assert.Equal(memberId, updatedCommunity!.CreatorId);

        var member = await _db.CommunityMembers.FirstOrDefaultAsync(m => m.CommunityId == comm.Id && m.UserId == memberId);
        Assert.NotNull(member);
        Assert.Equal(CommunityRole.MODERATOR, member!.Role);
    }

    [Fact]
    public async Task LeaveCommunity_CreatorIsOnlyMember_DeletesCommunity()
    {
        var creatorId = Guid.NewGuid();
        SeedUser(creatorId, "solo-creator@test.com", PricingPlan.PREMIUM);

        var bs = SeedBookspot(3, MakePoint(0, 0));
        var comm = new Community
        {
            Name = "DeleteComm",
            ReferenceBookspotId = bs.Id,
            Status = CommunityStatus.CREATED,
            CreatorId = creatorId,
            CreatedAt = DateTime.UtcNow
        };
        _db.Communities.Add(comm);

        var chat = new Chat { Type = ChatType.COMMUNITY };
        _db.Chats.Add(chat);
        await _db.SaveChangesAsync();

        _db.CommunityChats.Add(new CommunityChat { CommunityId = comm.Id, ChatId = chat.Id });
        _db.CommunityMembers.Add(new CommunityMember
        {
            CommunityId = comm.Id,
            UserId = creatorId,
            Role = CommunityRole.MODERATOR,
            JoinedAt = DateTime.UtcNow
        });
        _db.ChatParticipants.Add(new ChatParticipant { ChatId = chat.Id, UserId = creatorId, JoinedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync();

        await _service.LeaveCommunityAsync(creatorId, comm.Id);

        Assert.Null(await _db.Communities.FirstOrDefaultAsync(c => c.Id == comm.Id));
        Assert.Empty(await _db.CommunityMembers.Where(m => m.CommunityId == comm.Id).ToListAsync());
        Assert.Empty(await _db.CommunityChats.Where(cc => cc.CommunityId == comm.Id).ToListAsync());
    }

    [Fact]
    public async Task DeleteCommunity_Moderator_Succeeds()
    {
        var moderatorId = Guid.NewGuid();
        SeedUser(moderatorId, "moderator@test.com", PricingPlan.PREMIUM);

        var bs = SeedBookspot(4, MakePoint(0, 0));
        var comm = new Community
        {
            Name = "DeleteByModerator",
            ReferenceBookspotId = bs.Id,
            Status = CommunityStatus.ACTIVE,
            CreatorId = moderatorId,
            CreatedAt = DateTime.UtcNow
        };
        _db.Communities.Add(comm);
        await _db.SaveChangesAsync();

        _db.CommunityMembers.Add(new CommunityMember
        {
            CommunityId = comm.Id,
            UserId = moderatorId,
            Role = CommunityRole.MODERATOR,
            JoinedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        await _service.DeleteCommunityAsync(moderatorId, comm.Id);

        Assert.Null(await _db.Communities.FirstOrDefaultAsync(c => c.Id == comm.Id));
        Assert.Empty(await _db.CommunityMembers.Where(m => m.CommunityId == comm.Id).ToListAsync());
    }

    [Fact]
    public async Task DeleteCommunity_MemberWithoutModeratorRole_ThrowsForbidden()
    {
        var creatorId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        SeedUser(creatorId, "creator-del@test.com", PricingPlan.PREMIUM);
        SeedUser(memberId, "member-del@test.com", PricingPlan.PREMIUM);

        var bs = SeedBookspot(5, MakePoint(0, 0));
        var comm = new Community
        {
            Name = "DeleteForbidden",
            ReferenceBookspotId = bs.Id,
            Status = CommunityStatus.ACTIVE,
            CreatorId = creatorId,
            CreatedAt = DateTime.UtcNow
        };
        _db.Communities.Add(comm);
        await _db.SaveChangesAsync();

        _db.CommunityMembers.Add(new CommunityMember
        {
            CommunityId = comm.Id,
            UserId = creatorId,
            Role = CommunityRole.MODERATOR,
            JoinedAt = DateTime.UtcNow
        });
        _db.CommunityMembers.Add(new CommunityMember
        {
            CommunityId = comm.Id,
            UserId = memberId,
            Role = CommunityRole.MEMBER,
            JoinedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<ForbiddenException>(() => _service.DeleteCommunityAsync(memberId, comm.Id));
        Assert.Contains("Solo los moderadores", ex.Message);
    }

    // ==================== KICK MEMBER TESTS ====================

    [Fact]
    public async Task KickMember_ModeratorKicksMember_Succeeds()
    {
        // Arrange
        var moderatorId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        SeedUser(moderatorId, "moderator@test.com", PricingPlan.PREMIUM);
        SeedUser(memberId, "member@test.com", PricingPlan.PREMIUM);

        var bs = SeedBookspot(100, MakePoint(0, 0));
        var comm = new Community
        {
            Name = "KickTestComm",
            ReferenceBookspotId = bs.Id,
            Status = CommunityStatus.ACTIVE,
            CreatorId = moderatorId,
            CreatedAt = DateTime.UtcNow
        };
        _db.Communities.Add(comm);

        var chat = new Chat { Type = ChatType.COMMUNITY };
        _db.Chats.Add(chat);
        await _db.SaveChangesAsync();

        _db.CommunityChats.Add(new CommunityChat { CommunityId = comm.Id, ChatId = chat.Id });
        _db.CommunityMembers.Add(new CommunityMember
        {
            CommunityId = comm.Id,
            UserId = moderatorId,
            Role = CommunityRole.MODERATOR,
            JoinedAt = DateTime.UtcNow
        });
        _db.CommunityMembers.Add(new CommunityMember
        {
            CommunityId = comm.Id,
            UserId = memberId,
            Role = CommunityRole.MEMBER,
            JoinedAt = DateTime.UtcNow
        });
        _db.ChatParticipants.Add(new ChatParticipant { ChatId = chat.Id, UserId = moderatorId, JoinedAt = DateTime.UtcNow });
        _db.ChatParticipants.Add(new ChatParticipant { ChatId = chat.Id, UserId = memberId, JoinedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync();

        // Act
        await _service.KickMemberAsync(moderatorId, comm.Id, memberId);

        // Assert
        var kickedMember = await _db.CommunityMembers
            .FirstOrDefaultAsync(m => m.CommunityId == comm.Id && m.UserId == memberId);
        Assert.Null(kickedMember);

        var chatParticipant = await _db.ChatParticipants
            .FirstOrDefaultAsync(cp => cp.ChatId == chat.Id && cp.UserId == memberId);
        Assert.Null(chatParticipant);

        // Moderator should still be in community
        var moderator = await _db.CommunityMembers
            .FirstOrDefaultAsync(m => m.CommunityId == comm.Id && m.UserId == moderatorId);
        Assert.NotNull(moderator);
    }

    [Fact]
    public async Task KickMember_NonModeratorTriesToKick_ThrowsUnauthorized()
    {
        // Arrange
        var moderatorId = Guid.NewGuid();
        var member1Id = Guid.NewGuid();
        var member2Id = Guid.NewGuid();
        SeedUser(moderatorId, "moderator-kick@test.com", PricingPlan.PREMIUM);
        SeedUser(member1Id, "member1-kick@test.com", PricingPlan.PREMIUM);
        SeedUser(member2Id, "member2-kick@test.com", PricingPlan.PREMIUM);

        var bs = SeedBookspot(101, MakePoint(0, 0));
        var comm = new Community
        {
            Name = "KickUnauthorized",
            ReferenceBookspotId = bs.Id,
            Status = CommunityStatus.ACTIVE,
            CreatorId = moderatorId,
            CreatedAt = DateTime.UtcNow
        };
        _db.Communities.Add(comm);
        await _db.SaveChangesAsync();

        _db.CommunityMembers.Add(new CommunityMember
        {
            CommunityId = comm.Id,
            UserId = moderatorId,
            Role = CommunityRole.MODERATOR,
            JoinedAt = DateTime.UtcNow
        });
        _db.CommunityMembers.Add(new CommunityMember
        {
            CommunityId = comm.Id,
            UserId = member1Id,
            Role = CommunityRole.MEMBER,
            JoinedAt = DateTime.UtcNow
        });
        _db.CommunityMembers.Add(new CommunityMember
        {
            CommunityId = comm.Id,
            UserId = member2Id,
            Role = CommunityRole.MEMBER,
            JoinedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        // Act & Assert - member1 tries to kick member2
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.KickMemberAsync(member1Id, comm.Id, member2Id));
        Assert.Contains("Solo los moderadores pueden expulsar", ex.Message);
    }

    [Fact]
    public async Task KickMember_ModeratorTriesToKickSelf_ThrowsInvalidOperation()
    {
        // Arrange
        var moderatorId = Guid.NewGuid();
        SeedUser(moderatorId, "mod-self-kick@test.com", PricingPlan.PREMIUM);

        var bs = SeedBookspot(102, MakePoint(0, 0));
        var comm = new Community
        {
            Name = "SelfKickComm",
            ReferenceBookspotId = bs.Id,
            Status = CommunityStatus.ACTIVE,
            CreatorId = moderatorId,
            CreatedAt = DateTime.UtcNow
        };
        _db.Communities.Add(comm);
        await _db.SaveChangesAsync();

        _db.CommunityMembers.Add(new CommunityMember
        {
            CommunityId = comm.Id,
            UserId = moderatorId,
            Role = CommunityRole.MODERATOR,
            JoinedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.KickMemberAsync(moderatorId, comm.Id, moderatorId));
        Assert.Contains("No puedes expulsarte a ti mismo", ex.Message);
    }

    [Fact]
    public async Task KickMember_ModeratorTriesToKickAnotherModerator_ThrowsInvalidOperation()
    {
        // Arrange
        var mod1Id = Guid.NewGuid();
        var mod2Id = Guid.NewGuid();
        SeedUser(mod1Id, "mod1@test.com", PricingPlan.PREMIUM);
        SeedUser(mod2Id, "mod2@test.com", PricingPlan.PREMIUM);

        var bs = SeedBookspot(103, MakePoint(0, 0));
        var comm = new Community
        {
            Name = "TwoModsComm",
            ReferenceBookspotId = bs.Id,
            Status = CommunityStatus.ACTIVE,
            CreatorId = mod1Id,
            CreatedAt = DateTime.UtcNow
        };
        _db.Communities.Add(comm);
        await _db.SaveChangesAsync();

        _db.CommunityMembers.Add(new CommunityMember
        {
            CommunityId = comm.Id,
            UserId = mod1Id,
            Role = CommunityRole.MODERATOR,
            JoinedAt = DateTime.UtcNow
        });
        _db.CommunityMembers.Add(new CommunityMember
        {
            CommunityId = comm.Id,
            UserId = mod2Id,
            Role = CommunityRole.MODERATOR,
            JoinedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.KickMemberAsync(mod1Id, comm.Id, mod2Id));
        Assert.Contains("No puedes expulsar a otro moderador", ex.Message);
    }

    [Fact]
    public async Task KickMember_UserNotInCommunity_ThrowsInvalidOperation()
    {
        // Arrange
        var moderatorId = Guid.NewGuid();
        var outsiderId = Guid.NewGuid();
        SeedUser(moderatorId, "mod-outsider@test.com", PricingPlan.PREMIUM);
        SeedUser(outsiderId, "outsider@test.com", PricingPlan.PREMIUM);

        var bs = SeedBookspot(104, MakePoint(0, 0));
        var comm = new Community
        {
            Name = "OutsiderComm",
            ReferenceBookspotId = bs.Id,
            Status = CommunityStatus.ACTIVE,
            CreatorId = moderatorId,
            CreatedAt = DateTime.UtcNow
        };
        _db.Communities.Add(comm);
        await _db.SaveChangesAsync();

        _db.CommunityMembers.Add(new CommunityMember
        {
            CommunityId = comm.Id,
            UserId = moderatorId,
            Role = CommunityRole.MODERATOR,
            JoinedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        // Act & Assert - try to kick someone who's not in the community
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.KickMemberAsync(moderatorId, comm.Id, outsiderId));
        Assert.Contains("no es miembro de esta comunidad", ex.Message);
    }

    [Fact]
    public async Task KickMember_CommunityNotFound_ThrowsInvalidOperation()
    {
        // Arrange
        var moderatorId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        SeedUser(moderatorId, "mod-notfound@test.com", PricingPlan.PREMIUM);
        SeedUser(memberId, "member-notfound@test.com", PricingPlan.PREMIUM);
        await _db.SaveChangesAsync();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.KickMemberAsync(moderatorId, 99999, memberId));
        Assert.Contains("Comunidad no encontrada", ex.Message);
    }
}