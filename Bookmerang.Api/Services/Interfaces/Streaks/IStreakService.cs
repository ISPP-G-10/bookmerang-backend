namespace Bookmerang.Api.Services.Interfaces.Streaks;

public interface IStreakService
{
    Task<int> GetStreakLevelAsync(Guid userId);
    Task RegisterActiveActionAsync(Guid userId);
    decimal GetStreakMultiplier(int streakLevel);
    Task ProcessDailyStreakDecrementsAsync();
}