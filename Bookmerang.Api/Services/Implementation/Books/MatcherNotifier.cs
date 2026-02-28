using Bookmerang.Api.Services.Interfaces.Books;

namespace Bookmerang.Api.Services.Implementation.Books;

/// <summary>
/// Implementación temporal de IMatcherNotifier.
/// No hace nada — existe solo para que el servicio pueda inyectarla
/// sin que nada explote mientras el módulo matcher no está listo.
/// </summary>
public class DummyMatcherNotifier : IMatcherNotifier
{
    public Task OnBookPublishedAsync(int bookId, Guid ownerId, CancellationToken ct = default)
        => Task.CompletedTask;
}