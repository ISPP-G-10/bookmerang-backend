using Bookmerang.Api.Data;
using Bookmerang.Api.Exceptions;
using Bookmerang.Api.Models.Entities;
using Bookmerang.Api.Models.Enums;
using Bookmerang.Api.Services.Implementation.Communities;
using Bookmerang.Tests.Helpers;
using NetTopologySuite.Geometries;
using Xunit;

namespace Bookmerang.Tests.Communities;

public class CommunityLibraryServiceTests : IAsyncLifetime
{
    private AppDbContext _db = null!;
    private CommunityLibraryService _service = null!;

    public Task InitializeAsync()
    {
        _db = DbContextFactory.CreateInMemory();
        _service = new CommunityLibraryService(_db);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _db.Database.EnsureDeletedAsync();
        await _db.DisposeAsync();
    }

    private static Point MakePoint(double lon, double lat) =>
        new GeometryFactory(new PrecisionModel(), 4326).CreatePoint(new Coordinate(lon, lat));

    private User SeedUser(Guid id, PricingPlan plan = PricingPlan.FREE)
    {
        var baseUser = new BaseUser { Id = id, SupabaseId = $"sup-{id}", Email = "test@test.com", Username = "test", Name = "test", Location = MakePoint(0,0) };
        _db.Users.Add(baseUser);
        var user = new User { Id = id, Plan = plan };
        _db.RegularUsers.Add(user);
        return user;
    }

    private Community SeedCommunity(int id)
    {
        var bs = new Bookspot { Id = 1, Nombre = "BS", Location = MakePoint(0,0), Status = BookspotStatus.ACTIVE };
        _db.Bookspots.Add(bs);
        var comm = new Community { Id = id, Name = "Comm", ReferenceBookspotId = bs.Id, Status = CommunityStatus.ACTIVE };
        _db.Communities.Add(comm);
        return comm;
    }

    private Book SeedBook(int id, Guid ownerId, BookStatus status)
    {
        var book = new Book { Id = id, OwnerId = ownerId, Status = status, Titulo = $"Book {id}" };
        _db.Books.Add(book);
        return book;
    }

    private void SeedLike(int communityId, Guid userId, int bookId)
    {
        _db.CommunityLibraryLikes.Add(new CommunityLibraryLike
        {
            CommunityId = communityId,
            UserId = userId,
            BookId = bookId,
            CreatedAt = DateTime.UtcNow
        });
    }

    // Note: Free/Premium gating was moved from service to [RequirePremium] controller attribute.
    // The service itself is plan-agnostic; authorization is enforced at the HTTP layer.

    [Fact]
    public async Task GetCommunityLibrary_FreeUser_ServiceAllowsAccess()
    {
        // Gating is now at controller level via [RequirePremium].
        // The service itself does NOT throw for free users.
        var userId = Guid.NewGuid();
        SeedUser(userId, PricingPlan.FREE);
        var comm = SeedCommunity(1);
        _db.CommunityMembers.Add(new CommunityMember { CommunityId = comm.Id, UserId = userId, Role = CommunityRole.MEMBER });
        await _db.SaveChangesAsync();

        var result = await _service.GetCommunityLibraryAsync(userId, comm.Id);
        Assert.NotNull(result);
        Assert.Empty(result); // No books seeded
    }

    [Fact]
    public async Task ToggleLike_FreeUser_ServiceAllowsAccess()
    {
        // Gating is now at controller level via [RequirePremium].
        // The service itself does NOT throw for free users.
        var userId = Guid.NewGuid();
        SeedUser(userId, PricingPlan.FREE);
        var comm = SeedCommunity(1);
        _db.CommunityMembers.Add(new CommunityMember { CommunityId = comm.Id, UserId = userId, Role = CommunityRole.MEMBER });

        var ownerId = Guid.NewGuid();
        SeedUser(ownerId, PricingPlan.PREMIUM);
        _db.CommunityMembers.Add(new CommunityMember { CommunityId = comm.Id, UserId = ownerId, Role = CommunityRole.MEMBER });

        var book = SeedBook(10, ownerId, BookStatus.PUBLISHED);
        await _db.SaveChangesAsync();

        // Should succeed without throwing
        await _service.ToggleLikeAsync(userId, comm.Id, book.Id);
        var library = await _service.GetCommunityLibraryAsync(userId, comm.Id);
        Assert.Equal(1, library[0].LikesCount);
    }

