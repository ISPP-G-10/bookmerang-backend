using Bookmerang.Api.Data;
using Bookmerang.Api.Exceptions;
using Bookmerang.Api.Models.DTOs.Books.Requests;
using Bookmerang.Api.Models.Entities;
using Bookmerang.Api.Models.Enums;
using Bookmerang.Api.Services.Implementation.Books;
using Bookmerang.Api.Services.Interfaces.Books;
using Bookmerang.Api.Models.DTOs.Books.Queries;
using Bookmerang.Tests.Helpers;
using Moq;
using NetTopologySuite.Geometries;
using Xunit;

namespace Bookmerang.Tests.Books;

public class BookServiceTests : IAsyncLifetime
{
    private AppDbContext _db = null!;

    public Task InitializeAsync()
    {
        _db = DbContextFactory.CreateInMemory();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _db.Database.EnsureDeletedAsync();
        await _db.DisposeAsync();
    }

    private static Point MakePoint(double lon, double lat) =>
        new GeometryFactory(new PrecisionModel(), 4326).CreatePoint(new Coordinate(lon, lat));

    private void SeedUser(Guid id, string supabaseId, string email = "user@test.com")
    {
        var baseUser = new BaseUser
        {
            Id = id,
            SupabaseId = supabaseId,
            Email = email,
            Username = email.Split('@')[0],
            Name = email.Split('@')[0],
            ProfilePhoto = string.Empty,
            UserType = BaseUserType.USER,
            Location = MakePoint(0, 0),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Users.Add(baseUser);
    }

    private BookService CreateSut(
        Mock<IBookRepository> bookRepo,
        Mock<IGenreRepository> genreRepo,
        Mock<ILanguageRepository> languageRepo)
    {
        return new BookService(bookRepo.Object, genreRepo.Object, languageRepo.Object, _db);
    }

    [Fact]
    public async Task CreateDraftAsync_UsuarioNoExiste_LanzaNotFoundException()
    {
        var bookRepo = new Mock<IBookRepository>();
        var genreRepo = new Mock<IGenreRepository>();
        var languageRepo = new Mock<ILanguageRepository>();
        var sut = CreateSut(bookRepo, genreRepo, languageRepo);

        var request = new CreateBookDraftRequest
        {
            GenreIds = new List<int>(),
            LanguageIds = new List<int>()
        };

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.CreateDraftAsync("supabase-missing", request, CancellationToken.None));
    }

