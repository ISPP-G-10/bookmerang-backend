using Bookmerang.Api.Data;
using Bookmerang.Api.Exceptions;
using Bookmerang.Api.Models.DTOs.Communities;
using Bookmerang.Api.Models.Entities;
using Bookmerang.Api.Models.Enums;
using Bookmerang.Api.Services.Interfaces.Communities;
using Microsoft.EntityFrameworkCore;

namespace Bookmerang.Api.Services.Implementation.Communities;

public class CommunityLibraryService(AppDbContext db) : ICommunityLibraryService
{
    private readonly AppDbContext _db = db;

    public async Task<List<CommunityLibraryBookDto>> GetCommunityLibraryAsync(Guid userId, int communityId, int page = 1, int pageSize = 20)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 1;
        if (pageSize > 100) pageSize = 100;

        var community = await _db.Communities.FindAsync(communityId);
        if (community == null) throw new NotFoundException("Comunidad no encontrada.");
        if (community.Status != CommunityStatus.ACTIVE)
            throw new ForbiddenException("La comunidad debe estar activa para acceder a la biblioteca.");

        var user = await _db.RegularUsers.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null) throw new NotFoundException("Usuario no encontrado.");

        if (user.Plan == PricingPlan.FREE)
        {
            throw new ForbiddenException("La biblioteca virtual compartida es una funcionalidad exclusiva para usuarios premium.");
        }

        var isMember = await _db.CommunityMembers.AnyAsync(cm => cm.CommunityId == communityId && cm.UserId == userId);
        if (!isMember) throw new ForbiddenException("Debes ser miembro de la comunidad para ver su biblioteca.");

        // Get IDs of premium members only (free users don't participate in the shared library)
        var memberIds = await _db.CommunityMembers
            .Where(cm => cm.CommunityId == communityId)
            .Join(_db.RegularUsers,
                cm => cm.UserId,
                u => u.Id,
                (cm, u) => new { cm.UserId, u.Plan })
            .Where(x => x.Plan != PricingPlan.FREE)
            .Select(x => x.UserId)
            .ToListAsync();

        // Get all published books from these members
        var books = await _db.Books
            .Include(b => b.Photos)
            .Include(b => b.BookGenres).ThenInclude(bg => bg.Genre)
            .Where(b => memberIds.Contains(b.OwnerId) && b.Status == BookStatus.PUBLISHED)
            .ToListAsync();

        var bookIds = books.Select(b => b.Id).ToList();

        // Get all likes for these books in this community
        var likes = await _db.CommunityLibraryLikes
            .Where(l => l.CommunityId == communityId && bookIds.Contains(l.BookId))
            .ToListAsync();

        var owners = await _db.RegularUsers
            .Include(u => u.BaseUser)
            .Where(u => memberIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.BaseUser.Username);

        return books.Select(b => new CommunityLibraryBookDto
        {
            BookId = b.Id,
            OwnerId = b.OwnerId,
            OwnerUsername = owners.GetValueOrDefault(b.OwnerId) ?? "Unknown",
            Titulo = b.Titulo ?? "Sin título",
            Autor = b.Autor ?? "Autor desconocido",
            ThumbnailUrl = b.Photos.OrderBy(p => p.Orden).FirstOrDefault()?.Url,
            Genres = b.BookGenres.Select(bg => bg.Genre.Name).ToList(),
            LikesCount = likes.Count(l => l.BookId == b.Id),
            LikedByMe = likes.Any(l => l.BookId == b.Id && l.UserId == userId)
        })
        .OrderByDescending(dto => dto.LikesCount)
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .ToList();
    }

    public async Task ToggleLikeAsync(Guid userId, int communityId, int bookId)
    {
        var community = await _db.Communities.FindAsync(communityId);
        if (community == null) throw new NotFoundException("Comunidad no encontrada.");
        if (community.Status != CommunityStatus.ACTIVE)
            throw new ForbiddenException("La comunidad debe estar activa para interactuar con la biblioteca.");

        var user = await _db.RegularUsers.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null) throw new NotFoundException("Usuario no encontrado.");

        if (user.Plan == PricingPlan.FREE)
        {
            throw new ForbiddenException("Dar 'Me gusta' en la biblioteca virtual es una funcionalidad exclusiva para usuarios premium.");
        }

        var isMember = await _db.CommunityMembers.AnyAsync(cm => cm.CommunityId == communityId && cm.UserId == userId);
        if (!isMember) throw new ForbiddenException("Debes ser miembro de la comunidad para interactuar con su biblioteca.");

        var book = await _db.Books.FindAsync(bookId);
        if (book == null || book.Status != BookStatus.PUBLISHED) throw new NotFoundException("Libro no encontrado o no disponible.");

        var isOwnerMember = await _db.CommunityMembers.AnyAsync(cm => cm.CommunityId == communityId && cm.UserId == book.OwnerId);
        if (!isOwnerMember) throw new ValidationException("El dueño del libro no pertenece a esta comunidad.");

        var existingLike = await _db.CommunityLibraryLikes
            .FirstOrDefaultAsync(l => l.CommunityId == communityId && l.UserId == userId && l.BookId == bookId);

        if (existingLike != null)
        {
            _db.CommunityLibraryLikes.Remove(existingLike);
        }
        else
        {
            var like = new CommunityLibraryLike
            {
                CommunityId = communityId,
                UserId = userId,
                BookId = bookId,
                CreatedAt = DateTime.UtcNow
            };
            _db.CommunityLibraryLikes.Add(like);
        }

        await _db.SaveChangesAsync();
    }

    public async Task<List<CommunityLibraryBookDto>> GetSuggestedBooksForMeetupAsync(Guid userId, int communityId)
    {
        var community = await _db.Communities.FindAsync(communityId);
        if (community == null) throw new NotFoundException("Comunidad no encontrada.");
        if (community.Status != CommunityStatus.ACTIVE)
            throw new ForbiddenException("La comunidad debe estar activa para obtener sugerencias.");

        var isMember = await _db.CommunityMembers.AnyAsync(cm => cm.CommunityId == communityId && cm.UserId == userId);
        if (!isMember) throw new ForbiddenException("Debes ser miembro de la comunidad.");

        // Get my published books
        var myBooks = await _db.Books
            .Include(b => b.Photos)
            .Where(b => b.OwnerId == userId && b.Status == BookStatus.PUBLISHED)
            .ToListAsync();

        var myBookIds = myBooks.Select(b => b.Id).ToList();

        // Get likes for my books in this community
        var likes = await _db.CommunityLibraryLikes
            .Where(l => l.CommunityId == communityId && myBookIds.Contains(l.BookId))
            .ToListAsync();

        return myBooks.Select(b => new CommunityLibraryBookDto
        {
            BookId = b.Id,
            OwnerId = userId,
            OwnerUsername = "Me",
            Titulo = b.Titulo ?? "Sin título",
            Autor = b.Autor ?? "Autor desconocido",
            ThumbnailUrl = b.Photos.OrderBy(p => p.Orden).FirstOrDefault()?.Url,
            LikesCount = likes.Count(l => l.BookId == b.Id),
            LikedByMe = false // doesn't matter for suggestions
        })
        .Where(dto => dto.LikesCount > 0) // only suggest books with at least 1 like
        .OrderByDescending(dto => dto.LikesCount)
        .ToList();
    }
}