    [Fact]
    public async Task ToggleLike_PremiumUser_TogglesSuccessfully()
    {
        var userId = Guid.NewGuid();
        SeedUser(userId, PricingPlan.PREMIUM);
        
        var ownerId = Guid.NewGuid();
        SeedUser(ownerId, PricingPlan.PREMIUM);

        var comm = SeedCommunity(1);
        _db.CommunityMembers.Add(new CommunityMember { CommunityId = comm.Id, UserId = userId, Role = CommunityRole.MEMBER });
        _db.CommunityMembers.Add(new CommunityMember { CommunityId = comm.Id, UserId = ownerId, Role = CommunityRole.MEMBER });
        
        var book = SeedBook(10, ownerId, BookStatus.PUBLISHED);
        await _db.SaveChangesAsync();

        // Add like
        await _service.ToggleLikeAsync(userId, comm.Id, book.Id);
        
        var library = await _service.GetCommunityLibraryAsync(userId, comm.Id);
        Assert.Single(library);
        Assert.Equal(1, library[0].LikesCount);
        Assert.True(library[0].LikedByMe);

        // Remove like
        await _service.ToggleLikeAsync(userId, comm.Id, book.Id);
        
        library = await _service.GetCommunityLibraryAsync(userId, comm.Id);
        Assert.Equal(0, library[0].LikesCount);
        Assert.False(library[0].LikedByMe);
    }

    [Fact]
    public async Task GetCommunityLibrary_OnlyReturnsBookFromPremiumMembers()
    {
        // Arrange
        var viewerId = Guid.NewGuid();
        SeedUser(viewerId, PricingPlan.PREMIUM);

        var freeOwnerId = Guid.NewGuid();
        SeedUser(freeOwnerId, PricingPlan.FREE);

        var premiumOwnerId = Guid.NewGuid();
        SeedUser(premiumOwnerId, PricingPlan.PREMIUM);

        var comm = SeedCommunity(1);
        _db.CommunityMembers.Add(new CommunityMember { CommunityId = comm.Id, UserId = viewerId, Role = CommunityRole.MEMBER });
        _db.CommunityMembers.Add(new CommunityMember { CommunityId = comm.Id, UserId = freeOwnerId, Role = CommunityRole.MEMBER });
        _db.CommunityMembers.Add(new CommunityMember { CommunityId = comm.Id, UserId = premiumOwnerId, Role = CommunityRole.MEMBER });

        SeedBook(1, freeOwnerId, BookStatus.PUBLISHED);
        var premiumBook = SeedBook(2, premiumOwnerId, BookStatus.PUBLISHED);

        await _db.SaveChangesAsync();

        // Act
        var result = await _service.GetCommunityLibraryAsync(viewerId, comm.Id);

        // Assert: only the premium member's book appears; free member's book is excluded
        Assert.Single(result);
        Assert.Equal(premiumBook.Id, result[0].BookId);
    }

