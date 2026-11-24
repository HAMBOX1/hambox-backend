using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HAMBOX.Modules.Commerce.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class StarsLedgerV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "OrderNumber",
                schema: "commerce",
                table: "PointsTransactions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<Guid>(
                name: "OrderId",
                schema: "commerce",
                table: "PointsTransactions",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<string>(
                name: "ActorUserId",
                schema: "commerce",
                table: "PointsTransactions",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AvailableOnUtc",
                schema: "commerce",
                table: "PointsTransactions",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ExpiresOnUtc",
                schema: "commerce",
                table: "PointsTransactions",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Reference",
                schema: "commerce",
                table: "PointsTransactions",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                schema: "commerce",
                table: "PointsTransactions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "StarsEarnRules",
                schema: "commerce",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Scope = table.Column<int>(type: "int", nullable: false),
                    ScopeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Mode = table.Column<int>(type: "int", nullable: false),
                    Value = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    StartsOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    EndsOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StarsEarnRules", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PointsTransactions_UserId_Status",
                schema: "commerce",
                table: "PointsTransactions",
                columns: new[] { "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_StarsEarnRules_Active_Scope",
                schema: "commerce",
                table: "StarsEarnRules",
                columns: new[] { "IsActive", "Scope", "ScopeId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StarsEarnRules",
                schema: "commerce");

            migrationBuilder.DropIndex(
                name: "IX_PointsTransactions_UserId_Status",
                schema: "commerce",
                table: "PointsTransactions");

            migrationBuilder.DropColumn(
                name: "ActorUserId",
                schema: "commerce",
                table: "PointsTransactions");

            migrationBuilder.DropColumn(
                name: "AvailableOnUtc",
                schema: "commerce",
                table: "PointsTransactions");

            migrationBuilder.DropColumn(
                name: "ExpiresOnUtc",
                schema: "commerce",
                table: "PointsTransactions");

            migrationBuilder.DropColumn(
                name: "Reference",
                schema: "commerce",
                table: "PointsTransactions");

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "commerce",
                table: "PointsTransactions");

            migrationBuilder.AlterColumn<string>(
                name: "OrderNumber",
                schema: "commerce",
                table: "PointsTransactions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "OrderId",
                schema: "commerce",
                table: "PointsTransactions",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);
        }
    }
}
