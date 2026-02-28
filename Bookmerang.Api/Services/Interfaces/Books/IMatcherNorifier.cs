namespace Bookmerang.Api.Services.Interfaces.Books;

/// <summary>
/// TODO: Implementación real pendiente del grupo de Matcher.
/// Por ahora se usa DummyMatcherNotifier que no hace nada.
/// </summary>
public interface IMatcherNotifier
{
    Task OnBookPublishedAsync(int bookId, Guid ownerId, CancellationToken ct = default);
}