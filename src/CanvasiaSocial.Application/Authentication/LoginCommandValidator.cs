using FluentValidation;

namespace CanvasiaSocial.Application.Authentication;

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("E-posta adresi zorunludur.")
            .EmailAddress().WithMessage("Geçerli bir e-posta adresi girin.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Parola zorunludur.");
    }
}
