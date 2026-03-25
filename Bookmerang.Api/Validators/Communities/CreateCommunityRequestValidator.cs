using Bookmerang.Api.Models.DTOs.Communities;
using FluentValidation;

namespace Bookmerang.Api.Validators.Communities;

public class CreateCommunityRequestValidator : AbstractValidator<CreateCommunityRequest>
{
    public CreateCommunityRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("El nombre de la comunidad es obligatorio.")
            .MinimumLength(3)
            .WithMessage("El nombre de la comunidad debe tener al menos 3 caracteres.")
            .MaximumLength(80)
            .WithMessage("El nombre de la comunidad no puede superar los 80 caracteres.");

        RuleFor(x => x.ReferenceBookspotId)
            .GreaterThan(0)
            .WithMessage("El BookSpot de referencia es obligatorio.");
    }
}
