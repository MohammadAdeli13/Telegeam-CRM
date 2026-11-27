using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http.HttpResults;

namespace TelegramCrm.Api.Endpoints;

/// <summary>
/// Bridges FluentValidation to the RFC 7807 <c>validation_failed</c> response
/// (contract v1.2 §4): a 400 with a field → messages map.
/// </summary>
internal static class Validation
{
    /// <summary>Runs the validator; returns the problem to send, or <c>null</c> when valid.</summary>
    public static async Task<ValidationProblem?> CheckAsync<T>(
        IValidator<T> validator,
        T request,
        CancellationToken cancellationToken)
    {
        var result = await validator.ValidateAsync(request, cancellationToken).ConfigureAwait(false);
        return result.IsValid ? null : ToProblem(result);
    }

    public static ValidationProblem ToProblem(ValidationResult result) =>
        TypedResults.ValidationProblem(result.ToDictionary());
}
