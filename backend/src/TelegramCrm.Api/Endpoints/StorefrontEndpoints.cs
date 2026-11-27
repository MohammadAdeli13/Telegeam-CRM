using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using TelegramCrm.Api.Infrastructure;
using TelegramCrm.Application.Common;
using TelegramCrm.Application.Orders;
using TelegramCrm.Application.Storefront;

namespace TelegramCrm.Api.Endpoints;

/// <summary>Customer storefront endpoints — contract v1.2 §4.4.</summary>
public static class StorefrontEndpoints
{
    public static IEndpointRouteBuilder MapStorefrontEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/storefront/{shopSlug}")
            .WithTags("Storefront")
            .AddEndpointFilter<StorefrontTenantFilter>();

        // Public storefront read endpoints.
        group.MapGet("/", GetShopAsync)
            .WithName("Storefront_GetShop")
            .WithSummary("Get public information for an active shop.")
            .AllowAnonymous()
            .Produces<StorefrontShopDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/categories", ListCategoriesAsync)
            .WithName("Storefront_Categories_List")
            .WithSummary("List active categories for a shop.")
            .AllowAnonymous()
            .Produces<IReadOnlyList<StorefrontCategoryDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/products", ListProductsAsync)
            .WithName("Storefront_Products_List")
            .WithSummary("List active products for a shop.")
            .AllowAnonymous()
            .Produces<PagedResult<StorefrontProductSummaryDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/products/{id:guid}", GetProductAsync)
            .WithName("Storefront_Products_Get")
            .WithSummary("Get active product details including images.")
            .AllowAnonymous()
            .Produces<StorefrontProductDetailDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // Authenticated customer cart and order endpoints.
        group.MapPost("/cart/validate", ValidateCartAsync)
            .WithName("Storefront_Cart_Validate")
            .WithSummary("Validate cart items against current stock and prices.")
            .RequireAuthorization()
            .Produces<CartValidationResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/orders", PlaceOrderAsync)
            .WithName("Storefront_Orders_Place")
            .WithSummary("Place an order atomically (Idempotency-Key header mandatory).")
            .RequireAuthorization()
            .Produces<OrderDetailDto>(StatusCodes.Status201Created)
            .Produces<OrderDetailDto>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapGet("/orders", ListMyOrdersAsync)
            .WithName("Storefront_Orders_List")
            .WithSummary("List the authenticated customer's own orders in this shop.")
            .RequireAuthorization()
            .Produces<PagedResult<OrderSummaryDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/orders/{id:guid}", GetMyOrderAsync)
            .WithName("Storefront_Orders_Get")
            .WithSummary("Get the authenticated customer's own order detail and timeline.")
            .RequireAuthorization()
            .Produces<OrderDetailDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/orders/{id:guid}/cancel", CancelMyOrderAsync)
            .WithName("Storefront_Orders_Cancel")
            .WithSummary("Cancel the customer's own Pending order and restore stock.")
            .RequireAuthorization()
            .Produces<OrderDetailDto>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }

    private static async Task<Ok<StorefrontShopDto>> GetShopAsync(
        string shopSlug,
        StorefrontService service,
        CancellationToken cancellationToken)
    {
        _ = shopSlug;
        var shop = await service.GetShopAsync(cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(shop);
    }

    private static async Task<Ok<IReadOnlyList<StorefrontCategoryDto>>> ListCategoriesAsync(
        string shopSlug,
        StorefrontService service,
        CancellationToken cancellationToken)
    {
        _ = shopSlug;
        var categories = await service.ListCategoriesAsync(cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(categories);
    }

    private static async Task<Ok<PagedResult<StorefrontProductSummaryDto>>> ListProductsAsync(
        string shopSlug,
        StorefrontService service,
        CancellationToken cancellationToken,
        int? page = null,
        int? pageSize = null,
        Guid? categoryId = null,
        string? q = null)
    {
        _ = shopSlug;
        var result = await service
            .ListProductsAsync(page, pageSize, categoryId, q, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.Ok(result);
    }

    private static async Task<Ok<StorefrontProductDetailDto>> GetProductAsync(
        string shopSlug,
        Guid id,
        StorefrontService service,
        CancellationToken cancellationToken)
    {
        _ = shopSlug;
        var product = await service.GetProductAsync(id, cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(product);
    }

    private static async Task<Results<Ok<CartValidationResponse>, ValidationProblem>> ValidateCartAsync(
        string shopSlug,
        ValidateCartRequest request,
        ValidateCartRequestValidator validator,
        StorefrontService service,
        CancellationToken cancellationToken)
    {
        _ = shopSlug;
        var problem = await Validation.CheckAsync(validator, request, cancellationToken).ConfigureAwait(false);
        if (problem is not null)
        {
            return problem;
        }

        var result = await service.ValidateCartAsync(request, cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(result);
    }

    private static async Task<Results<Created<OrderDetailDto>, Ok<OrderDetailDto>, ValidationProblem>> PlaceOrderAsync(
        string shopSlug,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        PlaceOrderRequest request,
        PlaceOrderRequestValidator validator,
        StorefrontService service,
        CancellationToken cancellationToken)
    {
        var problem = await Validation.CheckAsync(validator, request, cancellationToken).ConfigureAwait(false);
        if (problem is not null)
        {
            return problem;
        }

        var (order, created) = await service
            .PlaceOrderAsync(request, idempotencyKey, cancellationToken)
            .ConfigureAwait(false);

        return created
            ? TypedResults.Created($"/api/v1/storefront/{shopSlug}/orders/{order.Id}", order)
            : TypedResults.Ok(order);
    }

    private static async Task<Ok<PagedResult<OrderSummaryDto>>> ListMyOrdersAsync(
        string shopSlug,
        StorefrontService service,
        CancellationToken cancellationToken,
        int? page = null,
        int? pageSize = null)
    {
        _ = shopSlug;
        var result = await service.ListMyOrdersAsync(page, pageSize, cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(result);
    }

    private static async Task<Ok<OrderDetailDto>> GetMyOrderAsync(
        string shopSlug,
        Guid id,
        StorefrontService service,
        CancellationToken cancellationToken)
    {
        _ = shopSlug;
        var order = await service.GetMyOrderAsync(id, cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(order);
    }

    private static async Task<Results<Ok<OrderDetailDto>, ValidationProblem>> CancelMyOrderAsync(
        string shopSlug,
        Guid id,
        CancelOrderRequestValidator validator,
        OrderService orderService,
        CancellationToken cancellationToken,
        CancelOrderRequest? request = null)
    {
        _ = shopSlug;
        if (request is not null)
        {
            var problem = await Validation.CheckAsync(validator, request, cancellationToken).ConfigureAwait(false);
            if (problem is not null)
            {
                return problem;
            }
        }

        var cancelled = await orderService
            .CancelCustomerOrderAsync(id, request, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.Ok(cancelled);
    }
}
