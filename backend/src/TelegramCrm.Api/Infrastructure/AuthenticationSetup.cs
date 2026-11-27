using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TelegramCrm.Infrastructure.Auth;

namespace TelegramCrm.Api.Infrastructure;

/// <summary>Wires JWT bearer authentication and the Development-only DevAuth scheme.</summary>
public static class AuthenticationSetup
{
    public const string DevAuthScheme = "DevAuth";

    public static IServiceCollection AddApiAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var allowDevBypass = configuration.GetValue<bool>("Auth:AllowDevBypass");

        // Hard guard: the bypass can never be enabled in a deployed environment (§5.5).
        if (allowDevBypass && !environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                "Auth:AllowDevBypass may only be enabled when ASPNETCORE_ENVIRONMENT=Development.");
        }

        // Fail fast on a missing/short signing key, but bind the actual values lazily so
        // the bearer handler always sees the final configuration (including test overrides).
        var startupJwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
        if (string.IsNullOrWhiteSpace(startupJwt.Secret) || Encoding.UTF8.GetByteCount(startupJwt.Secret) < 32)
        {
            throw new InvalidOperationException(
                "Jwt:Secret is required and must be at least 32 bytes (set JWT_SECRET).");
        }

        var builder = services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        });

        builder.AddJwtBearer();

        services
            .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((options, jwtAccessor) =>
            {
                var jwt = jwtAccessor.Value;

                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = "sub",
                    RoleClaimType = JwtTokenService.RoleClaim,
                };
            });

        if (allowDevBypass && environment.IsDevelopment())
        {
            builder.AddScheme<AuthenticationSchemeOptions, DevAuthHandler>(DevAuthScheme, _ => { });
        }

        services.AddAuthorization(options =>
        {
            options.AddPolicy("Merchant", policy => policy
                .RequireAuthenticatedUser()
                .RequireAssertion(ctx => ctx.User.HasClaim(c => c.Type == JwtTokenService.TenantIdClaim)));

            options.AddPolicy("PlatformAdmin", policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim(JwtTokenService.RoleClaim, "PlatformAdmin"));
        });

        return services;
    }
}
