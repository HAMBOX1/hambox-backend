using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace HAMBOX.Modules.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PromotionPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "identity",
                table: "PermissionGroups",
                columns: new[] { "Id", "CreatedOnUtc", "Description", "Key", "ModifiedOnUtc", "Module", "Name", "SortOrder" },
                values: new object[] { new Guid("15000000-0000-0000-0000-000000000024"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Promotions", null, "Commerce", "Promotions", 135 });

            migrationBuilder.InsertData(
                schema: "identity",
                table: "Permissions",
                columns: new[] { "Id", "CreatedOnUtc", "Description", "GroupId", "ModifiedOnUtc", "Name", "NormalizedName", "SortOrder" },
                values: new object[,]
                {
                    { new Guid("20000000-0000-0000-0000-000000000104"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new Guid("15000000-0000-0000-0000-000000000013"), null, "Coupons.Generate", "COUPONS.GENERATE", 5 },
                    { new Guid("20000000-0000-0000-0000-000000000105"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new Guid("15000000-0000-0000-0000-000000000013"), null, "Coupons.Export", "COUPONS.EXPORT", 6 },
                    { new Guid("20000000-0000-0000-0000-000000000106"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new Guid("15000000-0000-0000-0000-000000000013"), null, "Coupons.Import", "COUPONS.IMPORT", 7 },
                    { new Guid("20000000-0000-0000-0000-000000000210"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new Guid("15000000-0000-0000-0000-000000000024"), null, "Promotions.View", "PROMOTIONS.VIEW", 1 },
                    { new Guid("20000000-0000-0000-0000-000000000211"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new Guid("15000000-0000-0000-0000-000000000024"), null, "Promotions.Create", "PROMOTIONS.CREATE", 2 },
                    { new Guid("20000000-0000-0000-0000-000000000212"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new Guid("15000000-0000-0000-0000-000000000024"), null, "Promotions.Edit", "PROMOTIONS.EDIT", 3 },
                    { new Guid("20000000-0000-0000-0000-000000000213"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new Guid("15000000-0000-0000-0000-000000000024"), null, "Promotions.Delete", "PROMOTIONS.DELETE", 4 },
                    { new Guid("20000000-0000-0000-0000-000000000214"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new Guid("15000000-0000-0000-0000-000000000024"), null, "Promotions.Publish", "PROMOTIONS.PUBLISH", 5 }
                });

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000032"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000104"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000033"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000105"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000034"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000106"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000035"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000210"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000036"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000211"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000037"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000212"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000038"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000213"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000039"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000214"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000040"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000110"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000041"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000120"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000042"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000121"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000043"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000130"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000044"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000131"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000045"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000140"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000046"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000141"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000047"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000150"));

            migrationBuilder.InsertData(
                schema: "identity",
                table: "RolePermissions",
                columns: new[] { "Id", "CreatedOnUtc", "ModifiedOnUtc", "PermissionId", "RoleId" },
                values: new object[,]
                {
                    { new Guid("30000000-0000-0000-0000-000000000048"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new Guid("20000000-0000-0000-0000-000000000160"), new Guid("10000000-0000-0000-0000-000000000002") },
                    { new Guid("30000000-0000-0000-0000-000000000049"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new Guid("20000000-0000-0000-0000-000000000170"), new Guid("10000000-0000-0000-0000-000000000002") },
                    { new Guid("30000000-0000-0000-0000-000000000050"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new Guid("20000000-0000-0000-0000-000000000171"), new Guid("10000000-0000-0000-0000-000000000002") },
                    { new Guid("30000000-0000-0000-0000-000000000051"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new Guid("20000000-0000-0000-0000-000000000180"), new Guid("10000000-0000-0000-0000-000000000002") },
                    { new Guid("30000000-0000-0000-0000-000000000052"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new Guid("20000000-0000-0000-0000-000000000181"), new Guid("10000000-0000-0000-0000-000000000002") },
                    { new Guid("30000000-0000-0000-0000-000000000053"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new Guid("20000000-0000-0000-0000-000000000190"), new Guid("10000000-0000-0000-0000-000000000002") },
                    { new Guid("30000000-0000-0000-0000-000000000054"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new Guid("20000000-0000-0000-0000-000000000191"), new Guid("10000000-0000-0000-0000-000000000002") },
                    { new Guid("30000000-0000-0000-0000-000000000055"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new Guid("20000000-0000-0000-0000-000000000200"), new Guid("10000000-0000-0000-0000-000000000002") }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "identity",
                table: "PermissionGroups",
                keyColumn: "Id",
                keyValue: new Guid("15000000-0000-0000-0000-000000000024"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000104"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000105"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000106"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000210"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000211"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000212"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000213"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000214"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000048"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000049"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000050"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000051"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000052"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000053"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000054"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000055"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000032"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000110"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000033"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000120"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000034"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000121"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000035"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000130"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000036"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000131"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000037"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000140"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000038"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000141"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000039"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000150"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000040"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000160"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000041"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000170"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000042"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000171"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000043"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000180"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000044"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000181"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000045"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000190"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000046"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000191"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000047"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000200"));
        }
    }
}
