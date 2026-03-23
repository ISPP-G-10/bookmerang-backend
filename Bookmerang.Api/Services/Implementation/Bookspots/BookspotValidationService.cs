using Bookmerang.Api.Data;
using Bookmerang.Api.Exceptions;
using Bookmerang.Api.Models.DTOs.Bookspots.Requests;
using Bookmerang.Api.Models.DTOs.Bookspots.Responses;
using Bookmerang.Api.Models.Entities;
using Bookmerang.Api.Services.Interfaces.Bookspots;
using Microsoft.EntityFrameworkCore;

namespace Bookmerang.Api.Services.Implementation.Bookspots;

public class BookspotValidationService(
    IBookspotValidationRepository validationRepo,
    IBookspotRepository bookspotRepo,
    AppDbContext db
) : IBookspotValidationService
{
    public async Task<BookspotValidationDTO> CreateAsync(string supabaseId, CreateBookspotValidationRequest request, CancellationToken ct = default)
    {
        var validatingUserId = await ResolveUserIdAsync(supabaseId, ct);

        // Validamos la request
        // No comprobamos los atributos required del DTO porque no es necesario
        if (request is null || request.BookspotId <= 0)
            throw new ValidationException("El request no es válido.");

        var bookspot = await bookspotRepo.GetByIdAsync(request.BookspotId, ct);

        // Verificar si el bookspot existe
        if (bookspot is null)
            throw new NotFoundException($"No se encontró ningún bookspot con id '{request.BookspotId}'.");

        if (bookspot.Status != BookspotStatus.PENDING)
            throw new ValidationException("Solo se pueden validar bookspots que estén en estado PENDING.");

        // Evitar autoservalidación
        if (bookspot.CreatedByUserId == validatingUserId)
            throw new ValidationException("No puedes validar un bookspot creado por ti mismo.");

        var bookspotValidations = await validationRepo.GetByBookspotIdAsync(request.BookspotId, ct);

        // Evitar validaciones duplicadas
        if (bookspotValidations.Any(v => v.ValidatorUserId == validatingUserId))
            throw new ValidationException("Ya has validado este bookspot anteriormente.");

        // Creación del Objeto
        var bookspotValidation = new BookspotValidation
        {
            BookspotId = request.BookspotId,
            ValidatorUserId = validatingUserId,
            KnowsPlace = request.KnowsPlace,
            SafeForExchange = request.SafeForExchange
        };

        // Guardar en la base de datos
        var created = await validationRepo.CreateAsync(bookspotValidation, ct);

        // Procesamiento del bookspot
        // Si el user no conoce el lugar se ignora su validacion
        if (bookspotValidation.KnowsPlace)
        {
            // Cargamos todas las validaciones con la nueva validación incluida
            var allValidations = await validationRepo.GetByBookspotIdAsync(request.BookspotId, ct);

            var positives = allValidations.Count(v => v.KnowsPlace && v.SafeForExchange);
            var negatives = allValidations.Count(v => v.KnowsPlace && !v.SafeForExchange);

            if (positives >= 5)
            {
                await bookspotRepo.UpdateStatusAsync(request.BookspotId, BookspotStatus.ACTIVE, ct);
                await validationRepo.DeleteByBookspotIdAsync(request.BookspotId, ct);
            }
            else if (negatives >= 5)
            {
                await bookspotRepo.UpdateStatusAsync(request.BookspotId, BookspotStatus.REJECTED, ct);
                await validationRepo.DeleteByBookspotIdAsync(request.BookspotId, ct);
            }
        }

        return MapToDTO(created);
    }

    public async Task<List<BookspotValidationDTO>> GetByBookspotIdAsync(int bookspotId, CancellationToken ct = default)
    {
        var validations = await validationRepo.GetByBookspotIdAsync(bookspotId, ct);
        return validations.Select(MapToDTO).ToList();
    }

    public Task<BookspotValidationDTO> GetByIdAsync(int validationId, CancellationToken ct = default)
    {
        return validationRepo.GetByIdAsync(validationId, ct)
            .ContinueWith(t =>
            {
                var validation = t.Result;
                if (validation is null)
                    throw new NotFoundException($"No se encontró ninguna validación con id '{validationId}'.");

                return MapToDTO(validation);
            }, ct);
    }

    private async Task<Guid> ResolveUserIdAsync(string supabaseId, CancellationToken ct)
    {
        var userId = await db.Users
            .Where(u => u.SupabaseId == supabaseId)
            .Select(u => (Guid?)u.Id)
            .FirstOrDefaultAsync(ct);

        if (userId is null)
            throw new NotFoundException(
                $"No se encontró ningún usuario con supabaseId '{supabaseId}'.");

        return userId.Value;
    }

    private BookspotValidationDTO MapToDTO(BookspotValidation validation)
    {
        return new BookspotValidationDTO
        {
            Id = validation.Id,
            KnowsPlace = validation.KnowsPlace,
            SafeForExchange = validation.SafeForExchange,
            CreatedAt = validation.Created_at
        };
    }
}