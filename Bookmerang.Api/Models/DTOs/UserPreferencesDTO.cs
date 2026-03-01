using System;
using System.ComponentModel.DataAnnotations;
using Bookmerang.Api.Models;

namespace Bookmerang.Api.Models.DTOs;

public class UserPreferenceDto
{
    public int Id { get; set; }
    public Guid UserId { get; set; }

    public GeoPointDto Location { get; set; } = null!;

    public int RadioKm { get; set; }
    public BooksExtension Extension { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class GeoPointDto
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}

public class UpsertUserPreferenceDto
{
    [Required]
    public int RadioKm { get; set; }

    [Required]
    public BooksExtension Extension { get; set; }

    [Required]
    public double Latitude { get; set; }

    [Required]
    public double Longitude { get; set; }
}