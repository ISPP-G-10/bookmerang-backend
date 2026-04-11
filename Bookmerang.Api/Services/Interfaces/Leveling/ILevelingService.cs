namespace Bookmerang.Api.Services.Interfaces.Leveling;

public interface ILevelingService
{
    Task<int> GetTotalXpAsync(Guid userId);
    (int level, int xpToNextLevel, double progress) CalculateLevelInfo(int totalXp);
    string GetTier(int level);
    int GetXpRequiredForLevel(int level);
}
