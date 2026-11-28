using Microsoft.AspNetCore.Http.HttpResults;
using TelegramCrm.Application.Merchant;

namespace TelegramCrm.Api.Endpoints;

/// <summary>Active shop profile and branding endpoints (logo &amp; default product image).</summary>
public static class MerchantShopEndpoints
{
    public static IEndpointRouteBuilder MapMerchantShopEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/merchant/shop")
            .WithTags("Merchant.Shop")
            .RequireAuthorization("Merchant");

        group.MapGet("/", GetAsync)
            .WithName("Merchant_Shop_Get")
            .WithSummary("Get the active shop's profile and branding URLs.")
            .Produces<MerchantShopDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPatch("/", UpdateAsync)
            .WithName("Merchant_Shop_Update")
            .WithSummary("Update the active shop's name, default language, or currency.")
            .Produces<MerchantShopDto>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/logo", UploadLogoAsync)
            .WithName("Merchant_Shop_UploadLogo")
            .WithSummary("Upload or replace the shop's logo image (multipart/form-data, field 'file').")
            .DisableAntiforgery()
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<MerchantShopDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapDelete("/logo", DeleteLogoAsync)
            .WithName("Merchant_Shop_DeleteLogo")
            .WithSummary("Remove the shop's custom logo image.")
            .Produces<MerchantShopDto>(StatusCodes.Status200OK);

        group.MapPost("/default-product-image", UploadDefaultProductImageAsync)
            .WithName("Merchant_Shop_UploadDefaultProductImage")
            .WithSummary("Upload or replace the default placeholder image used when a product has no images.")
            .DisableAntiforgery()
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<MerchantShopDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapDelete("/default-product-image", DeleteDefaultProductImageAsync)
            .WithName("Merchant_Shop_DeleteDefaultProductImage")
            .WithSummary("Remove the shop's default product placeholder image.")
            .Produces<MerchantShopDto>(StatusCodes.Status200OK);

        return app;
    }

    private static async Task<Ok<MerchantShopDto>> GetAsync(
        MerchantShopService service,
        CancellationToken cancellationToken)
    {
        var shop = await service.GetAsync(cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(shop);
    }

    private static async Task<Results<Ok<MerchantShopDto>, ValidationProblem>> UpdateAsync(
        UpdateMerchantShopRequest request,
        UpdateMerchantShopRequestValidator validator,
        MerchantShopService service,
        CancellationToken cancellationToken)
    {
        var problem = await Validation.CheckAsync(validator, request, cancellationToken).ConfigureAwait(false);
        if (problem is not null)
        {
            return problem;
        }

        var updated = await service.UpdateAsync(request, cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(updated);
    }

    private static async Task<Ok<MerchantShopDto>> UploadLogoAsync(
        IFormFile file,
        MerchantShopService service,
        CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        var shop = await service
            .UploadLogoAsync(file.ContentType, file.Length, stream, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.Ok(shop);
    }

    private static async Task<Ok<MerchantShopDto>> DeleteLogoAsync(
        MerchantShopService service,
        CancellationToken cancellationToken)
    {
        var shop = await service.DeleteLogoAsync(cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(shop);
    }

    private static async Task<Ok<MerchantShopDto>> UploadDefaultProductImageAsync(
        IFormFile file,
        MerchantShopService service,
        CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        var shop = await service
            .UploadDefaultProductImageAsync(file.ContentType, file.Length, stream, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.Ok(shop);
    }

    private static async Task<Ok<MerchantShopDto>> DeleteDefaultProductImageAsync(
        MerchantShopService service,
        CancellationToken cancellationToken)
    {
        var shop = await service.DeleteDefaultProductImageAsync(cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(shop);
    }
}
