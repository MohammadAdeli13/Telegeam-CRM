using FluentValidation;

namespace TelegramCrm.Application.Storefront;

/// <summary>Validates <c>POST /storefront/{shopSlug}/cart/validate</c>.</summary>
public sealed class ValidateCartRequestValidator : AbstractValidator<ValidateCartRequest>
{
    public ValidateCartRequestValidator()
    {
        RuleFor(x => x.Items)
            .NotNull()
            .NotEmpty()
            .Must(items => items.Count <= 50)
            .WithMessage("Cart may contain at most 50 line items.");

        RuleForEach(x => x.Items).ChildRules(line =>
        {
            line.RuleFor(l => l.ProductId).NotEmpty();
            line.RuleFor(l => l.Quantity).GreaterThan(0).LessThanOrEqualTo(1000);
        });
    }
}

/// <summary>Validates <c>POST /storefront/{shopSlug}/orders</c>.</summary>
public sealed class PlaceOrderRequestValidator : AbstractValidator<PlaceOrderRequest>
{
    public PlaceOrderRequestValidator()
    {
        RuleFor(x => x.Items)
            .NotNull()
            .NotEmpty()
            .Must(items => items.Count <= 50)
            .WithMessage("An order may contain at most 50 line items.");

        RuleForEach(x => x.Items).ChildRules(line =>
        {
            line.RuleFor(l => l.ProductId).NotEmpty();
            line.RuleFor(l => l.Quantity).GreaterThan(0).LessThanOrEqualTo(1000);
        });

        RuleFor(x => x.Note).MaximumLength(2000);
        RuleFor(x => x.Phone)
            .MaximumLength(20)
            .Matches(@"^\+[1-9]\d{6,14}$")
            .WithMessage("Phone must be E.164 (e.g. +989121234567).")
            .When(x => !string.IsNullOrWhiteSpace(x.Phone));
    }
}
