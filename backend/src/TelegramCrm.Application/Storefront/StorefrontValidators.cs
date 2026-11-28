using FluentValidation;
using TelegramCrm.Domain.Enums;

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

        RuleFor(x => x.PaymentMethod)
            .Must(v => Enum.TryParse<PaymentMethod>(v, ignoreCase: true, out _))
            .WithMessage("PaymentMethod must be None, PayOnDelivery, or CardToCard.")
            .When(x => !string.IsNullOrWhiteSpace(x.PaymentMethod));
    }
}

/// <summary>Validates <c>POST /storefront/{shopSlug}/addresses</c>.</summary>
public sealed class CreateCustomerAddressRequestValidator : AbstractValidator<CreateCustomerAddressRequest>
{
    public CreateCustomerAddressRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(60);
        RuleFor(x => x.RecipientName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Phone)
            .NotEmpty()
            .MaximumLength(20)
            .Matches(@"^\+[1-9]\d{6,14}$")
            .WithMessage("Phone must be E.164 (e.g. +989121234567).");
        RuleFor(x => x.Province).NotEmpty().MaximumLength(80);
        RuleFor(x => x.City).NotEmpty().MaximumLength(80);
        RuleFor(x => x.AddressLine).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Plaque).MaximumLength(20);
        RuleFor(x => x.Unit).MaximumLength(20);
        RuleFor(x => x.PostalCode).MaximumLength(20);
    }
}

/// <summary>Validates <c>PATCH /storefront/{shopSlug}/addresses/{id}</c>.</summary>
public sealed class UpdateCustomerAddressRequestValidator : AbstractValidator<UpdateCustomerAddressRequest>
{
    public UpdateCustomerAddressRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(60).When(x => x.Title is not null);
        RuleFor(x => x.RecipientName).NotEmpty().MaximumLength(150).When(x => x.RecipientName is not null);
        RuleFor(x => x.Phone)
            .MaximumLength(20)
            .Matches(@"^\+[1-9]\d{6,14}$")
            .WithMessage("Phone must be E.164 (e.g. +989121234567).")
            .When(x => x.Phone is not null);
        RuleFor(x => x.Province).NotEmpty().MaximumLength(80).When(x => x.Province is not null);
        RuleFor(x => x.City).NotEmpty().MaximumLength(80).When(x => x.City is not null);
        RuleFor(x => x.AddressLine).NotEmpty().MaximumLength(500).When(x => x.AddressLine is not null);
        RuleFor(x => x.Plaque).MaximumLength(20);
        RuleFor(x => x.Unit).MaximumLength(20);
        RuleFor(x => x.PostalCode).MaximumLength(20);
    }
}
