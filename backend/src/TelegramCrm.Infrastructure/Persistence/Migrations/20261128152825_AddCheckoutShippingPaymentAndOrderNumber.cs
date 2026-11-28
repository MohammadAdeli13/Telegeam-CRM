using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TelegramCrm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCheckoutShippingPaymentAndOrderNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BankName",
                table: "Tenants",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CardHolderName",
                table: "Tenants",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CardNumber",
                table: "Tenants",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EnableCardToCard",
                table: "Tenants",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EnablePayOnDelivery",
                table: "Tenants",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "NextOrderNumber",
                table: "Tenants",
                type: "integer",
                nullable: false,
                defaultValue: 1001);

            migrationBuilder.AddColumn<int>(
                name: "PaymentExpiryMinutes",
                table: "Tenants",
                type: "integer",
                nullable: false,
                defaultValue: 60);

            migrationBuilder.AddColumn<bool>(
                name: "RequireOrderApproval",
                table: "Tenants",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "CarrierName",
                table: "Orders",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsShippingPayAtDestination",
                table: "Orders",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "OrderNumber",
                table: "Orders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PaymentExpiresAt",
                table: "Orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentMethod",
                table: "Orders",
                type: "character varying(24)",
                maxLength: 24,
                nullable: false,
                defaultValue: "None");

            migrationBuilder.AddColumn<string>(
                name: "PaymentRejectionReason",
                table: "Orders",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentStatus",
                table: "Orders",
                type: "character varying(24)",
                maxLength: 24,
                nullable: false,
                defaultValue: "NotRequired");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PaymentVerifiedAt",
                table: "Orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PaymentVerifiedByUserId",
                table: "Orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReceiptStoragePath",
                table: "Orders",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReceiptSubmittedAt",
                table: "Orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecipientName",
                table: "Orders",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecipientPhone",
                table: "Orders",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ShippedAt",
                table: "Orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShippingAddressSnapshot",
                table: "Orders",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ShippingAmount",
                table: "Orders",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ShippingCity",
                table: "Orders",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ShippingMethodId",
                table: "Orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShippingMethodNameSnapshot",
                table: "Orders",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShippingPostalCode",
                table: "Orders",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShippingProvince",
                table: "Orders",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SubtotalAmount",
                table: "Orders",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "TrackingCode",
                table: "Orders",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CustomerAddresses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    RecipientName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Province = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    City = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    AddressLine = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Plaque = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    PostalCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerAddresses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerAddresses_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ShippingMethods",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    FreeAboveAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    IsPayAtDestination = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    EstimatedDaysText = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShippingMethods", x => x.Id);
                    table.CheckConstraint("CK_ShippingMethods_FreeAboveAmount", "\"FreeAboveAmount\" IS NULL OR \"FreeAboveAmount\" >= 0");
                    table.CheckConstraint("CK_ShippingMethods_Price", "\"Price\" >= 0");
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Tenants_PaymentExpiryMinutes",
                table: "Tenants",
                sql: "\"PaymentExpiryMinutes\" >= 5 AND \"PaymentExpiryMinutes\" <= 10080");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_PaymentExpiresAt_Pending",
                table: "Orders",
                column: "PaymentExpiresAt",
                filter: "\"PaymentExpiresAt\" IS NOT NULL AND \"Status\" = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_TenantId_ShippingCity",
                table: "Orders",
                columns: new[] { "TenantId", "ShippingCity" });

            migrationBuilder.CreateIndex(
                name: "UX_Orders_TenantId_OrderNumber",
                table: "Orders",
                columns: new[] { "TenantId", "OrderNumber" },
                unique: true,
                filter: "\"OrderNumber\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Orders_PaymentMethod",
                table: "Orders",
                sql: "\"PaymentMethod\" IN ('None','PayOnDelivery','CardToCard')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Orders_PaymentStatus",
                table: "Orders",
                sql: "\"PaymentStatus\" IN ('NotRequired','AwaitingReceipt','ReceiptSubmitted','Verified','Rejected')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Orders_ShippingAmount",
                table: "Orders",
                sql: "\"ShippingAmount\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Orders_SubtotalAmount",
                table: "Orders",
                sql: "\"SubtotalAmount\" >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAddresses_TenantId",
                table: "CustomerAddresses",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAddresses_TenantId_CustomerId",
                table: "CustomerAddresses",
                columns: new[] { "TenantId", "CustomerId" });

            migrationBuilder.CreateIndex(
                name: "UX_CustomerAddresses_Default",
                table: "CustomerAddresses",
                column: "CustomerId",
                unique: true,
                filter: "\"IsDefault\"");

            migrationBuilder.CreateIndex(
                name: "IX_ShippingMethods_TenantId",
                table: "ShippingMethods",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ShippingMethods_TenantId_IsActive_SortOrder",
                table: "ShippingMethods",
                columns: new[] { "TenantId", "IsActive", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerAddresses");

            migrationBuilder.DropTable(
                name: "ShippingMethods");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Tenants_PaymentExpiryMinutes",
                table: "Tenants");

            migrationBuilder.DropIndex(
                name: "IX_Orders_PaymentExpiresAt_Pending",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_TenantId_ShippingCity",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "UX_Orders_TenantId_OrderNumber",
                table: "Orders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Orders_PaymentMethod",
                table: "Orders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Orders_PaymentStatus",
                table: "Orders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Orders_ShippingAmount",
                table: "Orders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Orders_SubtotalAmount",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "BankName",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "CardHolderName",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "CardNumber",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "EnableCardToCard",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "EnablePayOnDelivery",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "NextOrderNumber",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "PaymentExpiryMinutes",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "RequireOrderApproval",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "CarrierName",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "IsShippingPayAtDestination",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "OrderNumber",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PaymentExpiresAt",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PaymentMethod",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PaymentRejectionReason",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PaymentStatus",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PaymentVerifiedAt",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PaymentVerifiedByUserId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ReceiptStoragePath",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ReceiptSubmittedAt",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "RecipientName",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "RecipientPhone",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ShippedAt",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ShippingAddressSnapshot",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ShippingAmount",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ShippingCity",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ShippingMethodId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ShippingMethodNameSnapshot",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ShippingPostalCode",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ShippingProvince",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "SubtotalAmount",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "TrackingCode",
                table: "Orders");
        }
    }
}
