using System.ComponentModel.DataAnnotations;

namespace Bookmerang.Api.Models.DTOs.Bookspots.Requests;

public class UpdateBookspotNameRequest
{
    [Required]
    [StringLength(100, MinimumLength = 3)]
    public string Nombre { get; set; } = string.Empty;
}
