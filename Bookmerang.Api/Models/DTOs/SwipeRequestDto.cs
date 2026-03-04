using Bookmerang.Api.Models.Enums;

namespace Bookmerang.Api.Models.DTOs;

// Lo que recibe el endpoint /swipe -> el libro y la dirección del swipe
public class SwipeRequestDto
{
    public required int BookId { get; set; }
    public required SwipeDirection Direction { get; set; }
}
