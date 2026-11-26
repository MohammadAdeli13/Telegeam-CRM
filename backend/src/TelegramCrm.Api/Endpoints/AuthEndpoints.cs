using Microsoft.AspNetCore.Http.HttpResults;
using TelegramCrm.Application.Auth;
using TelegramCrm.Application.Common;

namespace TelegramCrm.Api.Endpoints;

/// <summary>Auth endpoints — contract v1.2 §4.1.</summary>
public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/auth").WithTags("Auth");

        group.MapPost("/telegram", LoginWithTelegramAsync)
            .AllowAnonymous()
            .WithName("Auth_LoginWithTelegram")
            .WithSummary("Validate Telegram WebApp initData and issue an access token.")
            .Produces<AuthResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/switch-tenant", SwitchTenantAsync)
            .RequireAuthorization()
            .WithName("Auth_SwitchTenant")
            .WithSummary("Re-issue the access token scoped to another tenant the caller belongs to.")
            .Produces<AuthResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return app;
    }

    private static async Task<Ok<AuthResponse>> LoginWithTelegramAsync(
        TelegramLoginRequest request,
        AuthService authService,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var botToken = configuration["TELEGRAM_BOT_TOKEN"];
        var response = await authService
            .LoginWithTelegramAsync(request.InitData, botToken, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.Ok(response);
    }

    private static async Task<Ok<AuthResponse>> SwitchTenantAsync(
        SwitchTenantRequest request,
        AuthService authService,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
            ?? throw new UnauthorizedAccessException("The access token carries no user id.");

        var response = await authService
            .SwitchTenantAsync(userId, request.TenantId, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.Ok(response);
    }
}
