using Microsoft.AspNetCore.Http.HttpResults;
using TelegramCrm.Application.Merchant;

namespace TelegramCrm.Api.Endpoints;

/// <summary>Merchant delivery method CRUD endpoints.</summary>
public static class ShippingMethodEndpoints
{
    public static IEndpointRouteBuilder MapShippingMethodEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/merchant/shipping-methods")
            .WithTags("Merchant.ShippingMethods")
            .RequireAuthorization("Merchant");

        group.MapGet("/", ListAsync)
            .WithName("Merchant_ShippingMethods_List")
            .WithSummary("List the shop's delivery methods.")
            .Produces<IReadOnlyList<ShippingMethodDto>>(StatusCodes.Status200OK);

        group.MapPost("/", CreateAsync)
            .WithName("Merchant_ShippingMethods_Create")
            .WithSummary("Create a delivery method.")
            .Produces<ShippingMethodDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        group.MapGet("/{id:guid}", GetAsync)
            .WithName("Merchant_ShippingMethods_Get")
            .WithSummary("Get one delivery method.")
            .Produces<ShippingMethodDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPatch("/{id:guid}", UpdateAsync)
            .WithName("Merchant_ShippingMethods_Update")
            .WithSummary("Partially update a delivery method.")
            .Produces<ShippingMethodDto>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}", DeleteAsync)
            .WithName("Merchant_ShippingMethods_Delete")
            .WithSummary("Delete a delivery method (existing order snapshots remain intact).")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<Ok<IReadOnlyList<ShippingMethodDto>>> ListAsync(
        ShippingMethodService service,
        CancellationToken cancellationToken,
        bool? isActive = null)
    {
        var result = await service.ListAsync(isActive, cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(result);
    }

    private static async Task<Results<Created<ShippingMethodDto>, ValidationProblem>> CreateAsync(
        CreateShippingMethodRequest request,
        CreateShippingMethodRequestValidator validator,
        ShippingMethodService service,
        CancellationToken cancellationToken)
    {
        var problem = await Validation.CheckAsync(validator, request, cancellationToken).ConfigureAwait(false);
        if (problem is not null)
        {
            return problem;
        }

        var created = await service.CreateAsync(request, cancellationToken).ConfigureAwait(false);
        return TypedResults.Created($"/api/v1/merchant/shipping-methods/{created.Id}", created);
    }

    private static async Task<Ok<ShippingMethodDto>> GetAsync(
        Guid id,
        ShippingMethodService service,
        CancellationToken cancellationToken)
    {
        var method = await service.GetAsync(id, cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(method);
    }

    private static async Task<Results<Ok<ShippingMethodDto>, ValidationProblem>> UpdateAsync(
        Guid id,
        UpdateShippingMethodRequest request,
        UpdateShippingMethodRequestValidator validator,
        ShippingMethodService service,
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
        ShippingMethodService service,
        CancellationToken cancellationToken)
    {
        await service.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
        return TypedResults.NoContent();
    }
}
