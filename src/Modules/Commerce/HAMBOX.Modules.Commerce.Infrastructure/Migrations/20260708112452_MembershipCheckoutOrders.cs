using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HAMBOX.Modules.Commerce.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MembershipCheckoutOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Kind",
                schema: "commerce",
                table: "Orders",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "MembershipAction",
                schema: "commerce",
                table: "Orders",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "MembershipPlanId",
                schema: "commerce",
                table: "Orders",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "MembershipSubscriptionId",
                schema: "commerce",
                table: "Orders",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ProductId",
                schema: "commerce",
                table: "OrderItems",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<string>(
                name: "LineItemType",
                schema: "commerce",
                table: "OrderItems",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "MembershipPlanId",
                schema: "commerce",
                table: "OrderItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OrderId",
                schema: "commerce",
                table: "MembershipTransactions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MembershipTransactions_OrderId",
                schema: "commerce",
                table: "MembershipTransactions",
                column: "OrderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MembershipTransactions_OrderId",
                schema: "commerce",
                table: "MembershipTransactions");

            migrationBuilder.DropColumn(
                name: "Kind",
                schema: "commerce",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "MembershipAction",
                schema: "commerce",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "MembershipPlanId",
                schema: "commerce",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "MembershipSubscriptionId",
                schema: "commerce",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "LineItemType",
                schema: "commerce",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "MembershipPlanId",
                schema: "commerce",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "OrderId",
                schema: "commerce",
                table: "MembershipTransactions");

            migrationBuilder.AlterColumn<Guid>(
                name: "ProductId",
                schema: "commerce",
                table: "OrderItems",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);
        }
    }
}
