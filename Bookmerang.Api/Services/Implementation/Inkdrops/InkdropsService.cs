using Bookmerang.Api.Data;
using Bookmerang.Api.Models.DTOs;
using Bookmerang.Api.Models.Entities;
using Bookmerang.Api.Models.Enums;
using Bookmerang.Api.Services.Interfaces.Inkdrops;
using Microsoft.EntityFrameworkCore;

namespace Bookmerang.Api.Services.Implementation.Inkdrops;

public class InkdropsService(AppDbContext db) : IInkdropsService
{
    private readonly AppDbContext _db = db;
    private const int ExchangeInkdropsAmount = 100;

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

        await GrantInkdropsToUserAsync(user1Id, currentMonth);
        await GrantInkdropsToUserAsync(user2Id, currentMonth);
        await _db.SaveChangesAsync();
    }

    private async Task GrantInkdropsToUserAsync(Guid userId, string currentMonth)
    {
        var user = await _db.RegularUsers.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null) return;

        if (user.InkdropsLastUpdated != currentMonth)
        {
            user.Inkdrops = 0;
            user.InkdropsLastUpdated = currentMonth;
        }

        user.Inkdrops += ExchangeInkdropsAmount;
        _db.RegularUsers.Update(user);

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
                    InkdropsThisMonth = ExchangeInkdropsAmount
                });
            }
            else
            {
                score.InkdropsThisMonth += ExchangeInkdropsAmount;
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
                (x, bu) => new CommunityRankingEntryDto(
                    x.u.Id,
                    bu.Username,
                    bu.Name,
                    x.s.InkdropsThisMonth
                )
            )
            .OrderByDescending(r => r.InkdropsThisMonth)
            .ToListAsync();

        return new CommunityRankingDto(communityId, currentMonth, ranking);
    }
}
