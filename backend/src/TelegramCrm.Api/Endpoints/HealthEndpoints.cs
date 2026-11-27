using Microsoft.AspNetCore.Http.HttpResults;
using TelegramCrm.Infrastructure.Persistence;

namespace TelegramCrm.Api.Endpoints;

/// <summary>Liveness/readiness endpoint — contract v1.2 §4 (used by the container healthcheck).</summary>
public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/health", GetHealthAsync)
            .AllowAnonymous()
            .WithTags("Health")
            .WithName("Health_Get")
            .WithSummary("Reports API liveness and database reachability.")
            .Produces<HealthResponse>(StatusCodes.Status200OK)
            .Produces<HealthResponse>(StatusCodes.Status503ServiceUnavailable);

        return app;
    }

    private static async Task<Results<Ok<HealthResponse>, JsonHttpResult<HealthResponse>>> GetHealthAsync(
        CrmDbContext db,
        CancellationToken cancellationToken)
    {
        var databaseReachable = await db.Database
            .CanConnectAsync(cancellationToken)
            .ConfigureAwait(false);

        var response = new HealthResponse(
            databaseReachable ? "healthy" : "degraded",
            databaseReachable ? "up" : "down",
            DateTimeOffset.UtcNow);

        return databaseReachable
            ? TypedResults.Ok(response)
            : TypedResults.Json(response, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}

/// <summary>Health payload.</summary>
public sealed record HealthResponse(string Status, string Database, DateTimeOffset CheckedAt);
