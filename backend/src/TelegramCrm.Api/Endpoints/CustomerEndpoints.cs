using Microsoft.AspNetCore.Http.HttpResults;
using TelegramCrm.Application.Common;
using TelegramCrm.Application.Merchant;

namespace TelegramCrm.Api.Endpoints;

/// <summary>Customer endpoints — contract v1.2 §4.3.</summary>
public static class CustomerEndpoints
{
    public static IEndpointRouteBuilder MapCustomerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/merchant/customers")
            .WithTags("Merchant.Customers")
            .RequireAuthorization("Merchant");

        group.MapGet("/", ListAsync)
            .WithName("Merchant_Customers_List")
            .WithSummary("List the shop's customers.")
            .Produces<PagedResult<CustomerDto>>(StatusCodes.Status200OK);

        group.MapPost("/", CreateAsync)
            .WithName("Merchant_Customers_Create")
            .WithSummary("Create a customer.")
            .Produces<CustomerDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/{id:guid}", GetAsync)
            .WithName("Merchant_Customers_Get")
            .WithSummary("Get one customer with their tags.")
            .Produces<CustomerDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPatch("/{id:guid}", UpdateAsync)
            .WithName("Merchant_Customers_Update")
            .WithSummary("Partially update a customer.")
            .Produces<CustomerDto>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}/orders", ListOrdersAsync)
            .WithName("Merchant_Customers_ListOrders")
            .WithSummary("A customer's order history, newest first.")
            .Produces<PagedResult<CustomerOrderDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/tags", AttachTagAsync)
            .WithName("Merchant_Customers_AttachTag")
            .WithSummary("Attach a tag to a customer.")
            .Produces<CustomerDto>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}/tags/{tagId:guid}", DetachTagAsync)
            .WithName("Merchant_Customers_DetachTag")
            .WithSummary("Detach a tag from a customer.")
            .Produces<CustomerDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<Ok<PagedResult<CustomerDto>>> ListAsync(
        CustomerService service,
        CancellationToken cancellationToken,
        int? page = null,
        int? pageSize = null,
        string? q = null,
        Guid? tagId = null)
    {
        var result = await service
            .ListAsync(page, pageSize, q, tagId, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.Ok(result);
    }

    private static async Task<Results<Created<CustomerDto>, ValidationProblem>> CreateAsync(
        CreateCustomerRequest request,
        CreateCustomerRequestValidator validator,
        CustomerService service,
        CancellationToken cancellationToken)
    {
        var problem = await Validation.CheckAsync(validator, request, cancellationToken).ConfigureAwait(false);
        if (problem is not null)
        {
            return problem;
        }

        var created = await service.CreateAsync(request, cancellationToken).ConfigureAwait(false);
        return TypedResults.Created($"/api/v1/merchant/customers/{created.Id}", created);
    }

    private static async Task<Ok<CustomerDto>> GetAsync(
        Guid id,
        CustomerService service,
        CancellationToken cancellationToken)
    {
        var customer = await service.GetAsync(id, cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(customer);
    }

    private static async Task<Results<Ok<CustomerDto>, ValidationProblem>> UpdateAsync(
        Guid id,
        UpdateCustomerRequest request,
        UpdateCustomerRequestValidator validator,
        CustomerService service,
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

    private static async Task<Ok<PagedResult<CustomerOrderDto>>> ListOrdersAsync(
        Guid id,
        CustomerService service,
        CancellationToken cancellationToken,
        int? page = null,
        int? pageSize = null)
    {
        var result = await service
            .ListOrdersAsync(id, page, pageSize, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.Ok(result);
    }

    private static async Task<Results<Ok<CustomerDto>, ValidationProblem>> AttachTagAsync(
        Guid id,
        AttachTagRequest request,
        AttachTagRequestValidator validator,
        CustomerService service,
        CancellationToken cancellationToken)
    {
        var problem = await Validation.CheckAsync(validator, request, cancellationToken).ConfigureAwait(false);
        if (problem is not null)
        {
            return problem;
        }

        var customer = await service.AttachTagAsync(id, request, cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(customer);
    }

    private static async Task<Ok<CustomerDto>> DetachTagAsync(
        Guid id,
        Guid tagId,
        CustomerService service,
        CancellationToken cancellationToken)
    {
        var customer = await service.DetachTagAsync(id, tagId, cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(customer);
    }
}
