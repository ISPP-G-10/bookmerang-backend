using Bookmerang.Api.Data;
using Bookmerang.Api.Models.Entities;
using Bookmerang.Api.Models.Enums;
using NetTopologySuite.Geometries;

namespace Bookmerang.Tests.Helpers;

public static class TestSeedHelper
{
    public static async Task<Guid> SeedUser(AppDbContext db, string prefix)
    {
        var id = Guid.NewGuid();
        db.Users.Add(new BaseUser
        {
            Id = id,
            SupabaseId = $"supa_{id:N}",
            Email = $"{prefix}_{id:N}@test.com",
            Username = prefix,
            Name = $"{prefix} User",
            ProfilePhoto = "photo.jpg",
            UserType = BaseUserType.USER,
            Location = new Point(-5.98, 37.39) { SRID = 4326 }
        });
        db.RegularUsers.Add(new User { Id = id });
        await db.SaveChangesAsync();
        return id;
    }

    public static async Task<Bookspot> SeedBookspot(
        AppDbContext db, bool isBookdrop = false, BookspotStatus status = BookspotStatus.ACTIVE)
    {
        var bookspot = new Bookspot
        {
            Nombre = isBookdrop ? "Test Bookdrop" : "Test Bookspot",
            AddressText = "Test Address",
            Location = new Point(-5.98, 37.39) { SRID = 4326 },
            IsBookdrop = isBookdrop,
            Status = status
        };
        db.Bookspots.Add(bookspot);
        await db.SaveChangesAsync();
        return bookspot;
    }
}
