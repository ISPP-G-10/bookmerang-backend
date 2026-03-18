namespace Bookmerang.Api.Models.DTOs.Communities;

public class CreateCommunityRequest
{
    public string Name { get; set; } = string.Empty;
    public int ReferenceBookspotId { get; set; }
}