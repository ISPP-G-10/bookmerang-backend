using Bookmerang.Api.Models.Enums;

namespace Bookmerang.Api.Models.DTOs.Communities;

public class CommunityDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int ReferenceBookspotId { get; set; }
    public CommunityStatus Status { get; set; }
    public Guid? CreatorId { get; set; }
    public DateTime CreatedAt { get; set; }
    public CommunityRole? CurrentUserRole { get; set; }
    
    // Optional details
    public Guid? ChatId { get; set; }
    public int MemberCount { get; set; }
}