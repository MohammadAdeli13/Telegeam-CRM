namespace TelegramCrm.Infrastructure.Auth;

/// <summary>JWT signing configuration (contract v1.2 §5.2 / §5.8).</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "telegram-crm";

    public string Audience { get; set; } = "telegram-crm-web";

    /// <summary>HS256 signing key. Must be at least 32 bytes; validated at startup.</summary>
    public string Secret { get; set; } = string.Empty;
}
