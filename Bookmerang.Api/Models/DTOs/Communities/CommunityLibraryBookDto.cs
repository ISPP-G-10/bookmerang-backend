namespace Bookmerang.Api.Models.DTOs.Communities;

public class CommunityLibraryBookDto
{
    public int BookId { get; set; }
    public Guid OwnerId { get; set; }
    public string OwnerUsername { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public string Autor { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public List<string> Genres { get; set; } = [];
    public int LikesCount { get; set; }
    public bool LikedByMe { get; set; }
}