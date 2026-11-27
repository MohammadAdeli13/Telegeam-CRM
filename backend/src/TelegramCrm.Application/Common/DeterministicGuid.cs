using System.Security.Cryptography;
using System.Text;

namespace TelegramCrm.Application.Common;

/// <summary>
/// Derives a deterministic RFC 4122 v5-shaped <see cref="Guid"/> from
/// <c>(tenantId, customerId, idempotencyKey)</c> so <c>Orders.Id</c> (primary key) enforces
/// idempotent order creation atomically in PostgreSQL with zero schema changes.
/// </summary>
public static class DeterministicGuid
{
    public static Guid FromOrderIdempotencyKey(Guid tenantId, Guid customerId, string idempotencyKey)
    {
        var payload = $"order-idempotency:{tenantId:N}:{customerId:N}:{idempotencyKey}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        var bytes = hash[..16];

        // Set RFC 4122 version 5 and variant bits.
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);

        return new Guid(bytes);
    }
}
