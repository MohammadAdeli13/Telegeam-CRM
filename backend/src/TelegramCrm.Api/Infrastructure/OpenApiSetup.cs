using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace TelegramCrm.Api.Infrastructure;

/// <summary>
/// Makes the generated OpenAPI document codegen-ready (P1 QA finding): it declares the
/// <c>Bearer</c> HTTP security scheme and marks every operation that is not explicitly
/// anonymous as requiring it. Without this, a generated client has no way to know which
/// calls need an <c>Authorization</c> header.
/// </summary>
public static class OpenApiSetup
{
    /// <summary>Name of the security scheme as it appears in <c>components.securitySchemes</c>.</summary>
    public const string BearerSchemeName = "Bearer";

    public static IServiceCollection AddCrmOpenApi(this IServiceCollection services)
    {
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
            options.AddOperationTransformer<BearerSecurityRequirementTransformer>();
        });

        return services;
    }
}

/// <summary>Registers the <c>Bearer</c> JWT security scheme on the document.</summary>
internal sealed class BearerSecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??=
            new Dictionary<string, IOpenApiSecurityScheme>(StringComparer.Ordinal);

        document.Components.SecuritySchemes[OpenApiSetup.BearerSchemeName] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Access token issued by POST /api/v1/auth/telegram (HS256 JWT, 60 minutes).",
        };

        return Task.CompletedTask;
    }
}

/// <summary>
/// Adds <c>security: [{ Bearer: [] }]</c> to every operation that is not marked
/// <see cref="IAllowAnonymous"/>, so the contract states exactly which calls need a token.
/// </summary>
internal sealed class BearerSecurityRequirementTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        var metadata = context.Description.ActionDescriptor.EndpointMetadata;
        var isAnonymous = metadata.OfType<IAllowAnonymous>().Any();

        if (isAnonymous)
        {
            return Task.CompletedTask;
        }

        var schemeReference = new OpenApiSecuritySchemeReference(
            OpenApiSetup.BearerSchemeName,
            context.Document,
            externalResource: null);

        operation.Security ??= [];
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [schemeReference] = [],
        });

        return Task.CompletedTask;
    }
}
