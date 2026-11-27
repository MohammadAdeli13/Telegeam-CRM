using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TelegramCrm.Application.Common;
using TelegramCrm.Domain.Entities;

namespace TelegramCrm.Application.Telegram;

/// <summary>
/// Handles Telegram Bot webhook updates fast by queueing outbound replies into
/// <c>OutboxMessages</c> (contract v1.2 §4.5).
/// </summary>
public sealed class TelegramWebhookService
{
    public const string ShopDeepLinkPrefix = "shop_";

    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    private readonly IAppDbContext _db;
    private readonly IClock _clock;

    public TelegramWebhookService(IAppDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task HandleUpdateAsync(
        TelegramWebhookUpdate update,
        string miniAppBaseUrl,
        CancellationToken cancellationToken = default)
    {
        var message = update.Message;
        if (message?.Chat is null || string.IsNullOrWhiteSpace(message.Text))
        {
            return;
        }

        var trimmed = message.Text.Trim();
        if (!trimmed.StartsWith("/start", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var baseUrl = miniAppBaseUrl.TrimEnd('/');
        var parts = trimmed.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var startParam = parts.Length > 1 ? parts[1].Trim() : null;

        Guid? tenantId = null;
        string text;
        string buttonText;
        string webAppUrl;

        if (!string.IsNullOrEmpty(startParam) &&
            startParam.StartsWith(ShopDeepLinkPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var slug = startParam[ShopDeepLinkPrefix.Length..].Trim().ToLowerInvariant();
            var shop = await _db.Tenants
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Slug == slug && t.IsActive, cancellationToken)
                .ConfigureAwait(false);

            if (shop is not null)
            {
                tenantId = shop.Id;
                var isPersian = string.Equals(shop.DefaultLanguage, "fa", StringComparison.OrdinalIgnoreCase);
                text = isPersian
                    ? $"به فروشگاه {shop.Name} خوش آمدید! برای مشاهده محصولات و ثبت سفارش دکمه زیر را بزنید."
                    : $"Welcome to {shop.Name}! Tap below to browse products and place an order.";
                buttonText = isPersian ? "ورود به فروشگاه" : "Open Shop";
                webAppUrl = $"{baseUrl}/s/{shop.Slug}";
            }
            else
            {
                text = "فروشگاه مورد نظر یافت نشد. / Requested shop was not found.";
                buttonText = "Open App";
                webAppUrl = baseUrl;
            }
        }
        else
        {
            var isPersian = string.Equals(message.From?.LanguageCode, "fa", StringComparison.OrdinalIgnoreCase);
            text = isPersian
                ? "به سامانه CRM تلگرامی خوش آمدید! برای مدیریت فروشگاه یا مشاهده سفارش‌ها وارد وب‌اپ شوید."
                : "Welcome to Telegram CRM! Open the Mini App to manage your shop or view your orders.";
            buttonText = isPersian ? "باز کردن وب‌اپ" : "Open Mini App";
            webAppUrl = baseUrl;
        }

        var now = _clock.UtcNow;
        var payload = new BotReplyOutboxPayload(message.Chat.Id, text, buttonText, webAppUrl);

        _db.OutboxMessages.Add(new OutboxMessage
        {
            Type = OutboxEventTypes.BotReply,
            Payload = JsonSerializer.Serialize(payload, _json),
            OccurredAt = now,
            TenantId = tenantId,
            CreatedAt = now,
            UpdatedAt = now,
        });

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
