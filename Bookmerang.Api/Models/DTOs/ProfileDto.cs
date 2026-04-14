using System;

namespace Bookmerang.Api.Models.DTOs;

public class ProfileDto
{
    public Guid Id { get; set; }
    public string SupabaseId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Avatar { get; set; } = string.Empty; // Maps to ProfilePhoto in DB
    public double Latitud { get; set; }
    public double Longitud { get; set; }

    // Gamification Stats
    public int Level { get; set; }
    public string Tier { get; set; } = string.Empty;
    public int MonthlyInkDrops { get; set; }
    public int DaysUntilReset { get; set; }
    public int InksToNextLevel { get; set; }
    public double Progress { get; set; }
    public int Streak { get; set; }
    public int Bonus { get; set; }
    public string? ActiveFrameId { get; set; }
    public string? ActiveColorId { get; set; }
}
