using FluentValidation;

namespace TelegramCrm.Application.Merchants;

/// <summary>Validates the merchant onboarding payload (contract v1.2 §4.2).</summary>
public sealed class CreateMerchantRequestValidator : AbstractValidator<CreateMerchantRequest>
{
    public CreateMerchantRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(120);

        RuleFor(x => x.Slug)
            .NotEmpty()
            .Matches("^[a-z0-9-]{3,40}$")
            .WithMessage("Slug must be 3-40 characters of lowercase letters, digits or hyphens.");

        RuleFor(x => x.DefaultLanguage)
            .Must(v => v is null || v is "fa" or "en")
            .WithMessage("DefaultLanguage must be 'fa' or 'en'.");

        RuleFor(x => x.Currency)
            .Must(v => v is null || (v.Length == 3 && v.All(char.IsAsciiLetterUpper)))
            .WithMessage("Currency must be a 3-letter ISO 4217 code.");
    }
}
