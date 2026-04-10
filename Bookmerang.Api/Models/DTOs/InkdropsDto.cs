namespace Bookmerang.Api.Models.DTOs;

using Bookmerang.Api.Models.Enums;

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

public record InkdropsHistoryDto(
    int Id,
    InkdropsActionType ActionType,
    int PointsGranted,
    DateTime CreatedAt
);