    [Fact]
    public async Task CreateDraftAsync_GenerosInvalidos_LanzaValidationException()
    {
        var ownerId = Guid.NewGuid();
        SeedUser(ownerId, "supabase-ok");
        await _db.SaveChangesAsync();

        var bookRepo = new Mock<IBookRepository>();
        var genreRepo = new Mock<IGenreRepository>();
        var languageRepo = new Mock<ILanguageRepository>();

        genreRepo.Setup(x => x.AllExistAsync(It.IsAny<List<int>>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(false);

        var sut = CreateSut(bookRepo, genreRepo, languageRepo);

        var request = new CreateBookDraftRequest
        {
            GenreIds = new List<int> { 999 },
            LanguageIds = new List<int>()
        };

        await Assert.ThrowsAsync<ValidationException>(() =>
            sut.CreateDraftAsync("supabase-ok", request, CancellationToken.None));
    }

    [Fact]
    public async Task PublishAsync_DraftIncompleto_LanzaValidationException()
    {
        var ownerId = Guid.NewGuid();
        SeedUser(ownerId, "supabase-ok");
        await _db.SaveChangesAsync();

        var draft = new Book
        {
            Id = 10,
            OwnerId = ownerId,
            Status = BookStatus.DRAFT,
            Isbn = null,
            Titulo = null,
            Autor = null,
            Cover = null,
            NumPaginas = null,
            Condition = null,
            Photos = new List<BookPhoto>(),
            BookGenres = new List<BookGenre>(),
            BookLanguages = new List<BookLanguage>()
        };

        var bookRepo = new Mock<IBookRepository>();
        var genreRepo = new Mock<IGenreRepository>();
        var languageRepo = new Mock<ILanguageRepository>();

        bookRepo.Setup(x => x.GetByIdAsync(10, It.IsAny<CancellationToken>()))
                .ReturnsAsync(draft);

        var sut = CreateSut(bookRepo, genreRepo, languageRepo);

        await Assert.ThrowsAsync<ValidationException>(() =>
            sut.PublishAsync(10, "supabase-ok", CancellationToken.None));
    }

    [Fact]
    public async Task DeleteAsync_LibroDeOtroUsuario_LanzaForbiddenException()
    {
        var ownerId = Guid.NewGuid();
        SeedUser(ownerId, "supabase-ok");
        await _db.SaveChangesAsync();

        var book = new Book
        {
            Id = 20,
            OwnerId = Guid.NewGuid(),
            Status = BookStatus.DRAFT,
            Photos = new List<BookPhoto>(),
            BookGenres = new List<BookGenre>(),
            BookLanguages = new List<BookLanguage>()
        };

        var bookRepo = new Mock<IBookRepository>();
        var genreRepo = new Mock<IGenreRepository>();
        var languageRepo = new Mock<ILanguageRepository>();

        bookRepo.Setup(x => x.GetByIdAsync(20, It.IsAny<CancellationToken>()))
                .ReturnsAsync(book);

        var sut = CreateSut(bookRepo, genreRepo, languageRepo);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            sut.DeleteAsync(20, "supabase-ok", CancellationToken.None));
    }

    [Fact]
    public async Task DeleteAsync_LibroPropio_MarcaDeletedYActualiza()
    {
        var ownerId = Guid.NewGuid();
        SeedUser(ownerId, "supabase-ok");
        await _db.SaveChangesAsync();

        var book = new Book
        {
            Id = 30,
            OwnerId = ownerId,
            Status = BookStatus.DRAFT,
            Photos = new List<BookPhoto>(),
            BookGenres = new List<BookGenre>(),
            BookLanguages = new List<BookLanguage>()
        };

        var bookRepo = new Mock<IBookRepository>();
        var genreRepo = new Mock<IGenreRepository>();
        var languageRepo = new Mock<ILanguageRepository>();

        bookRepo.Setup(x => x.GetByIdAsync(30, It.IsAny<CancellationToken>()))
                .ReturnsAsync(book);

        var sut = CreateSut(bookRepo, genreRepo, languageRepo);

        await sut.DeleteAsync(30, "supabase-ok", CancellationToken.None);

        Assert.Equal(BookStatus.DELETED, book.Status);
        bookRepo.Verify(x => x.UpdateAsync(book, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetMyLibraryAsync_DevuelveResultadoPaginadoCorrecto()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        SeedUser(ownerId, "supabase-ok");
        await _db.SaveChangesAsync();

        var query = new LibraryQuery
        {
            Page = 2,
            PageSize = 5,
            Search = "tolkien",
            Status = BookStatus.PUBLISHED
        };

        var repoItems = new List<Book>
        {
            new()
            {
                Id = 101,
                OwnerId = ownerId,
                Titulo = "El Hobbit",
                Autor = "Tolkien",
                Status = BookStatus.PUBLISHED,
                Photos = new List<BookPhoto> { new() { Url = "https://img/1.jpg", Orden = 0 } },
                BookGenres = new List<BookGenre>(),
                BookLanguages = new List<BookLanguage>()
            },
            new()
            {
                Id = 102,
                OwnerId = ownerId,
                Titulo = "LOTR",
                Autor = "Tolkien",
                Status = BookStatus.PUBLISHED,
                Photos = new List<BookPhoto>(),
                BookGenres = new List<BookGenre>(),
                BookLanguages = new List<BookLanguage>()
            }
        };

        var bookRepo = new Mock<IBookRepository>();
        var genreRepo = new Mock<IGenreRepository>();
        var languageRepo = new Mock<ILanguageRepository>();

        bookRepo.Setup(x => x.GetByOwnerPagedAsync(ownerId, query, It.IsAny<CancellationToken>()))
                .ReturnsAsync((repoItems, 12));

        var sut = CreateSut(bookRepo, genreRepo, languageRepo);

        // Act
        var result = await sut.GetMyLibraryAsync("supabase-ok", query, CancellationToken.None);

        // Assert
        Assert.Equal(2, result.Page);
        Assert.Equal(5, result.PageSize);
        Assert.Equal(12, result.Total);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(101, result.Items[0].Id);

        bookRepo.Verify(x => x.GetByOwnerPagedAsync(ownerId, query, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetMyDraftsAsync_DevuelveResultadoPaginadoCorrecto()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        SeedUser(ownerId, "supabase-ok");
        await _db.SaveChangesAsync();

        var query = new DraftsQuery
        {
            Page = 1,
            PageSize = 10,
            Search = "borrador"
        };

        var repoItems = new List<Book>
        {
            new()
            {
                Id = 201,
                OwnerId = ownerId,
                Titulo = "Borrador 1",
                Autor = "Autor",
                Status = BookStatus.DRAFT,
                Photos = new List<BookPhoto>(),
                BookGenres = new List<BookGenre>(),
                BookLanguages = new List<BookLanguage>()
            }
        };

        var bookRepo = new Mock<IBookRepository>();
        var genreRepo = new Mock<IGenreRepository>();
        var languageRepo = new Mock<ILanguageRepository>();

        bookRepo.Setup(x => x.GetDraftsPagedAsync(ownerId, query, It.IsAny<CancellationToken>()))
                .ReturnsAsync((repoItems, 1));

        var sut = CreateSut(bookRepo, genreRepo, languageRepo);

        // Act
        var result = await sut.GetMyDraftsAsync("supabase-ok", query, CancellationToken.None);

        // Assert
        Assert.Equal(1, result.Page);
        Assert.Equal(10, result.PageSize);
        Assert.Equal(1, result.Total);
        Assert.Single(result.Items);
        Assert.Equal(BookStatus.DRAFT, result.Items[0].Status);

        bookRepo.Verify(x => x.GetDraftsPagedAsync(ownerId, query, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PublishAsync_DrafCompleto()
    {
        var ownerId = Guid.NewGuid();
        SeedUser(ownerId, "supabase-ok");
        await _db.SaveChangesAsync();

        var draft = new Book
        {
            Id = 10,
            OwnerId = ownerId,
            Status = BookStatus.DRAFT,
            Isbn = "ISBN 978-84-123456-7-0",
            Titulo = "Titulo de prueba",
            Autor = "Autor de prueba",
            Editorial = "Editorial de prueba",
            Cover = CoverType.HARDCOVER,
            NumPaginas = 333,
            Condition = BookCondition.LIKE_NEW,
            Observaciones = "Observaciones de prueba",
            Photos = new List<BookPhoto> { new() { Url = "https://img/1.jpg", Orden = 0 } },
            BookGenres = new List<BookGenre> { new() { Book=null, BookId= 10, GenreId = 1, Genre = new Genre { Id = 1, Name = "Ficción" } } },
            BookLanguages = new List<BookLanguage> { new() { Book = null, BookId = 10, LanguageId = 1, Language = new Language { Id = 1, LanguageName = "Español" } } }
        };

        var bookRepo = new Mock<IBookRepository>();
        var genreRepo = new Mock<IGenreRepository>();
        var languageRepo = new Mock<ILanguageRepository>();

        bookRepo.Setup(x => x.GetByIdAsync(10, It.IsAny<CancellationToken>()))
                .ReturnsAsync(draft);

        var sut = CreateSut(bookRepo, genreRepo, languageRepo);
        
        var result = await sut.PublishAsync(10, "supabase-ok", CancellationToken.None);

        Assert.Equal(BookStatus.PUBLISHED, draft.Status);
        Assert.Equal("ISBN 978-84-123456-7-0", draft.Isbn);
        Assert.True(sut.GetByIdAsync(10, "supabase-ok", CancellationToken.None) != null);
    }
}