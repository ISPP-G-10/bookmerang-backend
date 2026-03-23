using Bookmerang.Api.Data;
using Bookmerang.Api.Models.Entities;
using Bookmerang.Api.Models.Enums;
using Bookmerang.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using NetTopologySuite.Geometries;
using Xunit;

namespace Bookmerang.Tests.Books.Integration;

public class BooksIntegrationTests : IClassFixture<PostgresBooksFixture>
{
    private readonly PostgresBooksFixture _fixture;

    public BooksIntegrationTests(PostgresBooksFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task PersistenciaBook_ConPostgreSql_GuardaCorrectamente()
    {
        await using var db = _fixture.CreateDbContext();

        var ownerId = Guid.NewGuid();
        await SeedUserAsync(db, ownerId, $"supabase-{Guid.NewGuid()}");

        var book = CreateValidBook(ownerId);
        db.Books.Add(book);
        await db.SaveChangesAsync();

        var exists = await db.Books.AnyAsync(x => x.Id == book.Id);
        Assert.True(exists);
    }

    [Fact]
    public async Task AsociacionesBook_GeneroEIdioma_SePersisten()
    {
        await using var db = _fixture.CreateDbContext();

        var ownerId = Guid.NewGuid();
        await SeedUserAsync(db, ownerId, $"supabase-{Guid.NewGuid()}");

        await SeedCatalogEntityAsync(db, "Genre", 9001, "Test Genre");
        await SeedCatalogEntityAsync(db, "Language", 9001, "Test Language");

        var book = CreateValidBook(ownerId);
        db.Books.Add(book);
        await db.SaveChangesAsync();

        db.Set<BookGenre>().Add(new BookGenre { BookId = book.Id, GenreId = 9001 });
        db.Set<BookLanguage>().Add(new BookLanguage { BookId = book.Id, LanguageId = 9001 });
        await db.SaveChangesAsync();

        var genresCount = await db.Set<BookGenre>().CountAsync(x => x.BookId == book.Id);
        var languagesCount = await db.Set<BookLanguage>().CountAsync(x => x.BookId == book.Id);

        Assert.Equal(1, genresCount);
        Assert.Equal(1, languagesCount);
    }

    [Fact]
    public async Task EnumBookStatus_SeMapeaEnPostgreSql_Correctamente()
    {
        await using var db = _fixture.CreateDbContext();

        var ownerId = Guid.NewGuid();
        await SeedUserAsync(db, ownerId, $"supabase-{Guid.NewGuid()}");

        var book = CreateValidBook(ownerId);
        book.Status = BookStatus.PUBLISHED;

        db.Books.Add(book);
        await db.SaveChangesAsync();

        db.ChangeTracker.Clear();

        var persisted = await db.Books.SingleAsync(x => x.Id == book.Id);
        Assert.Equal(BookStatus.PUBLISHED, persisted.Status);
    }

    private static Book CreateValidBook(Guid ownerId)
    {
        return new Book
        {
            OwnerId = ownerId,
            Isbn = "978-84-204-4607-4",
            Titulo = "Libro Test",
            Autor = "Autor Test",
            Editorial = "Editorial Test",
            NumPaginas = 100,
            Cover = Enum.GetValues<CoverType>().First(),
            Condition = Enum.GetValues<BookCondition>().First(),
            Observaciones = "Obs test",
            Status = BookStatus.DRAFT
        };
    }

    private static async Task SeedUserAsync(AppDbContext db, Guid id, string supabaseId)
    {
        // 1) Insertar base_users (si existe en el modelo)
        var baseUserType = db.Model.GetEntityTypes()
            .FirstOrDefault(e => string.Equals(e.GetTableName(), "base_users", StringComparison.OrdinalIgnoreCase));

        if (baseUserType is not null)
        {
            var baseUser = Activator.CreateInstance(baseUserType.ClrType)
                ?? throw new InvalidOperationException("No se pudo instanciar base_users.");

            SetIfExists(baseUserType, baseUser, "Id", id);
            SetIfExists(baseUserType, baseUser, "SupabaseId", supabaseId);
            SetIfExists(baseUserType, baseUser, "Email", $"{supabaseId}@test.local");
            SetIfExists(baseUserType, baseUser, "Username", $"user_{Guid.NewGuid():N}"[..12]);
            SetIfExists(baseUserType, baseUser, "Name", "Test User");
            SetIfExists(baseUserType, baseUser, "ProfilePhoto", string.Empty);
            SetIfExists(baseUserType, baseUser, "CreatedAt", DateTime.UtcNow);
            SetIfExists(baseUserType, baseUser, "UpdatedAt", DateTime.UtcNow);

            var locationProp = baseUserType.ClrType.GetProperty("Location");
            if (locationProp?.PropertyType == typeof(Point))
            {
                var point = new GeometryFactory(new PrecisionModel(), 4326)
                    .CreatePoint(new Coordinate(0, 0));
                locationProp.SetValue(baseUser, point);
            }

            FillRequiredDefaults(baseUserType, baseUser);
            db.Add(baseUser);
            await db.SaveChangesAsync();
        }

        // 2) Insertar users (si existe en el modelo), con mismo Id
        var userType = db.Model.GetEntityTypes()
            .FirstOrDefault(e => string.Equals(e.GetTableName(), "users", StringComparison.OrdinalIgnoreCase));

        if (userType is not null)
        {
            var user = Activator.CreateInstance(userType.ClrType)
                ?? throw new InvalidOperationException("No se pudo instanciar users.");

            SetIfExists(userType, user, "Id", id);
            FillRequiredDefaults(userType, user);

            db.Add(user);
            await db.SaveChangesAsync();
        }
    }

    private static void SetIfExists(IEntityType type, object entity, string propertyName, object value)
    {
        var p = type.ClrType.GetProperty(propertyName);
        if (p is not null && p.CanWrite)
            p.SetValue(entity, value);
    }

    private static async Task SeedCatalogEntityAsync(AppDbContext db, string clrTypeName, int id, string value)
    {
        var entityType = db.Model.GetEntityTypes()
            .FirstOrDefault(x => x.ClrType.Name.Equals(clrTypeName, StringComparison.OrdinalIgnoreCase));

        if (entityType is null)
            throw new InvalidOperationException($"No se encontró entidad '{clrTypeName}' en el modelo.");

        var entity = Activator.CreateInstance(entityType.ClrType)
            ?? throw new InvalidOperationException($"No se pudo instanciar '{clrTypeName}'.");

        entityType.ClrType.GetProperty("Id")?.SetValue(entity, id);
        entityType.ClrType.GetProperty("Name")?.SetValue(entity, value);
        entityType.ClrType.GetProperty("Nombre")?.SetValue(entity, value);

        FillRequiredDefaults(entityType, entity);

        db.Add(entity);
        await db.SaveChangesAsync();
    }

    private static void FillRequiredDefaults(IEntityType entityType, object entity)
    {
        foreach (var p in entityType.GetProperties())
        {
            if (p.IsNullable || p.IsPrimaryKey())
                continue;

            var clrProp = entityType.ClrType.GetProperty(p.Name);
            if (clrProp is null || !clrProp.CanWrite)
                continue;

            if (clrProp.GetValue(entity) is not null)
                continue;

            var t = clrProp.PropertyType;

            if (t == typeof(string)) clrProp.SetValue(entity, "test");
            else if (t == typeof(DateTime)) clrProp.SetValue(entity, DateTime.UtcNow);
            else if (t == typeof(Guid)) clrProp.SetValue(entity, Guid.NewGuid());
            else if (t.IsEnum) clrProp.SetValue(entity, Enum.GetValues(t).GetValue(0));
            else if (t.IsValueType) clrProp.SetValue(entity, Activator.CreateInstance(t));
        }
    }

    [Fact]
    public async Task CrudBook_CreateReadUpdateSoftDelete_FuncionaCorrectamente()
    {
        await using var db = _fixture.CreateDbContext();

        var ownerId = Guid.NewGuid();
        await SeedUserAsync(db, ownerId, $"supabase-{Guid.NewGuid()}");

        // CREATE
        var book = CreateValidBook(ownerId);
        book.Titulo = "Inicial";
        db.Books.Add(book);
        await db.SaveChangesAsync();

        // READ
        var created = await db.Books.SingleAsync(x => x.Id == book.Id);
        Assert.Equal("Inicial", created.Titulo);

        // UPDATE
        created.Titulo = "Actualizado";
        created.Status = BookStatus.PUBLISHED;
        await db.SaveChangesAsync();

        db.ChangeTracker.Clear();
        var updated = await db.Books.SingleAsync(x => x.Id == book.Id);
        Assert.Equal("Actualizado", updated.Titulo);
        Assert.Equal(BookStatus.PUBLISHED, updated.Status);

        // SOFT DELETE
        updated.Status = BookStatus.DELETED;
        await db.SaveChangesAsync();

        db.ChangeTracker.Clear();
        var visibleInLibrary = await db.Books.CountAsync(x =>
            x.OwnerId == ownerId && x.Status != BookStatus.DELETED);

        Assert.Equal(0, visibleInLibrary);
    }
}