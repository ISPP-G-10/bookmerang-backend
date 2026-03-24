using Bookmerang.Api.Models.Enums;

namespace Bookmerang.Api.Models.DTOs.Communities;

public class CommunityMemberDto
{
    public int CommunityId { get; set; }
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string ProfilePhoto { get; set; } = string.Empty;
    public CommunityRole Role { get; set; }
    public DateTime JoinedAt { get; set; }
}