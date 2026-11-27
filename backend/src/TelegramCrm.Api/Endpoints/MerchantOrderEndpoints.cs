using Microsoft.AspNetCore.Http.HttpResults;
using TelegramCrm.Application.Common;
using TelegramCrm.Application.Orders;

namespace TelegramCrm.Api.Endpoints;

/// <summary>Merchant order management endpoints — contract v1.2 §4.3.</summary>
public static class MerchantOrderEndpoints
{
    public static IEndpointRouteBuilder MapMerchantOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/merchant/orders")
            .WithTags("Merchant.Orders")
            .RequireAuthorization("Merchant");

        group.MapGet("/", ListAsync)
            .WithName("Merchant_Orders_List")
            .WithSummary("List the shop's orders, newest first.")
            .Produces<PagedResult<OrderSummaryDto>>(StatusCodes.Status200OK);

        group.MapGet("/{id:guid}", GetAsync)
            .WithName("Merchant_Orders_Get")
            .WithSummary("Get full order detail including items and status history.")
            .Produces<OrderDetailDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPatch("/{id:guid}/status", UpdateStatusAsync)
            .WithName("Merchant_Orders_UpdateStatus")
            .WithSummary("Transition an order status using the state machine.")
            .Produces<OrderDetailDto>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/{id:guid}/history", GetHistoryAsync)
            .WithName("Merchant_Orders_GetHistory")
            .WithSummary("Get an order's append-only status timeline.")
            .Produces<IReadOnlyList<OrderStatusHistoryDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<Ok<PagedResult<OrderSummaryDto>>> ListAsync(
        OrderService service,
        CancellationToken cancellationToken,
        int? page = null,
        int? pageSize = null,
        string? status = null,
        Guid? customerId = null)
    {
        var result = await service
            .ListMerchantOrdersAsync(page, pageSize, status, customerId, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.Ok(result);
    }

    private static async Task<Ok<OrderDetailDto>> GetAsync(
        Guid id,
        OrderService service,
        CancellationToken cancellationToken)
    {
        var order = await service.GetMerchantOrderAsync(id, cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(order);
    }

    private static async Task<Results<Ok<OrderDetailDto>, ValidationProblem>> UpdateStatusAsync(
        Guid id,
        UpdateOrderStatusRequest request,
        UpdateOrderStatusRequestValidator validator,
        OrderService service,
        CancellationToken cancellationToken)
    {
        var problem = await Validation.CheckAsync(validator, request, cancellationToken).ConfigureAwait(false);
        if (problem is not null)
        {
            return problem;
        }

        var updated = await service
            .TransitionMerchantOrderStatusAsync(id, request, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.Ok(updated);
    }

    private static async Task<Ok<IReadOnlyList<OrderStatusHistoryDto>>> GetHistoryAsync(
        Guid id,
        OrderService service,
        CancellationToken cancellationToken)
    {
        var history = await service.GetMerchantOrderHistoryAsync(id, cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(history);
    }
}
