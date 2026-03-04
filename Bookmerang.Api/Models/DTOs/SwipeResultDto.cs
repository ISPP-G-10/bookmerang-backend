namespace Bookmerang.Api.Models.DTOs;

// Lo que devuelve el endpoint de swipe -> resultado del swipe (swipe grabado, match creado, o libro no disponible por match ya aceptado) y datos del match si se ha creado
public class SwipeResultDto
{
    public required SwipeOutcome Outcome { get; set; }
    public MatchCreatedDto? Match { get; set; }
}

public enum SwipeOutcome
{
    Recorded,
    MatchCreated,
    BookUnavailable
}
