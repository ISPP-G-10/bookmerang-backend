using Bookmerang.Api.Models.DTOs.Bookdrop;

namespace Bookmerang.Api.Services.Interfaces.Bookdrop;

public interface IBookdropService
{
    Task<BookdropProfileDto?> GetPerfil(string supabaseId);
    Task<BookdropProfileDto?> UpdatePerfil(string supabaseId, UpdateBookdropProfileRequest request);
}
