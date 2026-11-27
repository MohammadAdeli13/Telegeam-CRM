using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using TelegramCrm.Application.Merchant;

namespace TelegramCrm.Api.Endpoints;

/// <summary>Product image endpoints — contract v1.2 §4.3.</summary>
public static class ProductImageEndpoints
{
    public static IEndpointRouteBuilder MapProductImageEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/merchant/products/{productId:guid}/images")
            .WithTags("Merchant.ProductImages")
            .RequireAuthorization("Merchant");

        group.MapGet("/", ListAsync)
            .WithName("Merchant_ProductImages_List")
            .WithSummary("List a product's images, primary first.")
            .Produces<IReadOnlyList<ProductImageDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", UploadAsync)
            .WithName("Merchant_ProductImages_Upload")
            .WithSummary("Upload an image (multipart/form-data, field 'file').")
            .DisableAntiforgery()
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<ProductImageDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPatch("/{imageId:guid}", UpdateAsync)
            .WithName("Merchant_ProductImages_Update")
            .WithSummary("Update an image's alt text or sort order.")
            .Produces<ProductImageDto>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{imageId:guid}", DeleteAsync)
            .WithName("Merchant_ProductImages_Delete")
            .WithSummary("Delete an image; the next image becomes primary if it was primary.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{imageId:guid}/primary", SetPrimaryAsync)
            .WithName("Merchant_ProductImages_SetPrimary")
            .WithSummary("Make this image the product's only primary image (atomic swap).")
            .Produces<ProductImageDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<Ok<IReadOnlyList<ProductImageDto>>> ListAsync(
        Guid productId,
        ProductImageService service,
        CancellationToken cancellationToken)
    {
        var images = await service.ListAsync(productId, cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(images);
    }

    private static async Task<Results<Created<ProductImageDto>, ProblemHttpResult>> UploadAsync(
        Guid productId,
        IFormFile file,
        ProductImageService service,
        CancellationToken cancellationToken,
        [FromForm] string? altText = null)
    {
        await using var stream = file.OpenReadStream();

        var image = await service
            .UploadAsync(
                productId,
                file.FileName,
                file.ContentType,
                file.Length,
                stream,
                altText,
                cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.Created(
            $"/api/v1/merchant/products/{productId}/images/{image.Id}",
            image);
    }

    private static async Task<Results<Ok<ProductImageDto>, ValidationProblem>> UpdateAsync(
        Guid productId,
        Guid imageId,
        UpdateProductImageRequest request,
        UpdateProductImageRequestValidator validator,
        ProductImageService service,
        CancellationToken cancellationToken)
    {
        var problem = await Validation.CheckAsync(validator, request, cancellationToken).ConfigureAwait(false);
        if (problem is not null)
        {
            return problem;
        }

        var updated = await service
            .UpdateAsync(productId, imageId, request, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.Ok(updated);
    }

    private static async Task<NoContent> DeleteAsync(
        Guid productId,
        Guid imageId,
        ProductImageService service,
        CancellationToken cancellationToken)
    {
        await service.DeleteAsync(productId, imageId, cancellationToken).ConfigureAwait(false);
        return TypedResults.NoContent();
    }

    private static async Task<Ok<ProductImageDto>> SetPrimaryAsync(
        Guid productId,
        Guid imageId,
        ProductImageService service,
        CancellationToken cancellationToken)
    {
        var image = await service.SetPrimaryAsync(productId, imageId, cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(image);
    }
}
