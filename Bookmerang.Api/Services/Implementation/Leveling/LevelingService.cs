using Bookmerang.Api.Data;
using Bookmerang.Api.Models.Enums;
using Bookmerang.Api.Services.Interfaces.Leveling;
using Microsoft.EntityFrameworkCore;

namespace Bookmerang.Api.Services.Implementation.Leveling;

public class LevelingService(AppDbContext db) : ILevelingService
{
    private readonly AppDbContext _db = db;
    private static readonly Dictionary<int, int> XpThresholdCache = new();

    public async Task<int> GetTotalXpAsync(Guid userId)
    {
        var progress = await _db.UserProgresses.FirstOrDefaultAsync(p => p.UserId == userId);
        return progress?.XpTotal ?? 0;
    }

    public (int level, int xpToNextLevel, double progress) CalculateLevelInfo(int totalXp)
    {
        int currentLevel = 1;
        int xpForCurrentLevel = 0;

        for (int level = 1; level <= 50; level++)
        {
            int xpRequired = GetXpRequiredForLevel(level);
            if (totalXp >= xpRequired)
            {
                currentLevel = level;
                xpForCurrentLevel = xpRequired;
            }
            else
            {
                break;
            }
        }

        if (currentLevel > 50)
            currentLevel = 50;

        int xpForNextLevel = GetXpRequiredForLevel(currentLevel + 1);
        int xpInCurrentLevel = totalXp - xpForCurrentLevel;
        int xpNeededForNextLevel = xpForNextLevel - xpForCurrentLevel;
        int xpToNextLevel = Math.Max(0, xpNeededForNextLevel - xpInCurrentLevel);

        double progress = xpNeededForNextLevel > 0 
            ? (double)xpInCurrentLevel / xpNeededForNextLevel 
            : 1.0;

        return (currentLevel, xpToNextLevel, progress);
    }

    public string GetTier(int level)
    {
        return level switch
        {
            <= 4 => "BRONCE",
            >= 5 and <= 14 => "PLATA",
            >= 15 and <= 24 => "ORO",
            >= 25 and <= 34 => "PLATINO",
            >= 35 and <= 44 => "DIAMANTE",
            >= 45 and <= 49 => "PLATINO_ELITE",
            _ => "LEGENDARIO"
        };
    }

    public int GetXpRequiredForLevel(int level)
    {
        if (level < 1) return 0;
        if (level > 50) level = 50;

        if (XpThresholdCache.TryGetValue(level, out var cached))
            return cached;
        
        int cumulativeXp = 0;
        double previousLevelXp = 100;

        for (int lv = 2; lv <= level; lv++)
        {
            if (lv == 2)
            {
                cumulativeXp += 100;
            }
            else if (lv <= 20)
            {
                double currentLevelXp = previousLevelXp * 1.08;
                previousLevelXp = currentLevelXp;
                cumulativeXp += (int)Math.Round(currentLevelXp);
            }
            else
            {
                double currentLevelXp = previousLevelXp * 1.12;
                previousLevelXp = currentLevelXp;
                cumulativeXp += (int)Math.Round(currentLevelXp);
            }
        }

        // Cache the result
        XpThresholdCache[level] = cumulativeXp;
        return cumulativeXp;
    }
}
