using Bookmerang.Api.Models.Enums;
using Bookmerang.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Xunit;
using System.IdentityModel.Tokens.Jwt;
using Bookmerang.Api.Models.DTOs.Bookdrop;

namespace Bookmerang.Tests.Bookdrop;

public class BookdropServiceTests : IClassFixture<PostgresBookdropFixture>
{
    private readonly PostgresBookdropFixture _fixture;

    public BookdropServiceTests(PostgresBookdropFixture fixture)
    {
        _fixture = fixture;
    }

    private static Point CreateLocation(double lat = 37.3886, double lon = -5.9823)
    {
        var factory = NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
        return factory.CreatePoint(new Coordinate(lon, lat));
    }

    // ===== Login devuelve JWT con claim user_type correcto =====

    [Fact]
    public async Task Login_BookdropUser_JwtContainsUserTypeClaim()
    {
        await using var db = _fixture.CreateDbContext();
        var authService = _fixture.CreateAuthService(db);

        // Registrar bookdrop
        await authService.RegisterBusiness(
            "login_test@test.com", "Test1234", "login_test_user", "Login Test",
            null, CreateLocation(), "Local Login", "Calle Login 1");

        // Login
        var (usuario, token, error) = await authService.Login("login_test@test.com", "Test1234");

        Assert.Null(error);
        Assert.NotNull(usuario);
        Assert.NotEmpty(token);

        // Decodificar JWT y verificar claims
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        var userTypeClaim = jwt.Claims.FirstOrDefault(c => c.Type == "user_type");
        Assert.NotNull(userTypeClaim);
        Assert.Equal(((int)BaseUserType.BOOKDROP_USER).ToString(), userTypeClaim.Value);

        var userIdClaim = jwt.Claims.FirstOrDefault(c => c.Type == "user_id");
        Assert.NotNull(userIdClaim);
        Assert.Equal(usuario.Id.ToString(), userIdClaim.Value);
    }

    // ===== Obtener perfil del establecimiento =====

    [Fact]
    public async Task GetPerfil_ExistingBookdrop_ReturnsCorrectData()
    {
        await using var db = _fixture.CreateDbContext();
        var authService = _fixture.CreateAuthService(db);
        var bookdropService = _fixture.CreateBookdropService(db);

        // Registrar bookdrop
        var (usuario, _, _) = await authService.RegisterBusiness(
            "perfil_test@test.com", "Test1234", "perfil_test_user", "Perfil Test",
            "foto.jpg", CreateLocation(40.4168, -3.7038), "Librería Madrid", "Gran Vía 1, Madrid");

        // Obtener perfil
        var perfil = await bookdropService.GetPerfil(usuario!.SupabaseId);

        Assert.NotNull(perfil);
        Assert.Equal(usuario.Id, perfil.Id);
        Assert.Equal("perfil_test@test.com", perfil.Email);
        Assert.Equal("perfil_test_user", perfil.Username);
        Assert.Equal("Perfil Test", perfil.Name);
        Assert.Equal("foto.jpg", perfil.ProfilePhoto);
        Assert.Equal("Librería Madrid", perfil.NombreEstablecimiento);
        Assert.Equal("Gran Vía 1, Madrid", perfil.AddressText);
        Assert.Equal(40.4168, perfil.Latitud, 4);
        Assert.Equal(-3.7038, perfil.Longitud, 4);
        Assert.Equal(BookspotStatus.ACTIVE, perfil.BookspotStatus);
    }

    [Fact]
    public async Task GetPerfil_NonExistentUser_ReturnsNull()
    {
        await using var db = _fixture.CreateDbContext();
        var bookdropService = _fixture.CreateBookdropService(db);

        var perfil = await bookdropService.GetPerfil("no-existe-supabase-id");

        Assert.Null(perfil);
    }

    // ===== Registro flujo completo exitoso =====

    [Fact]
    public async Task RegisterBusiness_Success_CreatesAllEntities()
    {
        await using var db = _fixture.CreateDbContext();
        var service = _fixture.CreateAuthService(db);

        var (usuario, yaExistia, error) = await service.RegisterBusiness(
            "prueba1@test.com", "Test1234", "prueba1_bookdrop", "Juan García",
            null, CreateLocation(), "Cafetería Central", "Calle Sierpes 42, Sevilla");

        Assert.Null(error);
        Assert.False(yaExistia);
        Assert.NotNull(usuario);
        Assert.Equal(BaseUserType.BOOKDROP_USER, usuario.UserType);
        Assert.NotEmpty(usuario.PasswordHash!);

        var bookdropUser = await db.BookdropUsers.FindAsync(usuario.Id);
        Assert.NotNull(bookdropUser);

        var bookspot = await db.Bookspots.FindAsync(bookdropUser.BookSpotId);
        Assert.NotNull(bookspot);
        Assert.True(bookspot.IsBookdrop);
        Assert.Equal(BookspotStatus.ACTIVE, bookspot.Status);
        Assert.Equal("Cafetería Central", bookspot.Nombre);
        Assert.Equal(usuario.Id, bookspot.OwnerId);
    }

