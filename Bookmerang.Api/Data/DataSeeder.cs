using Bookmerang.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace Bookmerang.Api.Data;

public static class DataSeeder
{
    public static async Task SeedAsync(AppDbContext db)
    {
        await SeedGenresAsync(db);
        await SeedLanguagesAsync(db);
        await db.SaveChangesAsync();
    }

    private static async Task SeedGenresAsync(AppDbContext db)
    {
        if (await db.Genres.AnyAsync()) return;

        db.Genres.AddRange(
            new Genre { Name = "Fantasía" },
            new Genre { Name = "Ciencia Ficción" },
            new Genre { Name = "Romance" },
            new Genre { Name = "Terror" },
            new Genre { Name = "Thriller" },
            new Genre { Name = "Historia" },
            new Genre { Name = "Biografía" },
            new Genre { Name = "Autoayuda" },
            new Genre { Name = "Infantil" },
            new Genre { Name = "Poesía" }
        );
    }

    private static async Task SeedLanguagesAsync(AppDbContext db)
    {
        if (await db.Languages.AnyAsync()) return;

        db.Languages.AddRange(
            new Language { LanguageName = "Español" },
            new Language { LanguageName = "English" },
            new Language { LanguageName = "Français" },
            new Language { LanguageName = "Deutsch" },
            new Language { LanguageName = "Italiano" },
            new Language { LanguageName = "Português" }
        );
    }
}