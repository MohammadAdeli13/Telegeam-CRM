using Microsoft.AspNetCore.Http.HttpResults;
using TelegramCrm.Application.Merchant;

namespace TelegramCrm.Api.Endpoints;

/// <summary>Tag endpoints — contract v1.2 §4.3.</summary>
public static class TagEndpoints
{
    public static IEndpointRouteBuilder MapTagEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/merchant/tags")
            .WithTags("Merchant.Tags")
            .RequireAuthorization("Merchant");

        group.MapGet("/", ListAsync)
            .WithName("Merchant_Tags_List")
            .WithSummary("List the shop's tags.")
            .Produces<IReadOnlyList<TagDto>>(StatusCodes.Status200OK);

        group.MapPost("/", CreateAsync)
            .WithName("Merchant_Tags_Create")
            .WithSummary("Create a tag.")
            .Produces<TagDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{id:guid}", DeleteAsync)
            .WithName("Merchant_Tags_Delete")
            .WithSummary("Delete a tag; it detaches from every customer.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<Ok<IReadOnlyList<TagDto>>> ListAsync(
        CustomerService service,
        CancellationToken cancellationToken)
    {
        var tags = await service.ListTagsAsync(cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(tags);
    }

    private static async Task<Results<Created<TagDto>, ValidationProblem>> CreateAsync(
        CreateTagRequest request,
        CreateTagRequestValidator validator,
        CustomerService service,
        CancellationToken cancellationToken)
    {
        var problem = await Validation.CheckAsync(validator, request, cancellationToken).ConfigureAwait(false);
        if (problem is not null)
        {
            return problem;
        }

        var created = await service.CreateTagAsync(request, cancellationToken).ConfigureAwait(false);
        return TypedResults.Created($"/api/v1/merchant/tags/{created.Id}", created);
    }

    private static async Task<NoContent> DeleteAsync(
        Guid id,
        CustomerService service,
        CancellationToken cancellationToken)
    {
        await service.DeleteTagAsync(id, cancellationToken).ConfigureAwait(false);
        return TypedResults.NoContent();
    }
}
