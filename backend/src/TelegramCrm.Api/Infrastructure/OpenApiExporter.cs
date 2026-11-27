using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace TelegramCrm.Api.Infrastructure;

/// <summary>
/// Writes the generated OpenAPI document to disk. Invoked with
/// <c>dotnet run --project src/TelegramCrm.Api -- --generate-openapi ../contracts/openapi.json</c>
/// (see <c>contracts/README.md</c>), so the committed contract is always produced by the
/// real endpoint metadata rather than hand-edited.
/// </summary>
public static class OpenApiExporter
{
    public const string Argument = "--generate-openapi";

    /// <summary>Document name of the single published contract.</summary>
    public const string DocumentName = "v1";

    /// <summary>
    /// Returns the requested output path, or <c>null</c> when the flag is absent. A relative
    /// path is resolved against the repository root (the nearest ancestor of the built
    /// assembly that contains <c>backend/TelegramCrm.sln</c>), so the command behaves the
    /// same no matter which directory <c>dotnet run</c> happens to use as its working
    /// directory.
    /// </summary>
    public static string? GetOutputPath(string[] args)
    {
        var index = Array.IndexOf(args, Argument);
        if (index < 0 || index + 1 >= args.Length)
        {
            return null;
        }

        return Path.GetFullPath(args[index + 1], FindRepositoryRoot());
    }

    /// <summary>Walks up from the built assembly to the directory holding the solution.</summary>
    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "backend", "TelegramCrm.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return Directory.GetCurrentDirectory();
    }

    public static async Task WriteAsync(IServiceProvider services, string outputPath)
    {
        // The provider is registered as a keyed service, keyed by document name ("v1").
        var provider = services.GetRequiredKeyedService<IOpenApiDocumentProvider>(DocumentName);
        var document = await provider.GetOpenApiDocumentAsync(CancellationToken.None).ConfigureAwait(false);

        var directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(outputPath);
        await using var writer = new StreamWriter(stream);
        var jsonWriter = new OpenApiJsonWriter(writer, new OpenApiWriterSettings
        {
            InlineLocalReferences = false,
            InlineExternalReferences = false,
        });

        document.SerializeAsV31(jsonWriter);
        await jsonWriter.FlushAsync().ConfigureAwait(false);
        await writer.WriteLineAsync().ConfigureAwait(false);
    }
}
