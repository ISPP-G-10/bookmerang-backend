namespace Bookmerang.Api.Models.DTOs;

/// <summary>
/// Wrapper de paginación para el feed del matcher.
/// </summary>
public class FeedResultDto
{
    public required List<FeedBookDto> Items { get; set; }
    public required int Page { get; set; }
    public required int PageSize { get; set; }
    public required bool HasMore { get; set; }
}