    // ===== Email duplicado =====

    [Fact]
    public async Task RegisterBusiness_DuplicateEmail_ReturnsError()
    {
        await using var db = _fixture.CreateDbContext();
        var service = _fixture.CreateAuthService(db);

        await service.RegisterBusiness(
            "duplicado_email@test.com", "Test1234", "original_user", "Original",
            null, CreateLocation(), "Local 1", "Calle 1, Sevilla");

        var (usuario, yaExistia, error) = await service.RegisterBusiness(
            "duplicado_email@test.com", "Test1234", "otro_user", "Otro",
            null, CreateLocation(), "Local 2", "Calle 2, Sevilla");

        Assert.Null(usuario);
        Assert.True(yaExistia);
        Assert.Equal("El email ya está registrado.", error);
    }

    // ===== Username duplicado =====

    [Fact]
    public async Task RegisterBusiness_DuplicateUsername_ReturnsError()
    {
        await using var db = _fixture.CreateDbContext();
        var service = _fixture.CreateAuthService(db);

        await service.RegisterBusiness(
            "primero_usr@test.com", "Test1234", "mismo_username_bd", "Primero",
            null, CreateLocation(), "Local 1", "Calle 1, Sevilla");

        var (usuario, yaExistia, error) = await service.RegisterBusiness(
            "segundo_usr@test.com", "Test1234", "mismo_username_bd", "Segundo",
            null, CreateLocation(), "Local 2", "Calle 2, Sevilla");

        Assert.Null(usuario);
        Assert.False(yaExistia);
        Assert.Equal("El nombre de usuario ya está en uso.", error);
    }

    // ===== Validación de campos obligatorios =====

    [Theory]
    [InlineData("", "Test1234", "user", "Name", "Local", "Calle 1", "El email es obligatorio.")]
    [InlineData("a@b.com", "123", "user", "Name", "Local", "Calle 1", "La contraseña debe tener al menos 6 caracteres.")]
    [InlineData("a@b.com", "Test1234", "user", "Name", "", "Calle 1", "El nombre del establecimiento es obligatorio.")]
    [InlineData("a@b.com", "Test1234", "user", "Name", "Local", "", "La dirección del establecimiento es obligatoria.")]
    public async Task RegisterBusiness_InvalidFields_ReturnsValidationError(
        string email, string password, string username, string name,
        string nombreEstablecimiento, string addressText, string expectedError)
    {
        await using var db = _fixture.CreateDbContext();
        var service = _fixture.CreateAuthService(db);

        var (usuario, _, error) = await service.RegisterBusiness(
            email, password, username, name,
            null, CreateLocation(), nombreEstablecimiento, addressText);

        Assert.Null(usuario);
        Assert.Equal(expectedError, error);
    }

    // ===== Transacción - rollback si falla a mitad =====

    [Fact]
    public async Task RegisterBusiness_TransactionRollback_NoDanglingData()
    {
        await using var db = _fixture.CreateDbContext();
        var service = _fixture.CreateAuthService(db);

        await service.RegisterBusiness(
            "existente_tx@test.com", "Test1234", "existente_tx", "Existente",
            null, CreateLocation(), "Local Existente", "Calle Existente 1");

        var countBaseUsersBefore = db.Users.Count();
        var countBookspotsBefore = db.Bookspots.Count();
        var countBookdropsBefore = db.BookdropUsers.Count();

        var (usuario, _, error) = await service.RegisterBusiness(
            "nuevo_tx@test.com", "Test1234", "existente_tx", "Nuevo",
            null, CreateLocation(), "Local Nuevo", "Calle Nueva 1");

        Assert.Null(usuario);
        Assert.NotNull(error);

        Assert.Equal(countBaseUsersBefore, db.Users.Count());
        Assert.Equal(countBookspotsBefore, db.Bookspots.Count());
        Assert.Equal(countBookdropsBefore, db.BookdropUsers.Count());
    }

    // ===== Actualización parcial del perfil =====

