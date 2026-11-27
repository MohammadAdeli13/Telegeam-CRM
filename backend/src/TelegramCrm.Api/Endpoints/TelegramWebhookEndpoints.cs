using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TelegramCrm.Application.Telegram;
using TelegramCrm.Domain.Exceptions;
using TelegramCrm.Infrastructure.Telegram;

namespace TelegramCrm.Api.Endpoints;

/// <summary>Single platform Telegram Bot webhook receiver — contract v1.2 §4.5.</summary>
public static class TelegramWebhookEndpoints
{
    public const string SecretTokenHeader = "X-Telegram-Bot-Api-Secret-Token";

    public static IEndpointRouteBuilder MapTelegramWebhookEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/telegram/webhook", HandleWebhookAsync)
            .WithTags("Telegram")
            .WithName("Telegram_Webhook")
            .WithSummary("Receive Telegram Bot API updates (validated by secret token header).")
            .AllowAnonymous()
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        return app;
    }

    private static async Task<Ok> HandleWebhookAsync(
        [FromHeader(Name = SecretTokenHeader)] string? providedSecret,
        TelegramWebhookUpdate update,
        IConfiguration configuration,
        IOptions<TelegramBotOptions> options,
        TelegramWebhookService service,
        CancellationToken cancellationToken)
    {
        var expectedSecret = configuration["TELEGRAM_WEBHOOK_SECRET"];
        if (string.IsNullOrWhiteSpace(expectedSecret))
        {
            expectedSecret = options.Value.WebhookSecret;
        }

        if (string.IsNullOrWhiteSpace(expectedSecret) ||
            string.IsNullOrWhiteSpace(providedSecret) ||
            !ConstantTimeEquals(expectedSecret, providedSecret))
        {
            throw new DomainException(
                "webhook_secret_invalid",
                "Invalid or missing Telegram webhook secret token.");
        }

        await service
            .HandleUpdateAsync(update, options.Value.MiniAppBaseUrl, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.Ok();
    }

    private static bool ConstantTimeEquals(string expected, string provided)
    {
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var providedBytes = Encoding.UTF8.GetBytes(provided);

        return expectedBytes.Length == providedBytes.Length &&
            CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);
    }
}
