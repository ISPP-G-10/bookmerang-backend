using System.ComponentModel.DataAnnotations;

namespace Bookmerang.Api.Models.DTOs.Bookspots.Requests;

public class CreateBookspotValidationRequest
{
    [Required]
    public int BookspotId { get; set; }

    [Required]
    public bool KnowsPlace { get; set; }

    [Required]
    public bool SafeForExchange { get; set; }

}