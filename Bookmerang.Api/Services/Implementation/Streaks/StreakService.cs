using Bookmerang.Api.Data;
using Bookmerang.Api.Models.Entities;
using Bookmerang.Api.Services.Interfaces.Streaks;
using Microsoft.EntityFrameworkCore;

namespace Bookmerang.Api.Services.Implementation.Streaks;

public class StreakService(AppDbContext db) : IStreakService
{
    private readonly AppDbContext _db = db;

    private static readonly decimal[] StreakMultipliers = new[]
    {
        1.00m,
        1.04m,
        1.08m,
        1.12m,
        1.16m,
        1.24m,
        1.36m
    };

    private static DateTime GetCurrentWeekStart(DateTime date)
    {
        var diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
        return date.Date.AddDays(-diff);
    }

    public async Task<int> GetStreakLevelAsync(Guid userId)
    {
        var progress = await _db.UserProgresses.FirstOrDefaultAsync(p => p.UserId == userId);
        return progress?.StreakWeeks ?? 0;
    }

    public async Task RegisterActiveActionAsync(Guid userId)
    {
        var now = DateTime.UtcNow;
        var currentWeekStart = GetCurrentWeekStart(now);

        var progress = await _db.UserProgresses.FirstOrDefaultAsync(p => p.UserId == userId);

        if (progress == null)
        {
            progress = new UserProgress
            {
                UserId = userId,
                XpTotal = 0,
                StreakWeeks = 1,
                LastActiveDate = now,
                StreakStartDate = now,
                UpdatedAt = now
            };
            _db.UserProgresses.Add(progress);
        }
        else
        {
            if (progress.LastActiveDate >= currentWeekStart)
            {
                return;
            }

            progress.LastActiveDate = now;
            progress.UpdatedAt = now;

            if (progress.StreakWeeks == 0)
            {
                progress.StreakWeeks = 1;
                progress.StreakStartDate = now;
            }
            else
            {
                progress.StreakWeeks++;
            }
        }

        await _db.SaveChangesAsync();
    }

    public decimal GetStreakMultiplier(int streakLevel)
    {
        if (streakLevel < 0) streakLevel = 0;
        if (streakLevel >= StreakMultipliers.Length)
            return StreakMultipliers[^1];
        return StreakMultipliers[streakLevel];
    }

    public async Task ProcessDailyStreakDecrementsAsync()
    {
        var now = DateTime.UtcNow;
        var currentWeekStart = GetCurrentWeekStart(now);

        var allProgress = await _db.UserProgresses
            .Where(p => p.StreakWeeks > 0)
            .ToListAsync();

        foreach (var progress in allProgress)
        {
            if (progress.LastActiveDate >= currentWeekStart)
                continue;

            var lastDecrement = progress.LastDecrementDate ?? progress.LastActiveDate ?? DateTime.MinValue;
            var daysSinceLastDecrement = (now - lastDecrement).TotalDays;

            if (daysSinceLastDecrement >= 1)
            {
                int decrements = (int)Math.Floor(daysSinceLastDecrement / 7.0);
                progress.StreakWeeks = Math.Max(0, progress.StreakWeeks - decrements);
                progress.LastDecrementDate = now;
                progress.UpdatedAt = now;
            }
        }

        await _db.SaveChangesAsync();
    }
}