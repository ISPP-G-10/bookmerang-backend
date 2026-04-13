using Bookmerang.Api.Data;
using Bookmerang.Api.Models.DTOs;
using Bookmerang.Api.Models.Entities;
using Bookmerang.Api.Models.Enums;
using Bookmerang.Api.Services.Interfaces.Inkdrops;
using Bookmerang.Api.Services.Interfaces.Streaks;
using Microsoft.EntityFrameworkCore;

namespace Bookmerang.Api.Services.Implementation.Inkdrops;

public class InkdropsService(AppDbContext db, IStreakService streakService) : IInkdropsService
{
    private readonly AppDbContext _db = db;
    private readonly IStreakService _streakService = streakService;
    private const int ExchangeInkdropsAmount = 100;
    private const int MeetupInkdropsAmount = 200;

    public async Task<InkdropsDto> GetUserInkdropsAsync(Guid userId)
    {
        var currentMonth = DateTime.UtcNow.ToString("yyyy-MM");
        var user = await _db.RegularUsers.FirstOrDefaultAsync(u => u.Id == userId)
            ?? throw new InvalidOperationException("Usuario no encontrado");

        if (user.InkdropsLastUpdated != currentMonth)
        {
            user.Inkdrops = 0;
            user.InkdropsLastUpdated = currentMonth;
            _db.RegularUsers.Update(user);
            await _db.SaveChangesAsync();
        }

        return new InkdropsDto(user.Id, user.Inkdrops, currentMonth);
    }

    public async Task GrantExchangeInkdropsAsync(Guid user1Id, Guid user2Id)
    {
        var currentMonth = DateTime.UtcNow.ToString("yyyy-MM");

        await GrantInkdropsToUserAsync(user1Id, currentMonth, ExchangeInkdropsAmount, InkdropsActionType.EXCHANGE_COMPLETED);
        await GrantInkdropsToUserAsync(user2Id, currentMonth, ExchangeInkdropsAmount, InkdropsActionType.EXCHANGE_COMPLETED);
        await _db.SaveChangesAsync();
    }

    public async Task GrantMeetupInkdropsAsync(Guid userId)
    {
        var currentMonth = DateTime.UtcNow.ToString("yyyy-MM");
        await GrantInkdropsToUserAsync(userId, currentMonth, MeetupInkdropsAmount, InkdropsActionType.MEETUP_ATTENDED);
        await _db.SaveChangesAsync();
    }

    private async Task GrantInkdropsToUserAsync(Guid userId, string currentMonth, int amount = -1, InkdropsActionType? actionType = null)
    {
        if (amount == -1)
            amount = ExchangeInkdropsAmount;

        var user = await _db.RegularUsers.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null) return;

        // Obtener el nivel de racha y calcular multiplicador
        var streakLevel = await _streakService.GetStreakLevelAsync(userId);
        var multiplier = _streakService.GetStreakMultiplier(streakLevel);
        int finalPoints = (int)Math.Round(amount * multiplier);

        // Registrar la acción activa (actualiza racha)
        await _streakService.RegisterActiveActionAsync(userId);

