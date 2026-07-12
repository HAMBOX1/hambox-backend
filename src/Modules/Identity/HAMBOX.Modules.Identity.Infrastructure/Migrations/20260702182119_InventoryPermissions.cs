using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace HAMBOX.Modules.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InventoryPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "identity",
                table: "Permissions",
                columns: new[] { "Id", "CreatedOnUtc", "Description", "GroupId", "ModifiedOnUtc", "Name", "NormalizedName", "SortOrder" },
                values: new object[,]
                {
                    { new Guid("20000000-0000-0000-0000-000000000032"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new Guid("15000000-0000-0000-0000-000000000005"), null, "Catalog.Inventory.Create", "CATALOG.INVENTORY.CREATE", 3 },
                    { new Guid("20000000-0000-0000-0000-000000000033"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new Guid("15000000-0000-0000-0000-000000000005"), null, "Catalog.Inventory.Delete", "CATALOG.INVENTORY.DELETE", 4 },
                    { new Guid("20000000-0000-0000-0000-000000000034"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new Guid("15000000-0000-0000-0000-000000000005"), null, "Catalog.Inventory.Import", "CATALOG.INVENTORY.IMPORT", 5 },
                    { new Guid("20000000-0000-0000-0000-000000000035"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new Guid("15000000-0000-0000-0000-000000000005"), null, "Catalog.Inventory.Export", "CATALOG.INVENTORY.EXPORT", 6 },
                    { new Guid("20000000-0000-0000-0000-000000000036"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new Guid("15000000-0000-0000-0000-000000000005"), null, "Catalog.Inventory.ManageCodes", "CATALOG.INVENTORY.MANAGECODES", 7 },
                    { new Guid("20000000-0000-0000-0000-000000000037"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new Guid("15000000-0000-0000-0000-000000000005"), null, "Catalog.Inventory.ViewCosts", "CATALOG.INVENTORY.VIEWCOSTS", 8 },
                    { new Guid("20000000-0000-0000-0000-000000000038"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new Guid("15000000-0000-0000-0000-000000000005"), null, "Catalog.Inventory.ManageBatches", "CATALOG.INVENTORY.MANAGEBATCHES", 9 },
                    { new Guid("20000000-0000-0000-0000-000000000039"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new Guid("15000000-0000-0000-0000-000000000005"), null, "Catalog.Inventory.ManageSuppliers", "CATALOG.INVENTORY.MANAGESUPPLIERS", 10 }
                });

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000012"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000032"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000013"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000033"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000014"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000034"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000015"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000035"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000016"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000036"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000017"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000037"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000018"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000038"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000019"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000039"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000020"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000040"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000021"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000041"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000022"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000042"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000023"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000050"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000024"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000051"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000025"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000060"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000026"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000061"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000027"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000062"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000028"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000070"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000029"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000071"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000030"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000072"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000031"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000073"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000032"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000074"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000033"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000080"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000034"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000090"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000035"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000091"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000036"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000092"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000037"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000093"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000038"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000094"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000039"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000095"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000040"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000096"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000041"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000097"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000042"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000100"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000043"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000101"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000044"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000102"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000045"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000103"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000046"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000104"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000047"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000105"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000048"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000106"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000049"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000210"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000050"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000211"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000051"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000212"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000052"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000213"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000053"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000214"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000054"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000110"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000055"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000111"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000056"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000112"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000057"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000113"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000058"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000114"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000059"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000115"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000060"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000116"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000061"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000117"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000062"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000118"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000063"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000119"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000064"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000120"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000065"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000121"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000066"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000130"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000067"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000131"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000068"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000140"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000069"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000141"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000070"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000150"));

            migrationBuilder.InsertData(
                schema: "identity",
                table: "RolePermissions",
                columns: new[] { "Id", "CreatedOnUtc", "ModifiedOnUtc", "PermissionId", "RoleId" },
                values: new object[,]
                {
                    { new Guid("30000000-0000-0000-0000-000000000071"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new Guid("20000000-0000-0000-0000-000000000160"), new Guid("10000000-0000-0000-0000-000000000002") },
                    { new Guid("30000000-0000-0000-0000-000000000072"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new Guid("20000000-0000-0000-0000-000000000170"), new Guid("10000000-0000-0000-0000-000000000002") },
                    { new Guid("30000000-0000-0000-0000-000000000073"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new Guid("20000000-0000-0000-0000-000000000171"), new Guid("10000000-0000-0000-0000-000000000002") },
                    { new Guid("30000000-0000-0000-0000-000000000074"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new Guid("20000000-0000-0000-0000-000000000180"), new Guid("10000000-0000-0000-0000-000000000002") },
                    { new Guid("30000000-0000-0000-0000-000000000075"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new Guid("20000000-0000-0000-0000-000000000181"), new Guid("10000000-0000-0000-0000-000000000002") },
                    { new Guid("30000000-0000-0000-0000-000000000076"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new Guid("20000000-0000-0000-0000-000000000190"), new Guid("10000000-0000-0000-0000-000000000002") },
                    { new Guid("30000000-0000-0000-0000-000000000077"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new Guid("20000000-0000-0000-0000-000000000191"), new Guid("10000000-0000-0000-0000-000000000002") },
                    { new Guid("30000000-0000-0000-0000-000000000078"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new Guid("20000000-0000-0000-0000-000000000200"), new Guid("10000000-0000-0000-0000-000000000002") }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "identity",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000032"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000033"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000034"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000035"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000036"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000037"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000038"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000039"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000071"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000072"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000073"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000074"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000075"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000076"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000077"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000078"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000012"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000040"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000013"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000041"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000014"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000042"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000015"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000050"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000016"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000051"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000017"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000060"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000018"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000061"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000019"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000062"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000020"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000070"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000021"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000071"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000022"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000072"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000023"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000073"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000024"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000074"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000025"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000080"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000026"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000090"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000027"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000091"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000028"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000092"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000029"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000093"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000030"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000094"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000031"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000095"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000032"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000096"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000033"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000097"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000034"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000100"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000035"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000101"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000036"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000102"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000037"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000103"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000038"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000104"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000039"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000105"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000040"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000106"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000041"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000210"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000042"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000211"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000043"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000212"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000044"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000213"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000045"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000214"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000046"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000110"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000047"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000111"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000048"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000112"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000049"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000113"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000050"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000114"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000051"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000115"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000052"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000116"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000053"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000117"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000054"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000118"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000055"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000119"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000056"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000120"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000057"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000121"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000058"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000130"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000059"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000131"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000060"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000140"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000061"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000141"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000062"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000150"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000063"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000160"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000064"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000170"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000065"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000171"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000066"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000180"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000067"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000181"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000068"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000190"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000069"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000191"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000070"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000200"));
        }
    }
}
