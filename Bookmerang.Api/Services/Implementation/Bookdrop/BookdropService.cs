using Bookmerang.Api.Data;
using Bookmerang.Api.Models.DTOs.Bookdrop;
using Bookmerang.Api.Services.Interfaces.Bookdrop;
using Microsoft.EntityFrameworkCore;

namespace Bookmerang.Api.Services.Implementation.Bookdrop;

public class BookdropService(AppDbContext db) : IBookdropService
{
    private readonly AppDbContext _db = db;

    public async Task<BookdropProfileDto?> GetPerfil(string supabaseId)
    {
        var baseUser = await _db.Users.FirstOrDefaultAsync(u => u.SupabaseId == supabaseId);
        if (baseUser == null) return null;

        var bookdropUser = await _db.BookdropUsers
            .Include(b => b.Bookspot)
            .FirstOrDefaultAsync(b => b.Id == baseUser.Id);
        if (bookdropUser == null) return null;

        return new BookdropProfileDto(
            baseUser.Id,
            baseUser.Email,
            baseUser.Username,
            baseUser.Name,
            baseUser.ProfilePhoto,
            bookdropUser.Bookspot.Nombre,
            bookdropUser.Bookspot.AddressText,
            bookdropUser.Bookspot.Location.Y,
            bookdropUser.Bookspot.Location.X,
            bookdropUser.Bookspot.Status,
            baseUser.CreatedAt
        );
    }

    public async Task<BookdropProfileDto?> UpdatePerfil(string supabaseId, UpdateBookdropProfileRequest request)
    {
        var baseUser = await _db.Users.FirstOrDefaultAsync(u => u.SupabaseId == supabaseId);
        if (baseUser == null) return null;

        var bookdropUser = await _db.BookdropUsers
            .Include(b => b.Bookspot)
            .FirstOrDefaultAsync(b => b.Id == baseUser.Id);
        if (bookdropUser == null) return null;

        var bookspot = bookdropUser.Bookspot;

        if (!string.IsNullOrWhiteSpace(request.NombreEstablecimiento))
            bookspot.Nombre = request.NombreEstablecimiento;

        if (!string.IsNullOrWhiteSpace(request.AddressText))
            bookspot.AddressText = request.AddressText;

        if (request.ProfilePhoto != null)
            baseUser.ProfilePhoto = request.ProfilePhoto;

        if (request.Latitud.HasValue && request.Longitud.HasValue)
        {
            var factory = NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
            var newLocation = factory.CreatePoint(new NetTopologySuite.Geometries.Coordinate(request.Longitud.Value, request.Latitud.Value));
            bookspot.Location = newLocation;
            baseUser.Location = newLocation;
        }

        bookspot.UpdatedAt = DateTime.UtcNow;
        baseUser.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return new BookdropProfileDto(
            baseUser.Id,
            baseUser.Email,
            baseUser.Username,
            baseUser.Name,
            baseUser.ProfilePhoto,
            bookspot.Nombre,
            bookspot.AddressText,
            bookspot.Location.Y,
            bookspot.Location.X,
            bookspot.Status,
            baseUser.CreatedAt
        );
    }
}
