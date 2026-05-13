using Bookmerang.Api.Configuration;
using Bookmerang.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Bookmerang.Api.Services.Implementation.Matcher;

public class SwipeCleanupHostedService(
    IServiceProvider serviceProvider,
    IOptions<MatcherSettings> settings,
    ILogger<SwipeCleanupHostedService> logger) : BackgroundService
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly FeedSettings _feedSettings = settings.Value.Feed;
    private readonly ILogger<SwipeCleanupHostedService> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_feedSettings.SwipeCleanupEnabled)
        {
            _logger.LogInformation("[SWIPE-CLEANUP] Disabled by configuration.");
            return;
        }

        var configuredHours = _feedSettings.SwipeCleanupIntervalHours;
        var intervalHours = configuredHours > 0 ? configuredHours : 48;

        if (configuredHours <= 0)
        {
            _logger.LogWarning(
                "[SWIPE-CLEANUP] Invalid SwipeCleanupIntervalHours={Hours}. Falling back to 48h.",
                configuredHours);
        }

        _logger.LogInformation("[SWIPE-CLEANUP] Started with interval {IntervalHours}h.", intervalHours);

        using var timer = new PeriodicTimer(TimeSpan.FromHours(intervalHours));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var hasNextTick = await timer.WaitForNextTickAsync(stoppingToken);
                if (!hasNextTick)
                {
                    break;
                }

                await CleanupSwipesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SWIPE-CLEANUP] Cleanup failed.");
            }
        }

        _logger.LogInformation("[SWIPE-CLEANUP] Stopped.");
    }

    private async Task CleanupSwipesAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Solo borrar swipes más antiguos que SwipeValidDays para evitar perder el historial
        // de libros ya vistos (lo que permite filtrarlos del feed) mientras estén dentro de la ventana de validez
        var cutoff = DateTime.UtcNow.AddDays(-_feedSettings.SwipeValidDays);
        var deleted = await db.Swipes
            .Where(s => s.CreatedAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken);
        
        _logger.LogInformation("[SWIPE-CLEANUP] Deleted {DeletedCount} old swipes (older than {CutoffDays} days).", 
            deleted, _feedSettings.SwipeValidDays);
    }
}
