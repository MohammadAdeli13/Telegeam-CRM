using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.Json;
using TelegramCrm.Api.Endpoints;
using TelegramCrm.Api.Infrastructure;
using TelegramCrm.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<JsonOptions>(options =>
{
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApiAuthentication(builder.Configuration, builder.Environment);

builder.Services.AddProblemDetails();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthEndpoints();
app.MapAuthEndpoints();
app.MapMerchantEndpoints();

app.Run();

/// <summary>Entry point marker so <c>WebApplicationFactory</c> can bind to the assembly.</summary>
public partial class Program;
