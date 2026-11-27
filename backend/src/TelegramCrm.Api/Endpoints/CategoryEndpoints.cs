using Microsoft.AspNetCore.Http.HttpResults;
using TelegramCrm.Application.Common;
using TelegramCrm.Application.Merchant;

namespace TelegramCrm.Api.Endpoints;

/// <summary>Category endpoints — contract v1.2 §4.3.</summary>
public static class CategoryEndpoints
{
    public static IEndpointRouteBuilder MapCategoryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/merchant/categories")
            .WithTags("Merchant.Categories")
            .RequireAuthorization("Merchant");

        group.MapGet("/", ListAsync)
            .WithName("Merchant_Categories_List")
            .WithSummary("List the shop's categories.")
            .Produces<PagedResult<CategoryDto>>(StatusCodes.Status200OK);

        group.MapPost("/", CreateAsync)
            .WithName("Merchant_Categories_Create")
            .WithSummary("Create a category.")
            .Produces<CategoryDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/{id:guid}", GetAsync)
            .WithName("Merchant_Categories_Get")
            .WithSummary("Get one category.")
            .Produces<CategoryDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPatch("/{id:guid}", UpdateAsync)
            .WithName("Merchant_Categories_Update")
            .WithSummary("Partially update a category.")
            .Produces<CategoryDto>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{id:guid}", DeleteAsync)
            .WithName("Merchant_Categories_Delete")
            .WithSummary("Delete a category that has no products.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }

    private static async Task<Ok<PagedResult<CategoryDto>>> ListAsync(
        CategoryService service,
        CancellationToken cancellationToken,
        int? page = null,
        int? pageSize = null,
        bool? isActive = null)
    {
        var result = await service.ListAsync(page, pageSize, isActive, cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(result);
    }

    private static async Task<Results<Created<CategoryDto>, ValidationProblem>> CreateAsync(
        CreateCategoryRequest request,
        CreateCategoryRequestValidator validator,
        CategoryService service,
        CancellationToken cancellationToken)
    {
        var problem = await Validation.CheckAsync(validator, request, cancellationToken).ConfigureAwait(false);
        if (problem is not null)
        {
            return problem;
        }

        var created = await service.CreateAsync(request, cancellationToken).ConfigureAwait(false);
        return TypedResults.Created($"/api/v1/merchant/categories/{created.Id}", created);
    }

    private static async Task<Ok<CategoryDto>> GetAsync(
        Guid id,
        CategoryService service,
        CancellationToken cancellationToken)
    {
        var category = await service.GetAsync(id, cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(category);
    }

    private static async Task<Results<Ok<CategoryDto>, ValidationProblem>> UpdateAsync(
        Guid id,
        UpdateCategoryRequest request,
        UpdateCategoryRequestValidator validator,
        CategoryService service,
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
        CategoryService service,
        CancellationToken cancellationToken)
    {
        await service.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
        return TypedResults.NoContent();
    }
}
