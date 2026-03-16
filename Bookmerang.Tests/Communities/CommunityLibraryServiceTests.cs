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

    [Fact]
    public async Task GetCommunityLibrary_FreeUser_ThrowsForbidden()
    {
        var userId = Guid.NewGuid();
        SeedUser(userId, PricingPlan.FREE);
        var comm = SeedCommunity(1);
        _db.CommunityMembers.Add(new CommunityMember { CommunityId = comm.Id, UserId = userId, Role = CommunityRole.MEMBER });
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<ForbiddenException>(() => _service.GetCommunityLibraryAsync(userId, comm.Id));
    }

    [Fact]
    public async Task ToggleLike_FreeUser_ThrowsForbidden()
    {
        var userId = Guid.NewGuid();
        SeedUser(userId, PricingPlan.FREE);
        var comm = SeedCommunity(1);
        _db.CommunityMembers.Add(new CommunityMember { CommunityId = comm.Id, UserId = userId, Role = CommunityRole.MEMBER });
        
        var ownerId = Guid.NewGuid();
        SeedUser(ownerId, PricingPlan.FREE);
        _db.CommunityMembers.Add(new CommunityMember { CommunityId = comm.Id, UserId = ownerId, Role = CommunityRole.MEMBER });
        
        var book = SeedBook(10, ownerId, BookStatus.PUBLISHED);
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<ForbiddenException>(() => _service.ToggleLikeAsync(userId, comm.Id, book.Id));
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
}