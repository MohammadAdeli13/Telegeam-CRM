using Microsoft.AspNetCore.Http.HttpResults;
using TelegramCrm.Application.Merchant;

namespace TelegramCrm.Api.Endpoints;

/// <summary>Shop settings endpoints — contract v1.2 §4.3.</summary>
public static class SettingsEndpoints
{
    public static IEndpointRouteBuilder MapSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/merchant/settings")
            .WithTags("Merchant.Settings")
            .RequireAuthorization("Merchant");

        group.MapGet("/", GetAsync)
            .WithName("Merchant_Settings_Get")
            .WithSummary("Read the shop's settings.")
            .Produces<IReadOnlyList<ShopSettingDto>>(StatusCodes.Status200OK);

        group.MapPut("/", ReplaceAsync)
            .WithName("Merchant_Settings_Replace")
            .WithSummary("Replace the shop's whole settings set in one transaction.")
            .Produces<IReadOnlyList<ShopSettingDto>>(StatusCodes.Status200OK)
            .ProducesValidationProblem();

        return app;
    }

    private static async Task<Ok<IReadOnlyList<ShopSettingDto>>> GetAsync(
        ShopSettingService service,
        CancellationToken cancellationToken)
    {
        var settings = await service.GetAsync(cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(settings);
    }

    private static async Task<Results<Ok<IReadOnlyList<ShopSettingDto>>, ValidationProblem>> ReplaceAsync(
        UpdateSettingsRequest request,
        UpdateSettingsRequestValidator validator,
        ShopSettingService service,
        CancellationToken cancellationToken)
    {
        var problem = await Validation.CheckAsync(validator, request, cancellationToken).ConfigureAwait(false);
        if (problem is not null)
        {
            return problem;
        }

        var settings = await service.ReplaceAsync(request, cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(settings);
    }
}
