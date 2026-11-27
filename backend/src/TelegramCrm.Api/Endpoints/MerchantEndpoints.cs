using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using TelegramCrm.Application.Common;
using TelegramCrm.Application.Merchants;

namespace TelegramCrm.Api.Endpoints;

/// <summary>Merchant onboarding endpoints — contract v1.2 §4.2.</summary>
public static class MerchantEndpoints
{
    public static IEndpointRouteBuilder MapMerchantEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/merchants")
            .WithTags("Merchants")
            .RequireAuthorization();

        group.MapPost("/", CreateAsync)
            .WithName("Merchants_Create")
            .WithSummary("Create a shop; the caller becomes its MerchantOwner.")
            .Produces<MerchantDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/mine", GetMineAsync)
            .WithName("Merchants_GetMine")
            .WithSummary("Shops the caller belongs to.")
            .Produces<IReadOnlyList<MerchantDto>>(StatusCodes.Status200OK);

        return app;
    }

    private static async Task<Results<Created<MerchantDto>, ValidationProblem>> CreateAsync(
        CreateMerchantRequest request,
        CreateMerchantRequestValidator validator,
        MerchantService merchantService,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken).ConfigureAwait(false);
        if (!validation.IsValid)
        {
            return TypedResults.ValidationProblem(validation.ToDictionary());
        }

        var userId = currentUser.UserId
            ?? throw new UnauthorizedAccessException("The access token carries no user id.");

        var created = await merchantService
            .CreateAsync(userId, request, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.Created($"/api/v1/merchants/{created.Id}", created);
    }

    private static async Task<Ok<IReadOnlyList<MerchantDto>>> GetMineAsync(
        MerchantService merchantService,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
            ?? throw new UnauthorizedAccessException("The access token carries no user id.");

        var merchants = await merchantService
            .GetMineAsync(userId, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.Ok(merchants);
    }
}
