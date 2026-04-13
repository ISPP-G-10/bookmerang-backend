using Bookmerang.Api.Data;
using Bookmerang.Api.Models.Entities;
using Bookmerang.Api.Models.Enums;
using Bookmerang.Api.Services.Interfaces.Subscriptions;
using Microsoft.EntityFrameworkCore;

namespace Bookmerang.Api.Services.Implementation.Subscriptions;

public class MonthlyRankingRewardHostedService(
    IServiceProvider serviceProvider,
    ILogger<MonthlyRankingRewardHostedService> logger,
    IConfiguration config) : BackgroundService
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly ILogger<MonthlyRankingRewardHostedService> _logger = logger;
    private readonly IConfiguration _config = config;
    private PeriodicTimer? _timer;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var enabled = _config.GetValue<bool>("Ranking:RewardEnabled", true);

        if (!enabled)
        {
            _logger.LogInformation("MonthlyRankingRewardHostedService is disabled");
            return;
        }

        // Run daily
        _timer = new PeriodicTimer(TimeSpan.FromHours(24));

        try
        {
            _logger.LogInformation("MonthlyRankingRewardHostedService started");

            while (await _timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    // Only run on the 1st day of the month
                    if (DateTime.UtcNow.Day == 1)
                    {
                        _logger.LogInformation("Running monthly ranking reward check...");
                        await ProcessMonthlyRewardsAsync();
                        _logger.LogInformation("Monthly ranking reward check completed");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during monthly ranking reward check");
                }
            }
        }
        finally
        {
            _timer?.Dispose();
        }
    }

    private async Task ProcessMonthlyRewardsAsync()
    {
        var minPremiumMembers = _config.GetValue<int>("Ranking:MinPremiumMembers", 3);
        var minInkdropsThreshold = _config.GetValue<int>("Ranking:MinInkdropsThreshold", 200);

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var subscriptionService = scope.ServiceProvider.GetRequiredService<ISubscriptionService>();

        // Get all active communities
        var communities = await db.Communities
            .Include(c => c.Members)
            .Where(c => c.Status == CommunityStatus.ACTIVE)
            .ToListAsync();

        foreach (var community in communities)
        {
            try
            {
                await ProcessCommunityRewardAsync(db, subscriptionService, community, minPremiumMembers, minInkdropsThreshold);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing reward for community {CommunityId}", community.Id);
            }
        }
    }

    private async Task ProcessCommunityRewardAsync(
        AppDbContext db,
        ISubscriptionService subscriptionService,
        Community community,
        int minPremiumMembers,
        int minInkdropsThreshold)
    {
        // Get premium members in this community
        var premiumMembers = await db.CommunityMembers
            .Include(cm => cm.User)
            .Where(cm => cm.CommunityId == community.Id && cm.User.Plan == PricingPlan.PREMIUM)
            .Select(cm => cm.UserId)
            .ToListAsync();

        if (premiumMembers.Count < minPremiumMembers)
        {
            _logger.LogInformation(
                "Community {CommunityId} has {PremiumCount} premium members, need at least {MinMembers}",
                community.Id, premiumMembers.Count, minPremiumMembers);
            return;
        }

        // Get members who participated this month (simplified: any member with recent exchanges)
        var thisMonth = DateTime.UtcNow.AddMonths(-1);
        var thisMonthStart = new DateTime(thisMonth.Year, thisMonth.Month, 1);
        var thisMonthEnd = thisMonth.AddMonths(1).AddDays(-1).AddHours(23).AddMinutes(59).AddSeconds(59);

        var participatingMembers = await db.Exchanges
            .Include(e => e.Match)
            .Where(e => e.CreatedAt >= thisMonthStart && e.CreatedAt <= thisMonthEnd)
            .Select(e => e.Match.User1Id) // Simplified: just track one user per exchange
            .Distinct()
            .Where(userId => premiumMembers.Contains(userId))
            .ToListAsync();

        if (participatingMembers.Count < minPremiumMembers)
        {
            _logger.LogInformation(
                "Community {CommunityId} has only {ParticipatingCount} premium members with activity, need at least {MinMembers}",
                community.Id, participatingMembers.Count, minPremiumMembers);
            return;
        }

        // For MVP: award to the first participating member
        if (participatingMembers.Count > 0)
        {
            var winnerId = participatingMembers[0];
            try
            {
                await subscriptionService.ExtendSubscriptionAsync(winnerId, 1, SubscriptionPlatform.SYSTEM);
                _logger.LogInformation(
                    "Awarded 1 month subscription to user {UserId} for ranking in community {CommunityId}",
                    winnerId, community.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to extend subscription for winner {UserId} in community {CommunityId}",
                    winnerId, community.Id);
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Dispose();
        await base.StopAsync(cancellationToken);
    }
}