    [Fact]
    public async Task UpdatePerfil_PartialUpdate_OnlyChangesProvidedFields()
    {
        await using var db = _fixture.CreateDbContext();
        var authService = _fixture.CreateAuthService(db);
        var bookdropService = _fixture.CreateBookdropService(db);

        var (usuario, _, _) = await authService.RegisterBusiness(
            "partial_update@test.com", "Test1234", "partial_update", "Original Name",
            "original.jpg", CreateLocation(), "Local Original", "Calle Original 1, Sevilla");

        var request = new UpdateBookdropProfileRequest
        {
            NombreEstablecimiento = "Local Renovado"
        };

        var perfil = await bookdropService.UpdatePerfil(usuario!.SupabaseId, request);

        Assert.NotNull(perfil);
        Assert.Equal("Local Renovado", perfil.NombreEstablecimiento);
        Assert.Equal("Calle Original 1, Sevilla", perfil.AddressText);
        Assert.Equal("original.jpg", perfil.ProfilePhoto);
        Assert.Equal(37.3886, perfil.Latitud, 4);
        Assert.Equal(-5.9823, perfil.Longitud, 4);
    }

    // ===== Actualización con coordenadas =====

    [Fact]
    public async Task UpdatePerfil_WithCoordinates_UpdatesBothBookspotAndBaseUser()
    {
        await using var db = _fixture.CreateDbContext();
        var authService = _fixture.CreateAuthService(db);
        var bookdropService = _fixture.CreateBookdropService(db);

        var (usuario, _, _) = await authService.RegisterBusiness(
            "coords_update@test.com", "Test1234", "coords_update", "Coords Test",
            null, CreateLocation(), "Local Coords", "Calle Coords 1");

        var request = new UpdateBookdropProfileRequest
        {
            Latitud = 40.4168,
            Longitud = -3.7038
        };

        var perfil = await bookdropService.UpdatePerfil(usuario!.SupabaseId, request);

        Assert.NotNull(perfil);
        Assert.Equal(40.4168, perfil.Latitud, 4);
        Assert.Equal(-3.7038, perfil.Longitud, 4);

        var baseUser = await db.Users.FirstAsync(u => u.Id == usuario.Id);
        Assert.Equal(40.4168, baseUser.Location.Y, 4);
        Assert.Equal(-3.7038, baseUser.Location.X, 4);
    }

    // ===== Listar todos los bookdrops =====

    [Fact]
    public async Task GetAll_MultipleBookdrops_ReturnsAll()
    {
        await using var db = _fixture.CreateDbContext();
        var authService = _fixture.CreateAuthService(db);
        var bookdropService = _fixture.CreateBookdropService(db);

        var countBefore = (await bookdropService.GetAll()).Count;

        await authService.RegisterBusiness(
            "list_a@test.com", "Test1234", "list_a", "List A",
            null, CreateLocation(), "Local A", "Calle A");

        await authService.RegisterBusiness(
            "list_b@test.com", "Test1234", "list_b", "List B",
            null, CreateLocation(), "Local B", "Calle B");

        var all = await bookdropService.GetAll();

        Assert.Equal(countBefore + 2, all.Count);
        Assert.Contains(all, b => b.NombreEstablecimiento == "Local A");
        Assert.Contains(all, b => b.NombreEstablecimiento == "Local B");
    }

    // ===== Eliminar bookdrop — cascada completa =====

    [Fact]
    public async Task DeleteBookdrop_RemovesAllRelatedEntities()
    {
        await using var db = _fixture.CreateDbContext();
        var authService = _fixture.CreateAuthService(db);
        var bookdropService = _fixture.CreateBookdropService(db);

        var (usuario, _, _) = await authService.RegisterBusiness(
            "delete_test@test.com", "Test1234", "delete_test", "Delete Test",
            null, CreateLocation(), "Local Delete", "Calle Delete 1");

        var userId = usuario!.Id;
        var bookdropUser = await db.BookdropUsers.FindAsync(userId);
        var bookspotId = bookdropUser!.BookSpotId;

        var (found, error) = await bookdropService.DeleteBookdrop(userId);

        Assert.True(found);
        Assert.Null(error);

        Assert.Null(await db.Users.FindAsync(userId));
        Assert.Null(await db.BookdropUsers.FindAsync(userId));
        Assert.Null(await db.Bookspots.FindAsync(bookspotId));
    }

    // ===== Eliminar bookdrop inexistente =====

    [Fact]
    public async Task DeleteBookdrop_NonExistent_ReturnsNotFound()
    {
        await using var db = _fixture.CreateDbContext();
        var bookdropService = _fixture.CreateBookdropService(db);

        var (found, error) = await bookdropService.DeleteBookdrop(Guid.NewGuid());

        Assert.False(found);
        Assert.Null(error);
    }
}
