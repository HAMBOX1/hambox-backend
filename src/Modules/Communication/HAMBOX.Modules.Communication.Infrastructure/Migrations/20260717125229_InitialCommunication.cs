using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HAMBOX.Modules.Communication.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCommunication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "communication");

            migrationBuilder.CreateTable(
                name: "CommunicationAuditLogs",
                schema: "communication",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<int>(type: "int", nullable: false),
                    ActorUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    EntityType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EntityId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Details = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunicationAuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CommunicationMessages",
                schema: "communication",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Channel = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    TemplateKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RetryCount = table.Column<int>(type: "int", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    MetadataJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RelatedEntityType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RelatedEntityId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SentOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    FailedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunicationMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CommunicationPreferences",
                schema: "communication",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    EmailEnabled = table.Column<bool>(type: "bit", nullable: false),
                    InAppEnabled = table.Column<bool>(type: "bit", nullable: false),
                    MarketingEnabled = table.Column<bool>(type: "bit", nullable: false),
                    SecurityEnabled = table.Column<bool>(type: "bit", nullable: false),
                    OrderEnabled = table.Column<bool>(type: "bit", nullable: false),
                    MembershipEnabled = table.Column<bool>(type: "bit", nullable: false),
                    SupportEnabled = table.Column<bool>(type: "bit", nullable: false),
                    PromotionEnabled = table.Column<bool>(type: "bit", nullable: false),
                    GeneralEnabled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunicationPreferences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CommunicationProviderConfigs",
                schema: "communication",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ProviderType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Channel = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunicationProviderConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CommunicationTemplates",
                schema: "communication",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Channel = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    ActiveVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VersionCounter = table.Column<int>(type: "int", nullable: false),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunicationTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CommunicationTemplateVersions",
                schema: "communication",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    SubjectEn = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SubjectAr = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    BodyEn = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BodyAr = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VariablesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false),
                    PublishedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    PublishedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunicationTemplateVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommunicationTemplateVersions_CommunicationTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalSchema: "communication",
                        principalTable: "CommunicationTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationAuditLogs_CreatedOnUtc",
                schema: "communication",
                table: "CommunicationAuditLogs",
                column: "CreatedOnUtc");

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationMessages_CorrelationId",
                schema: "communication",
                table: "CommunicationMessages",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationMessages_Status",
                schema: "communication",
                table: "CommunicationMessages",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationMessages_UserId_CreatedOnUtc",
                schema: "communication",
                table: "CommunicationMessages",
                columns: new[] { "UserId", "CreatedOnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationPreferences_UserId",
                schema: "communication",
                table: "CommunicationPreferences",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationProviderConfigs_Channel",
                schema: "communication",
                table: "CommunicationProviderConfigs",
                column: "Channel",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationTemplates_Key_Channel",
                schema: "communication",
                table: "CommunicationTemplates",
                columns: new[] { "Key", "Channel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationTemplateVersions_TemplateId_VersionNumber",
                schema: "communication",
                table: "CommunicationTemplateVersions",
                columns: new[] { "TemplateId", "VersionNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CommunicationAuditLogs",
                schema: "communication");

            migrationBuilder.DropTable(
                name: "CommunicationMessages",
                schema: "communication");

            migrationBuilder.DropTable(
                name: "CommunicationPreferences",
                schema: "communication");

            migrationBuilder.DropTable(
                name: "CommunicationProviderConfigs",
                schema: "communication");

            migrationBuilder.DropTable(
                name: "CommunicationTemplateVersions",
                schema: "communication");

            migrationBuilder.DropTable(
                name: "CommunicationTemplates",
                schema: "communication");
        }
    }
}
