using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace HAMBOX.Modules.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MembershipPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                schema: "identity",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000090"),
                column: "Description",
                value: "View membership plans and members");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000091"),
                columns: new[] { "Description", "SortOrder" },
                values: new object[] { "Edit membership plans", 3 });

            migrationBuilder.InsertData(
                schema: "identity",
                table: "Permissions",
                columns: new[] { "Id", "CreatedOnUtc", "Description", "GroupId", "ModifiedOnUtc", "Name", "NormalizedName", "SortOrder" },
                values: new object[,]
                {
                    { new Guid("20000000-0000-0000-0000-000000000092"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Create membership plans", new Guid("15000000-0000-0000-0000-000000000012"), null, "Memberships.Create", "MEMBERSHIPS.CREATE", 2 },
                    { new Guid("20000000-0000-0000-0000-000000000093"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Delete membership plans", new Guid("15000000-0000-0000-0000-000000000012"), null, "Memberships.Delete", "MEMBERSHIPS.DELETE", 4 },
                    { new Guid("20000000-0000-0000-0000-000000000094"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Assign memberships to users", new Guid("15000000-0000-0000-0000-000000000012"), null, "Memberships.Assign", "MEMBERSHIPS.ASSIGN", 5 },
                    { new Guid("20000000-0000-0000-0000-000000000095"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Renew member subscriptions", new Guid("15000000-0000-0000-0000-000000000012"), null, "Memberships.Renew", "MEMBERSHIPS.RENEW", 6 },
                    { new Guid("20000000-0000-0000-0000-000000000096"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Cancel member subscriptions", new Guid("15000000-0000-0000-0000-000000000012"), null, "Memberships.Cancel", "MEMBERSHIPS.CANCEL", 7 },
                    { new Guid("20000000-0000-0000-0000-000000000097"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Configure plan benefits", new Guid("15000000-0000-0000-0000-000000000012"), null, "Memberships.ConfigureBenefits", "MEMBERSHIPS.CONFIGUREBENEFITS", 8 }
                });

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

            migrationBuilder.InsertData(
                schema: "identity",
                table: "RolePermissions",
                columns: new[] { "Id", "CreatedOnUtc", "ModifiedOnUtc", "PermissionId", "RoleId" },
                values: new object[,]
                {
                    { new Guid("30000000-0000-0000-0000-000000000056"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new Guid("20000000-0000-0000-0000-000000000171"), new Guid("10000000-0000-0000-0000-000000000002") },
                    { new Guid("30000000-0000-0000-0000-000000000057"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new Guid("20000000-0000-0000-0000-000000000180"), new Guid("10000000-0000-0000-0000-000000000002") },
                    { new Guid("30000000-0000-0000-0000-000000000058"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new Guid("20000000-0000-0000-0000-000000000181"), new Guid("10000000-0000-0000-0000-000000000002") },
                    { new Guid("30000000-0000-0000-0000-000000000059"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new Guid("20000000-0000-0000-0000-000000000190"), new Guid("10000000-0000-0000-0000-000000000002") },
                    { new Guid("30000000-0000-0000-0000-000000000060"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new Guid("20000000-0000-0000-0000-000000000191"), new Guid("10000000-0000-0000-0000-000000000002") },
                    { new Guid("30000000-0000-0000-0000-000000000061"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new Guid("20000000-0000-0000-0000-000000000200"), new Guid("10000000-0000-0000-0000-000000000002") }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "identity",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000092"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000093"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000094"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000095"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000096"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000097"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000056"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000057"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000058"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000059"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000060"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000061"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000090"),
                column: "Description",
                value: null);

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000091"),
                columns: new[] { "Description", "SortOrder" },
                values: new object[] { null, 2 });

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000028"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000100"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000029"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000101"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000030"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000102"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000031"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000103"));

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

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000048"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000160"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000049"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000170"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000050"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000171"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000051"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000180"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000052"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000181"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000053"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000190"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000054"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000191"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000055"),
                column: "PermissionId",
                value: new Guid("20000000-0000-0000-0000-000000000200"));
        }
    }
}
