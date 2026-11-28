using System.Text.Json;
using FluentValidation;
using TelegramCrm.Application.Common;

namespace TelegramCrm.Application.Merchant;

/// <summary>Validates <c>PATCH /merchant/shop</c>.</summary>
public sealed class UpdateMerchantShopRequestValidator : AbstractValidator<UpdateMerchantShopRequest>
{
    public UpdateMerchantShopRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(120)
            .When(x => x.Name is not null);

        RuleFor(x => x.DefaultLanguage)
            .Must(v => v is "fa" or "en")
            .WithMessage("DefaultLanguage must be 'fa' or 'en'.")
            .When(x => x.DefaultLanguage is not null);

        RuleFor(x => x.Currency)
            .Must(ShopCurrencies.IsSupported)
            .WithMessage("Currency must be one of: IRT, IRR, USD, EUR.")
            .When(x => x.Currency is not null);
    }
}

/// <summary>Validates <c>POST /merchant/categories</c> (contract v1.2 §4.3).</summary>
public sealed class CreateCategoryRequestValidator : AbstractValidator<CreateCategoryRequest>
{
    public CreateCategoryRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.NameEn).MaximumLength(120);
        RuleFor(x => x.Slug)
            .NotEmpty()
            .MaximumLength(40)
            .Matches("^[a-z0-9-]+$")
            .WithMessage("Slug may only contain lowercase letters, digits and hyphens.");
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0).When(x => x.SortOrder.HasValue);
    }
}

/// <summary>Validates <c>PATCH /merchant/categories/{id}</c>.</summary>
public sealed class UpdateCategoryRequestValidator : AbstractValidator<UpdateCategoryRequest>
{
    public UpdateCategoryRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120).When(x => x.Name is not null);
        RuleFor(x => x.NameEn).MaximumLength(120);
        RuleFor(x => x.Slug)
            .MaximumLength(40)
            .Matches("^[a-z0-9-]+$")
            .WithMessage("Slug may only contain lowercase letters, digits and hyphens.")
            .When(x => x.Slug is not null);
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0).When(x => x.SortOrder.HasValue);
    }
}

/// <summary>Validates <c>POST /merchant/products</c>.</summary>
public sealed class CreateProductRequestValidator : AbstractValidator<CreateProductRequest>
{
    public CreateProductRequestValidator()
    {
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameEn).MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(4000);
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0m);
        RuleFor(x => x.Stock).GreaterThanOrEqualTo(0);
    }
}

/// <summary>Validates <c>PATCH /merchant/products/{id}</c>.</summary>
public sealed class UpdateProductRequestValidator : AbstractValidator<UpdateProductRequest>
{
    public UpdateProductRequestValidator()
    {
        RuleFor(x => x.CategoryId).NotEmpty().When(x => x.CategoryId.HasValue);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200).When(x => x.Name is not null);
        RuleFor(x => x.NameEn).MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(4000);
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0m).When(x => x.Price.HasValue);
    }
}

/// <summary>Validates <c>POST /merchant/products/{id}/stock-adjust</c>.</summary>
public sealed class StockAdjustRequestValidator : AbstractValidator<StockAdjustRequest>
{
    public StockAdjustRequestValidator()
    {
        RuleFor(x => x.Delta)
            .NotEqual(0)
            .WithMessage("Delta must be a non-zero integer.");
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}

/// <summary>Validates <c>PATCH /merchant/products/{id}/images/{imageId}</c>.</summary>
public sealed class UpdateProductImageRequestValidator : AbstractValidator<UpdateProductImageRequest>
{
    public UpdateProductImageRequestValidator()
    {
        RuleFor(x => x.AltText).MaximumLength(300);
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0).When(x => x.SortOrder.HasValue);
    }
}

/// <summary>Validates <c>POST /merchant/customers</c>.</summary>
public sealed class CreateCustomerRequestValidator : AbstractValidator<CreateCustomerRequest>
{
    public CreateCustomerRequestValidator()
    {
        RuleFor(x => x.TelegramUserId).GreaterThan(0);
        RuleFor(x => x.TelegramUsername)
            .MaximumLength(64)
            .Must(TelegramUsernameHelper.IsValid)
            .WithMessage("TelegramUsername must be 4-32 characters starting with a letter (letters, digits, underscore).")
            .When(x => !string.IsNullOrWhiteSpace(x.TelegramUsername));
        RuleFor(x => x.DisplayName).MaximumLength(200);
        RuleFor(x => x.Phone)
            .MaximumLength(20)
            .Matches(@"^\+[1-9]\d{6,14}$")
            .WithMessage("Phone must be E.164 (e.g. +989121234567).")
            .When(x => !string.IsNullOrWhiteSpace(x.Phone));
        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}

/// <summary>Validates <c>PATCH /merchant/customers/{id}</c>.</summary>
public sealed class UpdateCustomerRequestValidator : AbstractValidator<UpdateCustomerRequest>
{
    public UpdateCustomerRequestValidator()
    {
        RuleFor(x => x.TelegramUsername)
            .MaximumLength(64)
            .Must(TelegramUsernameHelper.IsValid)
            .WithMessage("TelegramUsername must be 4-32 characters starting with a letter (letters, digits, underscore).")
            .When(x => !string.IsNullOrWhiteSpace(x.TelegramUsername));
        RuleFor(x => x.DisplayName).MaximumLength(200);
        RuleFor(x => x.Phone)
            .MaximumLength(20)
            .Matches(@"^\+[1-9]\d{6,14}$")
            .WithMessage("Phone must be E.164 (e.g. +989121234567).")
            .When(x => !string.IsNullOrWhiteSpace(x.Phone));
        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}

/// <summary>Validates <c>POST /merchant/tags</c>.</summary>
public sealed class CreateTagRequestValidator : AbstractValidator<CreateTagRequest>
{
    public CreateTagRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(60);
    }
}

/// <summary>Validates <c>POST /merchant/customers/{id}/tags</c>.</summary>
public sealed class AttachTagRequestValidator : AbstractValidator<AttachTagRequest>
{
    public AttachTagRequestValidator()
    {
        RuleFor(x => x.TagId).NotEmpty();
    }
}

/// <summary>Validates <c>PUT /merchant/settings</c>.</summary>
public sealed class UpdateSettingsRequestValidator : AbstractValidator<UpdateSettingsRequest>
{
    public UpdateSettingsRequestValidator()
    {
        RuleFor(x => x.Settings).NotNull();
        RuleForEach(x => x.Settings).ChildRules(setting =>
        {
            setting.RuleFor(s => s.Key).NotEmpty().MaximumLength(120);
            setting.RuleFor(s => s.Value)
                .NotEmpty()
                .Must(IsValidJson)
                .WithMessage("Setting value must be a valid JSON document.");
        });
        RuleFor(x => x.Settings)
            .Must(list => list.Select(s => s.Key).Distinct(StringComparer.Ordinal).Count() == list.Count)
            .WithMessage("Setting keys must be unique.")
            .When(x => x.Settings is not null);
    }

    private static bool IsValidJson(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            using var _ = JsonDocument.Parse(value);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