        await UpdateUserInkdropsAsync(user, currentMonth, finalPoints);
        await UpdateUserProgressXpAsync(userId, finalPoints);
        await RecordInkdropsHistoryAsync(userId, actionType, finalPoints);
        await UpdateCommunityScoresAsync(userId, currentMonth, finalPoints);
    }

    private async Task UpdateUserInkdropsAsync(User user, string currentMonth, int finalPoints)
    {
        if (user.InkdropsLastUpdated != currentMonth)
        {
            user.Inkdrops = 0;
            user.InkdropsLastUpdated = currentMonth;
        }

        user.Inkdrops += finalPoints;
        _db.RegularUsers.Update(user);
    }

    private async Task UpdateUserProgressXpAsync(Guid userId, int finalPoints)
    {
        var progress = await _db.UserProgresses.FirstOrDefaultAsync(p => p.UserId == userId);
        if (progress == null) return;

        progress.XpTotal += finalPoints;
        progress.UpdatedAt = DateTime.UtcNow;
        _db.UserProgresses.Update(progress);
    }

    private async Task RecordInkdropsHistoryAsync(Guid userId, InkdropsActionType? actionType, int finalPoints)
    {
        if (actionType.HasValue)
        {
            var history = new InkdropsHistory
            {
                UserId = userId,
                ActionType = actionType.Value,
                PointsGranted = finalPoints,
                CreatedAt = DateTime.UtcNow
            };
            _db.InkdropsHistories.Add(history);
        }
    }

    private async Task UpdateCommunityScoresAsync(Guid userId, string currentMonth, int finalPoints)
    {
        var communities = await _db.CommunityMembers
            .Where(cm => cm.UserId == userId)
            .Select(cm => cm.CommunityId)
            .ToListAsync();

        foreach (var communityId in communities)
        {
            var score = await _db.CommunityMonthlyScores
                .FirstOrDefaultAsync(s => s.CommunityId == communityId && s.UserId == userId && s.Month == currentMonth);

            if (score == null)
            {
                _db.CommunityMonthlyScores.Add(new CommunityMonthlyScore
                {
                    CommunityId = communityId,
                    UserId = userId,
                    Month = currentMonth,
                    InkdropsThisMonth = finalPoints
                });
            }
            else
            {
                score.InkdropsThisMonth += finalPoints;
                _db.CommunityMonthlyScores.Update(score);
            }
        }
    }

    public async Task<CommunityRankingDto> GetCommunityRankingAsync(Guid requestingUserId, int communityId)
    {
        var requestingUser = await _db.RegularUsers.FirstOrDefaultAsync(u => u.Id == requestingUserId)
            ?? throw new InvalidOperationException("Usuario no encontrado");

        if (requestingUser.Plan != PricingPlan.PREMIUM)
            throw new InvalidOperationException("Solo usuarios PREMIUM pueden ver el ranking");

        var isMember = await _db.CommunityMembers
            .AnyAsync(cm => cm.CommunityId == communityId && cm.UserId == requestingUserId);

        if (!isMember)
            throw new InvalidOperationException("No eres miembro de esta comunidad");

        var currentMonth = DateTime.UtcNow.ToString("yyyy-MM");

        var ranking = await _db.CommunityMonthlyScores
            .Where(s => s.CommunityId == communityId && s.Month == currentMonth)
            .Join(
                _db.RegularUsers.Where(u => u.Plan == PricingPlan.PREMIUM),
                s => s.UserId,
                u => u.Id,
                (s, u) => new { s, u }
            )
            .Join(
                _db.Users,
                x => x.u.Id,
                bu => bu.Id,
                (x, bu) => new { x.u.Id, bu.Username, bu.Name, x.s.InkdropsThisMonth }
            )
            .OrderByDescending(r => r.InkdropsThisMonth)
            .ToListAsync();

        var rankingUserIds = ranking.Select(r => r.Id).ToList();
        var personalizationMap = await _db.UserProgresses
            .Where(p => rankingUserIds.Contains(p.UserId))
            .ToDictionaryAsync(p => p.UserId, p => (p.ActiveFrameId, p.ActiveColorId));

        var rankingDtos = ranking.Select(r => {
            personalizationMap.TryGetValue(r.Id, out var pers);
            return new CommunityRankingEntryDto(r.Id, r.Username, r.Name, r.InkdropsThisMonth, pers.ActiveFrameId, pers.ActiveColorId);
        }).ToList();

        return new CommunityRankingDto(communityId, currentMonth, rankingDtos);
    }

    public async Task<List<InkdropsHistoryDto>> GetInkdropsHistoryAsync(Guid userId)
    {
        var history = await _db.InkdropsHistories
            .Where(h => h.UserId == userId)
            .OrderByDescending(h => h.CreatedAt)
            .ToListAsync();

        return history.Select(h => new InkdropsHistoryDto(
            h.Id,
            h.ActionType,
            h.PointsGranted,
            h.CreatedAt
        )).ToList();
    }
}