    [Fact]
    public async Task GetCommunityLibrary_NoBooks_WhenAllMembersAreFree()
    {
        // Arrange
        var viewerId = Guid.NewGuid();
        SeedUser(viewerId, PricingPlan.FREE);

        var freeOwner1 = Guid.NewGuid();
        var freeOwner2 = Guid.NewGuid();
        SeedUser(freeOwner1, PricingPlan.FREE);
        SeedUser(freeOwner2, PricingPlan.FREE);

        var comm = SeedCommunity(1);
        _db.CommunityMembers.Add(new CommunityMember { CommunityId = comm.Id, UserId = viewerId, Role = CommunityRole.MEMBER });
        _db.CommunityMembers.Add(new CommunityMember { CommunityId = comm.Id, UserId = freeOwner1, Role = CommunityRole.MEMBER });
        _db.CommunityMembers.Add(new CommunityMember { CommunityId = comm.Id, UserId = freeOwner2, Role = CommunityRole.MEMBER });

        SeedBook(1, freeOwner1, BookStatus.PUBLISHED);
        SeedBook(2, freeOwner2, BookStatus.PUBLISHED);

        await _db.SaveChangesAsync();

        // Act
        var result = await _service.GetCommunityLibraryAsync(viewerId, comm.Id);

        // Assert: no books shown since all owners are free-tier
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetCommunityLibrary_Pagination_ReturnsExpectedPage()
    {
        var viewerId = Guid.NewGuid();
        SeedUser(viewerId, PricingPlan.PREMIUM);

        var comm = SeedCommunity(1);
        _db.CommunityMembers.Add(new CommunityMember { CommunityId = comm.Id, UserId = viewerId, Role = CommunityRole.MEMBER });

        var ownerA = Guid.NewGuid();
        var ownerB = Guid.NewGuid();
        var ownerC = Guid.NewGuid();
        var ownerD = Guid.NewGuid();
        var ownerE = Guid.NewGuid();

        SeedUser(ownerA, PricingPlan.PREMIUM);
        SeedUser(ownerB, PricingPlan.PREMIUM);
        SeedUser(ownerC, PricingPlan.PREMIUM);
        SeedUser(ownerD, PricingPlan.PREMIUM);
        SeedUser(ownerE, PricingPlan.PREMIUM);

        _db.CommunityMembers.Add(new CommunityMember { CommunityId = comm.Id, UserId = ownerA, Role = CommunityRole.MEMBER });
        _db.CommunityMembers.Add(new CommunityMember { CommunityId = comm.Id, UserId = ownerB, Role = CommunityRole.MEMBER });
        _db.CommunityMembers.Add(new CommunityMember { CommunityId = comm.Id, UserId = ownerC, Role = CommunityRole.MEMBER });
        _db.CommunityMembers.Add(new CommunityMember { CommunityId = comm.Id, UserId = ownerD, Role = CommunityRole.MEMBER });
        _db.CommunityMembers.Add(new CommunityMember { CommunityId = comm.Id, UserId = ownerE, Role = CommunityRole.MEMBER });

        var bookA = SeedBook(1, ownerA, BookStatus.PUBLISHED);
        var bookB = SeedBook(2, ownerB, BookStatus.PUBLISHED);
        var bookC = SeedBook(3, ownerC, BookStatus.PUBLISHED);
        var bookD = SeedBook(4, ownerD, BookStatus.PUBLISHED);
        var bookE = SeedBook(5, ownerE, BookStatus.PUBLISHED);

        SeedLike(comm.Id, viewerId, bookA.Id); // 1 like
        SeedLike(comm.Id, viewerId, bookB.Id); // 1 like
        var likeUser1 = Guid.NewGuid();
        var likeUser2 = Guid.NewGuid();
        var likeUser3 = Guid.NewGuid();
        var likeUser4 = Guid.NewGuid();
        SeedUser(likeUser1, PricingPlan.PREMIUM);
        SeedUser(likeUser2, PricingPlan.PREMIUM);
        SeedUser(likeUser3, PricingPlan.PREMIUM);
        SeedUser(likeUser4, PricingPlan.PREMIUM);
        SeedLike(comm.Id, likeUser1, bookC.Id);
        SeedLike(comm.Id, likeUser2, bookC.Id); // 2 likes
        SeedLike(comm.Id, likeUser1, bookD.Id);
        SeedLike(comm.Id, likeUser2, bookD.Id);
        SeedLike(comm.Id, likeUser3, bookD.Id); // 3 likes
        SeedLike(comm.Id, likeUser1, bookE.Id);
        SeedLike(comm.Id, likeUser2, bookE.Id);
        SeedLike(comm.Id, likeUser3, bookE.Id);
        SeedLike(comm.Id, likeUser4, bookE.Id); // 4 likes

        await _db.SaveChangesAsync();

        var page1 = await _service.GetCommunityLibraryAsync(viewerId, comm.Id, page: 1, pageSize: 2);
        Assert.Equal(2, page1.Count);
        Assert.Equal(bookE.Id, page1[0].BookId);
        Assert.Equal(bookD.Id, page1[1].BookId);

        var page2 = await _service.GetCommunityLibraryAsync(viewerId, comm.Id, page: 2, pageSize: 2);
        Assert.Equal(2, page2.Count);
        Assert.Equal(bookC.Id, page2[0].BookId);
    }
}