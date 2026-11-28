using FluentValidation;
using TelegramCrm.Domain.Enums;

namespace TelegramCrm.Application.Orders;

/// <summary>Validates <c>PATCH /merchant/orders/{id}/status</c>.</summary>
public sealed class UpdateOrderStatusRequestValidator : AbstractValidator<UpdateOrderStatusRequest>
{
    public UpdateOrderStatusRequestValidator()
    {
        RuleFor(x => x.Status)
            .NotEmpty()
            .Must(s => Enum.TryParse<OrderStatus>(s, ignoreCase: true, out _))
            .WithMessage("Status must be a valid OrderStatus value.");
        RuleFor(x => x.Reason).MaximumLength(500);
        RuleFor(x => x.CarrierName).MaximumLength(80);
        RuleFor(x => x.TrackingCode).MaximumLength(100);
    }
}

/// <summary>Validates <c>POST /merchant/orders/{id}/payment-decision</c>.</summary>
public sealed class PaymentDecisionRequestValidator : AbstractValidator<PaymentDecisionRequest>
{
    public PaymentDecisionRequestValidator()
    {
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}

/// <summary>Validates <c>POST /storefront/{shopSlug}/orders/{id}/cancel</c>.</summary>
public sealed class CancelOrderRequestValidator : AbstractValidator<CancelOrderRequest>
{
    public CancelOrderRequestValidator()
    {
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}
