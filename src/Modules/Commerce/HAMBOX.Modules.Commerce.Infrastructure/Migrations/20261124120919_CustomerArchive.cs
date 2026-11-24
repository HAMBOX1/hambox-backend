using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HAMBOX.Modules.Commerce.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CustomerArchive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LegacyCustomerRecords",
                schema: "commerce",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LegacyId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RegisteredOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    PromisedStars = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    LinkedUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    LinkedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LinkedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    LinkReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ImportedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LegacyCustomerRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LegacyOrderRecords",
                schema: "commerce",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LegacyOrderId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LegacyCustomerId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    OrderedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LegacyOrderRecords", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LegacyCustomerRecords_Email",
                schema: "commerce",
                table: "LegacyCustomerRecords",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_LegacyCustomerRecords_LinkedUser",
                schema: "commerce",
                table: "LegacyCustomerRecords",
                column: "LinkedUserId");

            migrationBuilder.CreateIndex(
                name: "UX_LegacyCustomerRecords_LegacyId",
                schema: "commerce",
                table: "LegacyCustomerRecords",
                column: "LegacyId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LegacyOrderRecords_Customer",
                schema: "commerce",
                table: "LegacyOrderRecords",
                column: "LegacyCustomerId");

            migrationBuilder.CreateIndex(
                name: "UX_LegacyOrderRecords_LegacyOrderId",
                schema: "commerce",
                table: "LegacyOrderRecords",
                column: "LegacyOrderId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LegacyCustomerRecords",
                schema: "commerce");

            migrationBuilder.DropTable(
                name: "LegacyOrderRecords",
                schema: "commerce");
        }
    }
}
