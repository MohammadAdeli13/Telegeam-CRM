using TelegramCrm.Domain.Enums;

namespace TelegramCrm.Domain.Exceptions;

/// <summary>
/// A business-rule violation. <see cref="Code"/> is surfaced verbatim as the
/// machine-readable <c>code</c> member of the RFC 7807 ProblemDetails response.
/// </summary>
public class DomainException : Exception
{
    public DomainException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public DomainException(string code, string message, Exception innerException)
        : base(message, innerException)
    {
        Code = code;
    }

    public string Code { get; }
}

/// <summary>Raised when an order transition is not one of the legal edges.</summary>
public sealed class InvalidOrderStatusTransitionException : DomainException
{
    public InvalidOrderStatusTransitionException(OrderStatus from, OrderStatus to)
        : base("invalid_status_transition", $"Order cannot transition from '{from}' to '{to}'.")
    {
        From = from;
        To = to;
    }

    public OrderStatus From { get; }

    public OrderStatus To { get; }
}
