using HAMBOX.Modules.Support.Domain.KnowledgeBase;
using HAMBOX.Modules.Support.Domain.SavedReplies;
using HAMBOX.Modules.Support.Domain.Tickets;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Support.Application.Abstractions;

public interface ISupportDbContext
{
    DbSet<Ticket> Tickets { get; }
    DbSet<TicketMessage> TicketMessages { get; }
    DbSet<TicketAttachment> TicketAttachments { get; }
    DbSet<TicketParticipant> TicketParticipants { get; }
    DbSet<TicketAssignment> TicketAssignments { get; }
    DbSet<TicketStatusHistory> TicketStatusHistories { get; }
    DbSet<TicketAuditLog> TicketAuditLogs { get; }
    DbSet<TicketTag> TicketTags { get; }
    DbSet<TicketTagAssignment> TicketTagAssignments { get; }
    DbSet<TicketCategory> TicketCategories { get; }
    DbSet<TicketPriority> TicketPriorities { get; }

    DbSet<KnowledgeCategory> KnowledgeCategories { get; }
    DbSet<KnowledgeArticle> KnowledgeArticles { get; }

    DbSet<SavedReplyFolder> SavedReplyFolders { get; }
    DbSet<SavedReply> SavedReplies { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
