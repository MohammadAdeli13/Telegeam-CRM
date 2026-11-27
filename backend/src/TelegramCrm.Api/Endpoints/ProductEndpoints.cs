using Microsoft.AspNetCore.Http.HttpResults;
using TelegramCrm.Application.Common;
using TelegramCrm.Application.Merchant;

namespace TelegramCrm.Api.Endpoints;

/// <summary>Product and stock endpoints — contract v1.2 §4.3.</summary>
public static class ProductEndpoints
{
    public static IEndpointRouteBuilder MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/merchant/products")
            .WithTags("Merchant.Products")
            .RequireAuthorization("Merchant");

        group.MapGet("/", ListAsync)
            .WithName("Merchant_Products_List")
            .WithSummary("List the shop's products.")
            .Produces<PagedResult<ProductDto>>(StatusCodes.Status200OK);

        group.MapPost("/", CreateAsync)
            .WithName("Merchant_Products_Create")
            .WithSummary("Create a product.")
            .Produces<ProductDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapGet("/{id:guid}", GetAsync)
            .WithName("Merchant_Products_Get")
            .WithSummary("Get one product.")
            .Produces<ProductDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPatch("/{id:guid}", UpdateAsync)
            .WithName("Merchant_Products_Update")
            .WithSummary("Partially update a product.")
            .Produces<ProductDto>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}", DeleteAsync)
            .WithName("Merchant_Products_Delete")
            .WithSummary("Delete a product that has never been ordered.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/stock-adjust", AdjustStockAsync)
            .WithName("Merchant_Products_AdjustStock")
            .WithSummary("Atomically adjust stock; 409 insufficient_stock if it would go negative.")
            .Produces<StockAdjustResult>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }

    private static async Task<Ok<PagedResult<ProductDto>>> ListAsync(
        ProductService service,
        CancellationToken cancellationToken,
        int? page = null,
        int? pageSize = null,
        Guid? categoryId = null,
        string? q = null,
        bool? isActive = null)
    {
        var result = await service
            .ListAsync(page, pageSize, categoryId, q, isActive, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.Ok(result);
    }

    private static async Task<Results<Created<ProductDto>, ValidationProblem>> CreateAsync(
        CreateProductRequest request,
        CreateProductRequestValidator validator,
        ProductService service,
        CancellationToken cancellationToken)
    {
        var problem = await Validation.CheckAsync(validator, request, cancellationToken).ConfigureAwait(false);
        if (problem is not null)
        {
            return problem;
        }

        var created = await service.CreateAsync(request, cancellationToken).ConfigureAwait(false);
        return TypedResults.Created($"/api/v1/merchant/products/{created.Id}", created);
    }

    private static async Task<Ok<ProductDto>> GetAsync(
        Guid id,
        ProductService service,
        CancellationToken cancellationToken)
    {
        var product = await service.GetAsync(id, cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(product);
    }

    private static async Task<Results<Ok<ProductDto>, ValidationProblem>> UpdateAsync(
        Guid id,
        UpdateProductRequest request,
        UpdateProductRequestValidator validator,
        ProductService service,
        CancellationToken cancellationToken)
    {
        var problem = await Validation.CheckAsync(validator, request, cancellationToken).ConfigureAwait(false);
        if (problem is not null)
        {
            return problem;
        }

        var updated = await service.UpdateAsync(id, request, cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(updated);
    }

    private static async Task<NoContent> DeleteAsync(
        Guid id,
        ProductService service,
        CancellationToken cancellationToken)
    {
        await service.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<StockAdjustResult>, ValidationProblem>> AdjustStockAsync(
        Guid id,
        StockAdjustRequest request,
        StockAdjustRequestValidator validator,
        ProductService service,
        CancellationToken cancellationToken)
    {
        var problem = await Validation.CheckAsync(validator, request, cancellationToken).ConfigureAwait(false);
        if (problem is not null)
        {
            return problem;
        }

        var result = await service.AdjustStockAsync(id, request, cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(result);
    }
}
