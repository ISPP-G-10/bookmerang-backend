using Bookmerang.Api.Data;
using Bookmerang.Api.Exceptions;
using Bookmerang.Api.Models.DTOs.Communities;
using Bookmerang.Api.Models.Entities;
using Bookmerang.Api.Models.Enums;
using Bookmerang.Api.Services.Interfaces.Chats;
using Bookmerang.Api.Services.Interfaces.Communities;
using Microsoft.EntityFrameworkCore;

namespace Bookmerang.Api.Services.Implementation.Communities;

public class CommunityService(AppDbContext db, IChatService chatService) : ICommunityService
{
    private readonly AppDbContext _db = db;
    private readonly IChatService _chatService = chatService;

    public async Task<List<CommunityDto>> ExploreCommunitiesAsync(Guid userId, double latitude, double longitude, int radiusKm = 50)
    {
        var factory = NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
        var location = factory.CreatePoint(new NetTopologySuite.Geometries.Coordinate(longitude, latitude));

        var communities = await _db.Communities
            .Include(c => c.ReferenceBookspot)
            .Include(c => c.Members)
            .Include(c => c.CommunityChat)
            .Where(c => c.Status == CommunityStatus.ACTIVE || c.Status == CommunityStatus.CREATED)
            .Where(c => c.ReferenceBookspot.Location.IsWithinDistance(location, radiusKm * 1000.0))
            .ToListAsync();

        return communities.Select(c => new CommunityDto
        {
            Id = c.Id,
            Name = c.Name,
            ReferenceBookspotId = c.ReferenceBookspotId,
            Status = c.Status,
            CreatorId = c.CreatorId,
            CreatedAt = c.CreatedAt,
            ChatId = c.CommunityChat?.ChatId,
            MemberCount = c.Members.Count
        }).ToList();
    }

