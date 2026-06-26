using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HAMBOX.Modules.Identity.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddUserPreferredCurrency : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "PreferredCurrency",
            table: "Users",
            type: "nvarchar(3)",
            maxLength: 3,
            nullable: false,
            defaultValue: "USD");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "PreferredCurrency",
            table: "Users");
    }
}
