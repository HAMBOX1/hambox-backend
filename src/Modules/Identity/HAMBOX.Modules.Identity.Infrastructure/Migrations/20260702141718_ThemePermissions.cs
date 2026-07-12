using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace HAMBOX.Modules.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ThemePermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                schema: "identity",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000110"),
                columns: new[] { "Name", "NormalizedName" },
                values: new object[] { "Themes.View", "THEMES.VIEW" });

            migrationBuilder.InsertData(
                schema: "identity",
                table: "Permissions",
                columns: new[] { "Id", "CreatedOnUtc", "Description", "GroupId", "ModifiedOnUtc", "Name", "NormalizedName", "SortOrder" },
                values: new object[,]
                {
                    { new Guid("20000000-0000-0000-0000-000000000111"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new Guid("15000000-0000-0000-0000-000000000014"), null, "Themes.Create", "THEMES.CREATE", 2 },
                    { new Guid("20000000-0000-0000-0000-000000000112"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new Guid("15000000-0000-0000-0000-000000000014"), null, "Themes.Edit", "THEMES.EDIT", 3 },
                    { new Guid("20000000-0000-0000-0000-000000000113"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new Guid("15000000-0000-0000-0000-000000000014"), null, "Themes.Delete", "THEMES.DELETE", 4 },
                    { new Guid("20000000-0000-0000-0000-000000000114"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new Guid("15000000-0000-0000-0000-000000000014"), null, "Themes.Publish", "THEMES.PUBLISH", 5 },
                    { new Guid("20000000-0000-0000-0000-000000000115"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new Guid("15000000-0000-0000-0000-000000000014"), null, "Themes.Schedule", "THEMES.SCHEDULE", 6 },
                    { new Guid("20000000-0000-0000-0000-000000000116"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new Guid("15000000-0000-0000-0000-000000000014"), null, "Themes.Assign", "THEMES.ASSIGN", 7 },
                    { new Guid("20000000-0000-0000-0000-000000000117"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new Guid("15000000-0000-0000-0000-000000000014"), null, "Themes.Export", "THEMES.EXPORT", 8 },
                    { new Guid("20000000-0000-0000-0000-000000000118"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new Guid("15000000-0000-0000-0000-000000000014"), null, "Themes.Import", "THEMES.IMPORT", 9 },
                    { new Guid("20000000-0000-0000-0000-000000000119"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new Guid("15000000-0000-0000-0000-000000000014"), null, "Themes.Rollback", "THEMES.ROLLBACK", 10 }
                });

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

            migrationBuilder.InsertData(
                schema: "identity",
                table: "RolePermissions",
                columns: new[] { "Id", "CreatedOnUtc", "ModifiedOnUtc", "PermissionId", "RoleId" },
                values: new object[,]
                {
                    { new Guid("30000000-0000-0000-0000-000000000062"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new Guid("20000000-0000-0000-0000-000000000150"), new Guid("10000000-0000-0000-0000-000000000002") },
                    { new Guid("30000000-0000-0000-0000-000000000063"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new Guid("20000000-0000-0000-0000-000000000160"), new Guid("10000000-0000-0000-0000-000000000002") },
                    { new Guid("30000000-0000-0000-0000-000000000064"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new Guid("20000000-0000-0000-0000-000000000170"), new Guid("10000000-0000-0000-0000-000000000002") },
                    { new Guid("30000000-0000-0000-0000-000000000065"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new Guid("20000000-0000-0000-0000-000000000171"), new Guid("10000000-0000-0000-0000-000000000002") },
                    { new Guid("30000000-0000-0000-0000-000000000066"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new Guid("20000000-0000-0000-0000-000000000180"), new Guid("10000000-0000-0000-0000-000000000002") },
                    { new Guid("30000000-0000-0000-0000-000000000067"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new Guid("20000000-0000-0000-0000-000000000181"), new Guid("10000000-0000-0000-0000-000000000002") },
                    { new Guid("30000000-0000-0000-0000-000000000068"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new Guid("20000000-0000-0000-0000-000000000190"), new Guid("10000000-0000-0000-0000-000000000002") },
                    { new Guid("30000000-0000-0000-0000-000000000069"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new Guid("20000000-0000-0000-0000-000000000191"), new Guid("10000000-0000-0000-0000-000000000002") },
                    { new Guid("30000000-0000-0000-0000-000000000070"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new Guid("20000000-0000-0000-0000-000000000200"), new Guid("10000000-0000-0000-0000-000000000002") }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "identity",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000111"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000112"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000113"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000114"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000115"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000116"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000117"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000118"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000119"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000062"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000063"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000064"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000065"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000066"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000067"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000068"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000069"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000070"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000110"),
                columns: new[] { "Name", "NormalizedName" },
                values: new object[] { "Themes.Manage", "THEMES.MANAGE" });

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000047"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000120"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000048"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000121"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000049"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000130"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000050"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000131"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000051"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000140"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000052"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000141"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000053"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000150"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000054"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000160"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000055"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000170"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000056"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000171"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000057"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000180"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000058"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000181"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000059"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000190"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000060"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000191"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000061"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000200"));
        }
    }
}
