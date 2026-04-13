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

    public async Task<List<BookdropProfileDto>> GetAll()
    {
        var bookdropUsers = await _db.BookdropUsers
            .Include(b => b.BaseUser)
            .Include(b => b.Bookspot)
            .ToListAsync();

        return bookdropUsers.Select(b => new BookdropProfileDto(
            b.Id,
            b.BaseUser.Email,
            b.BaseUser.Username,
            b.BaseUser.Name,
            b.BaseUser.ProfilePhoto,
            b.Bookspot.Nombre,
            b.Bookspot.AddressText,
            b.Bookspot.Location.Y,
            b.Bookspot.Location.X,
            b.Bookspot.Status,
            b.BaseUser.CreatedAt
        )).ToList();
    }

    public async Task<int?> GetBookspotIdBySupabaseId(string supabaseId)
    {
        var baseUser = await _db.Users.FirstOrDefaultAsync(u => u.SupabaseId == supabaseId);
        if (baseUser == null) return null;

        var bookdropUser = await _db.BookdropUsers.FirstOrDefaultAsync(b => b.Id == baseUser.Id);
        return bookdropUser?.BookSpotId;
    }

    public async Task<(bool found, string? error)> DeleteBookdrop(Guid bookdropUserId)
    {
        var bookdropUser = await _db.BookdropUsers
            .Include(b => b.Bookspot)
            .FirstOrDefaultAsync(b => b.Id == bookdropUserId);
        if (bookdropUser == null) return (false, null);

        using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            var bookspot = bookdropUser.Bookspot;

            // Eliminar validaciones del bookspot
            var validations = await _db.BookspotValidations
                .Where(v => v.BookspotId == bookspot.Id)
                .ToListAsync();
            if (validations.Any()) _db.BookspotValidations.RemoveRange(validations);

            // Desvincular owner del bookspot antes de borrar bookdrop_user
            bookspot.OwnerId = null;
            await _db.SaveChangesAsync();

            _db.BookdropUsers.Remove(bookdropUser);
            _db.Bookspots.Remove(bookspot);
            await _db.SaveChangesAsync();

            var baseUser = await _db.Users.FindAsync(bookdropUserId);
            if (baseUser != null)
            {
                _db.Users.Remove(baseUser);
                await _db.SaveChangesAsync();
            }

            await transaction.CommitAsync();
            return (true, null);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
