using Microsoft.AspNetCore.Mvc;
using TelegramCrm.Domain.Exceptions;
using TelegramCrm.Infrastructure.Persistence;

namespace TelegramCrm.Api.Infrastructure;

/// <summary>
/// Translates domain and infrastructure exceptions into RFC 7807 ProblemDetails with a
/// machine-readable <c>code</c> (contract v1.2 §4). Cross-tenant access is never a 403:
/// the query filter simply returns nothing, so the caller sees 404.
/// </summary>
public sealed partial class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await WriteProblemAsync(context, ex);
        }
    }

    private async Task WriteProblemAsync(HttpContext context, Exception exception)
    {
        if (context.Response.HasStarted)
        {
            LogResponseAlreadyStarted(exception);
            return;
        }

        var (status, code, title, extensions) = Map(exception);

        if (status >= StatusCodes.Status500InternalServerError)
        {
            LogUnhandledException(exception, code);
        }
        else
        {
            LogRequestFailed(code, status);
        }

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = exception.Message,
            Instance = context.Request.Path,
        };

        problem.Extensions["code"] = code;
        foreach (var (key, value) in extensions)
        {
            problem.Extensions[key] = value;
        }

        context.Response.Clear();
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(problem);
    }

    private static (int Status, string Code, string Title, Dictionary<string, object?> Extensions) Map(
        Exception exception) => exception switch
        {
            InvalidOrderStatusTransitionException invalid => (
                StatusCodes.Status409Conflict,
                invalid.Code,
                "Invalid order status transition.",
                new Dictionary<string, object?> { ["currentStatus"] = invalid.From.ToString() }),

            NotFoundException notFound => (
                StatusCodes.Status404NotFound,
                notFound.Code,
                "Resource not found.",
                new Dictionary<string, object?>()),

            DomainException domain => (
                StatusFor(domain.Code),
                domain.Code,
                "Request could not be completed.",
                new Dictionary<string, object?>()),

            TenantIsolationException => (
                StatusCodes.Status403Forbidden,
                "tenant_isolation_violation",
                "Tenant isolation violation.",
                new Dictionary<string, object?>()),

            UnauthorizedAccessException => (
                StatusCodes.Status403Forbidden,
                "forbidden",
                "Forbidden.",
                new Dictionary<string, object?>()),

            _ => (
                StatusCodes.Status500InternalServerError,
                "internal_error",
                "An unexpected error occurred.",
                new Dictionary<string, object?>()),
        };

    private static int StatusFor(string code) => code switch
    {
        "idempotency_key_required" or "invalid_status_filter" or "invalid_status"
            => StatusCodes.Status400BadRequest,

        "init_data_missing" or "init_data_malformed" or "init_data_hash_missing" or
        "init_data_invalid_hash" or "init_data_auth_date_missing" or "init_data_expired" or
        "init_data_auth_date_in_future" or "webhook_secret_invalid" or
        "init_data_user_missing" or "init_data_user_malformed" or "bot_token_not_configured"
            => StatusCodes.Status401Unauthorized,

        "not_a_tenant_member" => StatusCodes.Status403Forbidden,

        "user_not_found" => StatusCodes.Status401Unauthorized,

        "slug_taken" or "invalid_status_transition" or "concurrency_conflict" or "insufficient_stock"
            or "category_in_use" or "product_in_use" or "customer_exists" or "tag_exists"
            => StatusCodes.Status409Conflict,

        "unsupported_media_type" => StatusCodes.Status415UnsupportedMediaType,

        "empty_file" or "file_too_large" or "invalid_alt_text" or "product_not_available"
            => StatusCodes.Status422UnprocessableEntity,

        _ => StatusCodes.Status422UnprocessableEntity,
    };

    [LoggerMessage(EventId = 1000, Level = LogLevel.Error, Message = "Unhandled exception after the response started.")]
    private partial void LogResponseAlreadyStarted(Exception exception);

    [LoggerMessage(EventId = 1001, Level = LogLevel.Error, Message = "Unhandled exception: {Code}")]
    private partial void LogUnhandledException(Exception exception, string code);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Information, Message = "Request failed with {Code} ({Status}).")]
    private partial void LogRequestFailed(string code, int status);
}
