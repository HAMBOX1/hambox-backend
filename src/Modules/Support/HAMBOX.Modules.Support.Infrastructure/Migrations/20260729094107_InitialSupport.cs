using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HAMBOX.Modules.Support.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "support");

            migrationBuilder.CreateTable(
                name: "KnowledgeArticles",
                schema: "support",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(220)", maxLength: 220, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Visibility = table.Column<int>(type: "int", nullable: false),
                    ViewCount = table.Column<int>(type: "int", nullable: false),
                    RelatedArticleIdsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PublishedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnowledgeArticles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KnowledgeCategories",
                schema: "support",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnowledgeCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SavedReplies",
                schema: "support",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FolderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UsageCount = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedReplies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SavedReplyFolders",
                schema: "support",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedReplyFolders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TicketAssignments",
                schema: "support",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromAgentUserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ToAgentUserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    AssignedByUserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketAssignments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TicketAttachments",
                schema: "support",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StorageKey = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    PublicUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    UploadedByUserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ScanStatus = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketAttachments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TicketAuditLogs",
                schema: "support",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<int>(type: "int", nullable: false),
                    ActorUserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    DetailsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketAuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TicketCategories",
                schema: "support",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Color = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Icon = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TicketMessages",
                schema: "support",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuthorUserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    AuthorRole = table.Column<int>(type: "int", nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsInternal = table.Column<bool>(type: "bit", nullable: false),
                    IsDelivered = table.Column<bool>(type: "bit", nullable: false),
                    DeliveredOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsRead = table.Column<bool>(type: "bit", nullable: false),
                    ReadOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SavedReplyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TicketParticipants",
                schema: "support",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Role = table.Column<int>(type: "int", nullable: false),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketParticipants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TicketPriorities",
                schema: "support",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Color = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Level = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    SlaFirstResponseMinutes = table.Column<int>(type: "int", nullable: true),
                    SlaResolutionMinutes = table.Column<int>(type: "int", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketPriorities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tickets",
                schema: "support",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TicketNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CustomerUserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PriorityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AssignedAgentUserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    AssignedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    FirstResponseOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ResolvedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ClosedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReopenedCount = table.Column<int>(type: "int", nullable: false),
                    RatingScore = table.Column<int>(type: "int", nullable: true),
                    RatingComment = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    MergedIntoTicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RelatedOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RelatedProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CustomerCountry = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CustomerBrowser = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CustomerDevice = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CustomerIpAddress = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    LastMessageOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastMessageByRole = table.Column<int>(type: "int", nullable: true),
                    LastCustomerMessageOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastAgentMessageOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    AiSummary = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AiSentiment = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    AiSuggestedCategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AiSuggestedPriorityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tickets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TicketStatusHistories",
                schema: "support",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromStatus = table.Column<int>(type: "int", nullable: false),
                    ToStatus = table.Column<int>(type: "int", nullable: false),
                    ChangedByUserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketStatusHistories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TicketTagAssignments",
                schema: "support",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TagId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketTagAssignments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TicketTags",
                schema: "support",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Color = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketTags", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeArticles_CategoryId",
                schema: "support",
                table: "KnowledgeArticles",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeArticles_Slug",
                schema: "support",
                table: "KnowledgeArticles",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeArticles_Status",
                schema: "support",
                table: "KnowledgeArticles",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeCategories_Slug",
                schema: "support",
                table: "KnowledgeCategories",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SavedReplies_FolderId",
                schema: "support",
                table: "SavedReplies",
                column: "FolderId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketAssignments_TicketId",
                schema: "support",
                table: "TicketAssignments",
                column: "TicketId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketAttachments_MessageId",
                schema: "support",
                table: "TicketAttachments",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketAttachments_TicketId",
                schema: "support",
                table: "TicketAttachments",
                column: "TicketId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketAuditLogs_TicketId_CreatedOnUtc",
                schema: "support",
                table: "TicketAuditLogs",
                columns: new[] { "TicketId", "CreatedOnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TicketCategories_Name",
                schema: "support",
                table: "TicketCategories",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TicketMessages_TicketId_CreatedOnUtc",
                schema: "support",
                table: "TicketMessages",
                columns: new[] { "TicketId", "CreatedOnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TicketParticipants_TicketId_UserId",
                schema: "support",
                table: "TicketParticipants",
                columns: new[] { "TicketId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TicketPriorities_Name",
                schema: "support",
                table: "TicketPriorities",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_AssignedAgentUserId",
                schema: "support",
                table: "Tickets",
                column: "AssignedAgentUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_CreatedOnUtc",
                schema: "support",
                table: "Tickets",
                column: "CreatedOnUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_CustomerUserId",
                schema: "support",
                table: "Tickets",
                column: "CustomerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_Status",
                schema: "support",
                table: "Tickets",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_TicketNumber",
                schema: "support",
                table: "Tickets",
                column: "TicketNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TicketStatusHistories_TicketId_CreatedOnUtc",
                schema: "support",
                table: "TicketStatusHistories",
                columns: new[] { "TicketId", "CreatedOnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TicketTagAssignments_TicketId_TagId",
                schema: "support",
                table: "TicketTagAssignments",
                columns: new[] { "TicketId", "TagId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TicketTags_Name",
                schema: "support",
                table: "TicketTags",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KnowledgeArticles",
                schema: "support");

            migrationBuilder.DropTable(
                name: "KnowledgeCategories",
                schema: "support");

            migrationBuilder.DropTable(
                name: "SavedReplies",
                schema: "support");

            migrationBuilder.DropTable(
                name: "SavedReplyFolders",
                schema: "support");

            migrationBuilder.DropTable(
                name: "TicketAssignments",
                schema: "support");

            migrationBuilder.DropTable(
                name: "TicketAttachments",
                schema: "support");

            migrationBuilder.DropTable(
                name: "TicketAuditLogs",
                schema: "support");

            migrationBuilder.DropTable(
                name: "TicketCategories",
                schema: "support");

            migrationBuilder.DropTable(
                name: "TicketMessages",
                schema: "support");

            migrationBuilder.DropTable(
                name: "TicketParticipants",
                schema: "support");

            migrationBuilder.DropTable(
                name: "TicketPriorities",
                schema: "support");

            migrationBuilder.DropTable(
                name: "Tickets",
                schema: "support");

            migrationBuilder.DropTable(
                name: "TicketStatusHistories",
                schema: "support");

            migrationBuilder.DropTable(
                name: "TicketTagAssignments",
                schema: "support");

            migrationBuilder.DropTable(
                name: "TicketTags",
                schema: "support");
        }
    }
}
