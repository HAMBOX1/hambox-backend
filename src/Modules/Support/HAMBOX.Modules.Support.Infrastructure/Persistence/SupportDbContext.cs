using System.Linq.Expressions;
using HAMBOX.Domain.Entities;
using HAMBOX.Modules.Support.Application.Abstractions;
using HAMBOX.Modules.Support.Domain.KnowledgeBase;
using HAMBOX.Modules.Support.Domain.SavedReplies;
using HAMBOX.Modules.Support.Domain.Tickets;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Support.Infrastructure.Persistence;

public sealed class SupportDbContext(DbContextOptions<SupportDbContext> options)
    : DbContext(options), ISupportDbContext
{
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<TicketMessage> TicketMessages => Set<TicketMessage>();
    public DbSet<TicketAttachment> TicketAttachments => Set<TicketAttachment>();
    public DbSet<TicketParticipant> TicketParticipants => Set<TicketParticipant>();
    public DbSet<TicketAssignment> TicketAssignments => Set<TicketAssignment>();
    public DbSet<TicketStatusHistory> TicketStatusHistories => Set<TicketStatusHistory>();
    public DbSet<TicketAuditLog> TicketAuditLogs => Set<TicketAuditLog>();
    public DbSet<TicketTag> TicketTags => Set<TicketTag>();
    public DbSet<TicketTagAssignment> TicketTagAssignments => Set<TicketTagAssignment>();
    public DbSet<TicketCategory> TicketCategories => Set<TicketCategory>();
    public DbSet<TicketPriority> TicketPriorities => Set<TicketPriority>();

    public DbSet<KnowledgeCategory> KnowledgeCategories => Set<KnowledgeCategory>();
    public DbSet<KnowledgeArticle> KnowledgeArticles => Set<KnowledgeArticle>();

    public DbSet<SavedReplyFolder> SavedReplyFolders => Set<SavedReplyFolder>();
    public DbSet<SavedReply> SavedReplies => Set<SavedReply>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("support");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SupportDbContext).Assembly);

        ApplyGlobalQueryFilters(modelBuilder);
    }

    // Mirrors every other module's reflective soft-delete filter registration (CLAUDE.md §3/§13 —
    // known, accepted per-context duplication, not yet factored into a shared base).
    private static void ApplyGlobalQueryFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var property = Expression.Property(parameter, nameof(ISoftDeletable.IsDeleted));
            var condition = Expression.Equal(property, Expression.Constant(false));
            var lambda = Expression.Lambda(condition, parameter);

            modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);
        }
    }
}
