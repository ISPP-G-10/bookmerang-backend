namespace Bookmerang.Api.Models.DTOs;

public record InkdropsDto(
    Guid UserId,
    int Inkdrops,
    string Month
);

public record CommunityRankingEntryDto(
    Guid UserId,
    string Username,
    string Name,
    int InkdropsThisMonth
);

public record CommunityRankingDto(
    int CommunityId,
    string Month,
    List<CommunityRankingEntryDto> Ranking
);
