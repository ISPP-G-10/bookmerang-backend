using Bookmerang.Api.Services.Interfaces.Streaks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bookmerang.Api.Services.Implementation.Streaks;

public class StreakMaintenanceHostedService(
    IServiceProvider serviceProvider,
    ILogger<StreakMaintenanceHostedService> logger) : BackgroundService
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly ILogger<StreakMaintenanceHostedService> _logger = logger;
    private Timer? _timer;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var now = DateTime.UtcNow;
        var nextMonday = now.Date.AddDays(((int)DayOfWeek.Monday - (int)now.DayOfWeek + 7) % 7 == 0 ? 7 : ((int)DayOfWeek.Monday - (int)now.DayOfWeek + 7) % 7);
        var delayUntilNextMonday = nextMonday - now;

        _timer = new Timer(DoWork, null, delayUntilNextMonday, TimeSpan.FromDays(7));

        return Task.CompletedTask;
    }

    private void DoWork(object? state)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var streakService = scope.ServiceProvider.GetRequiredService<IStreakService>();
            streakService.ProcessDailyStreakDecrementsAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en ProcessDailyStreakDecrementsAsync");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Change(Timeout.Infinite, 0);
        _timer?.Dispose();
        await base.StopAsync(cancellationToken);
    }
}