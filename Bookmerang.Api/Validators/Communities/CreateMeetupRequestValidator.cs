using Bookmerang.Api.Models.DTOs.Communities;
using FluentValidation;

namespace Bookmerang.Api.Validators.Communities;

public class CreateMeetupRequestValidator : AbstractValidator<CreateMeetupRequest>
{
    public CreateMeetupRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty()
            .WithMessage("El título de la quedada es obligatorio.")
            .MaximumLength(120)
            .WithMessage("El título de la quedada no puede superar los 120 caracteres.");

        RuleFor(x => x.ScheduledAt)
            .Must(date => date != default)
            .WithMessage("La fecha y hora de la quedada es obligatoria.")
            .Must(date => date > DateTime.UtcNow)
            .WithMessage("La quedada debe programarse en una fecha futura.");

        RuleFor(x => x.OtherLocation)
            .Must(loc => loc == null || loc.Length == 2)
            .WithMessage("La ubicación personalizada debe incluir exactamente longitud y latitud.");
    }
}