    public async Task<CommunityDto> CreateCommunityAsync(Guid creatorId, CreateCommunityRequest request)
    {
        var user = await _db.RegularUsers.Include(u => u.BaseUser).FirstOrDefaultAsync(u => u.Id == creatorId);
        if (user == null) throw new NotFoundException("Usuario no encontrado.");

        // Check if free user is already in another active community
        if (user.Plan == PricingPlan.FREE)
        {
            var activeCommunities = await _db.CommunityMembers
                .Include(cm => cm.Community)
                .Where(cm => cm.UserId == creatorId && cm.Community.Status != CommunityStatus.ARCHIVED)
                .CountAsync();
            
            if (activeCommunities >= 1)
            {
                throw new ForbiddenException("Los usuarios con plan gratuito solo pueden pertenecer a una comunidad no archivada.");
            }
        }

        var bookspot = await _db.Bookspots.FindAsync(request.ReferenceBookspotId);
        if (bookspot == null) throw new NotFoundException("BookSpot no encontrado.");

        // Check if community with same name exists in a nearby radius
        // Note: InMemory uses degrees, PostGIS geography uses meters. 0.05 degrees ~ 5km.
        double distanceLimit = _db.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory" ? 0.05 : 5000.0;

        var nameExistsNearby = await _db.Communities
            .Include(c => c.ReferenceBookspot)
            .AnyAsync(c => c.Name.ToLower() == request.Name.ToLower() 
                && c.Status != CommunityStatus.ARCHIVED
                && c.ReferenceBookspot.Location.IsWithinDistance(bookspot.Location, distanceLimit));

        if (nameExistsNearby)
        {
            throw new ValidationException("Ya existe una comunidad con ese nombre en esta zona.");
        }

        using var transaction = await _db.Database.BeginTransactionAsync();

        try
        {
            var community = new Community
            {
                Name = request.Name,
                ReferenceBookspotId = request.ReferenceBookspotId,
                Status = CommunityStatus.CREATED,
                CreatorId = creatorId,
                CreatedAt = DateTime.UtcNow
            };

            _db.Communities.Add(community);
            await _db.SaveChangesAsync();

            var member = new CommunityMember
            {
                CommunityId = community.Id,
                UserId = creatorId,
                Role = CommunityRole.MODERATOR,
                JoinedAt = DateTime.UtcNow
            };
            _db.CommunityMembers.Add(member);
            await _db.SaveChangesAsync();

            var chatDto = await _chatService.CreateChat(ChatType.COMMUNITY, new List<Guid> { creatorId });
            if (chatDto == null) throw new Exception("No se pudo crear el chat de la comunidad.");

            var communityChat = new CommunityChat
            {
                CommunityId = community.Id,
                ChatId = chatDto.Id
            };
            _db.CommunityChats.Add(communityChat);
            await _db.SaveChangesAsync();

            await transaction.CommitAsync();

            return new CommunityDto
            {
                Id = community.Id,
                Name = community.Name,
                ReferenceBookspotId = community.ReferenceBookspotId,
                Status = community.Status,
                CreatorId = community.CreatorId,
                CreatedAt = community.CreatedAt,
                ChatId = chatDto.Id,
                MemberCount = 1
            };
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<CommunityDto> JoinCommunityAsync(Guid userId, int communityId)
    {
        var community = await _db.Communities
            .Include(c => c.Members)
            .Include(c => c.CommunityChat)
            .FirstOrDefaultAsync(c => c.Id == communityId);
        
        if (community == null) throw new NotFoundException("Comunidad no encontrada.");
        
        if (community.Members.Any(m => m.UserId == userId))
            throw new ValidationException("Ya perteneces a esta comunidad.");

        if (community.Members.Count >= 10)
            throw new ValidationException("La comunidad está llena (máximo 10 miembros).");

        var user = await _db.RegularUsers.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null) throw new NotFoundException("Usuario no encontrado.");

        if (user.Plan == PricingPlan.FREE)
        {
            var activeCommunities = await _db.CommunityMembers
                .Include(cm => cm.Community)
                .Where(cm => cm.UserId == userId && cm.Community.Status != CommunityStatus.ARCHIVED)
                .CountAsync();
            
            if (activeCommunities >= 1)
            {
                throw new ForbiddenException("Los usuarios con plan gratuito solo pueden pertenecer a una comunidad no archivada. Abandona tu comunidad actual para unirte a otra.");
            }
        }

        using var transaction = await _db.Database.BeginTransactionAsync();

        try
        {
            var memberCountBeforeJoin = community.Members.Count;

            var member = new CommunityMember
            {
                CommunityId = communityId,
                UserId = userId,
                Role = CommunityRole.MEMBER,
                JoinedAt = DateTime.UtcNow
            };
            _db.CommunityMembers.Add(member);

            if (memberCountBeforeJoin + 1 >= 3 && community.Status == CommunityStatus.CREATED)
            {
                community.Status = CommunityStatus.ACTIVE;
            }

            if (community.CommunityChat != null)
            {
                var chatParticipant = new ChatParticipant
                {
                    ChatId = community.CommunityChat.ChatId,
                    UserId = userId,
                    JoinedAt = DateTime.UtcNow
                };
                _db.ChatParticipants.Add(chatParticipant);
            }

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            return new CommunityDto
            {
                Id = community.Id,
                Name = community.Name,
                ReferenceBookspotId = community.ReferenceBookspotId,
                Status = community.Status,
                CreatorId = community.CreatorId,
                CreatedAt = community.CreatedAt,
                ChatId = community.CommunityChat?.ChatId,
                MemberCount = memberCountBeforeJoin + 1
            };
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task LeaveCommunityAsync(Guid userId, int communityId)
    {
        var member = await _db.CommunityMembers.FirstOrDefaultAsync(m => m.CommunityId == communityId && m.UserId == userId);
        if (member == null) throw new NotFoundException("No perteneces a esta comunidad.");

        var communityChat = await _db.CommunityChats.FirstOrDefaultAsync(cc => cc.CommunityId == communityId);
        
        using var transaction = await _db.Database.BeginTransactionAsync();

        try
        {
            _db.CommunityMembers.Remove(member);

            if (communityChat != null)
            {
                var chatParticipant = await _db.ChatParticipants.FirstOrDefaultAsync(cp => cp.ChatId == communityChat.ChatId && cp.UserId == userId);
                if (chatParticipant != null)
                {
                    _db.ChatParticipants.Remove(chatParticipant);
                }
            }

            // Remove likes in library
            var likes = await _db.CommunityLibraryLikes.Where(l => l.CommunityId == communityId && l.UserId == userId).ToListAsync();
            _db.CommunityLibraryLikes.RemoveRange(likes);

            // Remove future meetup attendances
            var attendances = await _db.MeetupAttendances
                .Include(ma => ma.Meetup)
                .Where(ma => ma.Meetup.CommunityId == communityId && ma.UserId == userId && ma.Meetup.Status == MeetupStatus.SCHEDULED)
                .ToListAsync();
            
            _db.MeetupAttendances.RemoveRange(attendances);

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<CommunityDto> GetCommunityDetailsAsync(int communityId)
    {
        var community = await _db.Communities
            .Include(c => c.Members)
            .Include(c => c.CommunityChat)
            .FirstOrDefaultAsync(c => c.Id == communityId);

        if (community == null) throw new NotFoundException("Comunidad no encontrada.");

        return new CommunityDto
        {
            Id = community.Id,
            Name = community.Name,
            ReferenceBookspotId = community.ReferenceBookspotId,
            Status = community.Status,
            CreatorId = community.CreatorId,
            CreatedAt = community.CreatedAt,
            ChatId = community.CommunityChat?.ChatId,
            MemberCount = community.Members.Count
        };
    }

    public async Task<List<CommunityDto>> GetMyCommunitiesAsync(Guid userId)
    {
        var communities = await _db.CommunityMembers
            .Include(cm => cm.Community)
                .ThenInclude(c => c.Members)
            .Include(cm => cm.Community)
                .ThenInclude(c => c.CommunityChat)
            .Where(cm => cm.UserId == userId && cm.Community.Status != CommunityStatus.ARCHIVED)
            .Select(cm => cm.Community)
            .ToListAsync();

        return communities.Select(c => new CommunityDto
        {
            Id = c.Id,
            Name = c.Name,
            ReferenceBookspotId = c.ReferenceBookspotId,
            Status = c.Status,
            CreatorId = c.CreatorId,
            CreatedAt = c.CreatedAt,
            ChatId = c.CommunityChat?.ChatId,
            MemberCount = c.Members.Count
        }).ToList();
    }
}