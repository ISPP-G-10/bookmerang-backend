using Bookmerang.Api.Models.Entities;

namespace Bookmerang.Api.Services.Interfaces.Books;

public interface ILanguageRepository
{
    /// Devuelve todos los idiomas disponibles.
    Task<List<Language>> GetAllAsync(CancellationToken ct = default);

    /// Verifica que todos los IDs proporcionados existen en BD.
    Task<bool> AllExistAsync(List<int> ids, CancellationToken ct = default);
}