using Bookmerang.Api.Services.Interfaces.Subscriptions;

namespace Bookmerang.Api.Services.Implementation.Subscriptions;

public class SubscriptionExpirationHostedService(
    IServiceProvider serviceProvider,
    ILogger<SubscriptionExpirationHostedService> logger,
    IConfiguration config) : BackgroundService
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly ILogger<SubscriptionExpirationHostedService> _logger = logger;
    private readonly IConfiguration _config = config;
    private PeriodicTimer? _timer;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalMinutes = _config.GetValue<int>("Subscription:ExpirationCheckIntervalMinutes", 60);
        var enabled = _config.GetValue<bool>("Subscription:ExpirationCheckEnabled", true);

        if (!enabled)
        {
            _logger.LogInformation("SubscriptionExpirationHostedService is disabled");
            return;
        }

        _timer = new PeriodicTimer(TimeSpan.FromMinutes(intervalMinutes));

        try
        {
            _logger.LogInformation("SubscriptionExpirationHostedService started (interval: {IntervalMinutes} minutes)", intervalMinutes);

            while (await _timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    _logger.LogInformation("Running subscription expiration check...");

                    using var scope = _serviceProvider.CreateScope();
                    var subscriptionService = scope.ServiceProvider.GetRequiredService<ISubscriptionService>();
                    await subscriptionService.HandleExpirationCheckAsync();

                    _logger.LogInformation("Subscription expiration check completed");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during subscription expiration check");
                }
            }
        }
        finally
        {
            _timer?.Dispose();
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Dispose();
        await base.StopAsync(cancellationToken);
    }
}
