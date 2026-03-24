namespace Bookmerang.Api.Configuration;

public class MatcherSettings
{
    public const string SectionName = "Matcher";

    public required WeightsSettings Weights { get; set; }
    public required FeedSettings Feed { get; set; }
}

public class WeightsSettings
{
    public required double GenreMatch { get; set; }
    public required double ExtensionMatch { get; set; }
    public required double DistanceScore { get; set; }
    public required double RecencyBonus { get; set; }
}

public class FeedSettings
{
    public required int PriorityToDiscoveryRatio { get; set; }
    public required int DefaultPageSize { get; set; }
    public required int RecencyDecayDays { get; set; }
    public required int SwipeValidDays { get; set; }
    public bool SwipeCleanupEnabled { get; set; } = true;
    public int SwipeCleanupIntervalHours { get; set; } = 48;
}
