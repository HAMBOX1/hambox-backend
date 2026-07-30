using HAMBOX.Modules.Support.Domain.Tickets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Support.Infrastructure.Configurations;

internal sealed class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    public void Configure(EntityTypeBuilder<Ticket> builder)
    {
        builder.ToTable("Tickets");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.TicketNumber).HasMaxLength(50).IsRequired();
        builder.Property(t => t.Subject).HasMaxLength(200).IsRequired();
        builder.Property(t => t.CustomerUserId).HasMaxLength(128).IsRequired();
        builder.Property(t => t.AssignedAgentUserId).HasMaxLength(128);
        builder.Property(t => t.RatingComment).HasMaxLength(2_000);
        builder.Property(t => t.CustomerCountry).HasMaxLength(100);
        builder.Property(t => t.CustomerBrowser).HasMaxLength(200);
        builder.Property(t => t.CustomerDevice).HasMaxLength(200);
        builder.Property(t => t.CustomerIpAddress).HasMaxLength(64);
        builder.Property(t => t.AiSummary).HasColumnType("nvarchar(max)");
        builder.Property(t => t.AiSentiment).HasMaxLength(50);
        builder.Property(t => t.CreatedBy).HasMaxLength(128);
        builder.Property(t => t.ModifiedBy).HasMaxLength(128);
        builder.Property(t => t.Status).HasConversion<int>();
        builder.Property(t => t.LastMessageByRole).HasConversion<int?>();

        builder.HasIndex(t => t.TicketNumber).IsUnique();
        builder.HasIndex(t => t.CustomerUserId);
        builder.HasIndex(t => t.AssignedAgentUserId);
        builder.HasIndex(t => t.Status);
        builder.HasIndex(t => t.CreatedOnUtc);

        builder.Ignore(t => t.DomainEvents);
    }
}

internal sealed class TicketMessageConfiguration : IEntityTypeConfiguration<TicketMessage>
{
    public void Configure(EntityTypeBuilder<TicketMessage> builder)
    {
        builder.ToTable("TicketMessages");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.AuthorUserId).HasMaxLength(128).IsRequired();
        builder.Property(m => m.AuthorRole).HasConversion<int>();
        builder.Property(m => m.Body).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(m => m.CreatedBy).HasMaxLength(128);
        builder.Property(m => m.ModifiedBy).HasMaxLength(128);

        builder.HasIndex(m => new { m.TicketId, m.CreatedOnUtc });
    }
}

internal sealed class TicketAttachmentConfiguration : IEntityTypeConfiguration<TicketAttachment>
{
    public void Configure(EntityTypeBuilder<TicketAttachment> builder)
    {
        builder.ToTable("TicketAttachments");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.StorageKey).HasMaxLength(500).IsRequired();
        builder.Property(a => a.PublicUrl).HasMaxLength(500).IsRequired();
        builder.Property(a => a.FileName).HasMaxLength(260).IsRequired();
        builder.Property(a => a.ContentType).HasMaxLength(200).IsRequired();
        builder.Property(a => a.UploadedByUserId).HasMaxLength(128).IsRequired();
        builder.Property(a => a.ScanStatus).HasConversion<int>();
        builder.Property(a => a.CreatedBy).HasMaxLength(128);
        builder.Property(a => a.ModifiedBy).HasMaxLength(128);

        builder.HasIndex(a => a.TicketId);
        builder.HasIndex(a => a.MessageId);
    }
}

internal sealed class TicketParticipantConfiguration : IEntityTypeConfiguration<TicketParticipant>
{
    public void Configure(EntityTypeBuilder<TicketParticipant> builder)
    {
        builder.ToTable("TicketParticipants");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.UserId).HasMaxLength(128).IsRequired();
        builder.Property(p => p.Role).HasConversion<int>();

        builder.HasIndex(p => new { p.TicketId, p.UserId }).IsUnique();
    }
}

internal sealed class TicketAssignmentConfiguration : IEntityTypeConfiguration<TicketAssignment>
{
    public void Configure(EntityTypeBuilder<TicketAssignment> builder)
    {
        builder.ToTable("TicketAssignments");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.FromAgentUserId).HasMaxLength(128);
        builder.Property(a => a.ToAgentUserId).HasMaxLength(128).IsRequired();
        builder.Property(a => a.AssignedByUserId).HasMaxLength(128).IsRequired();

        builder.HasIndex(a => a.TicketId);
    }
}

internal sealed class TicketStatusHistoryConfiguration : IEntityTypeConfiguration<TicketStatusHistory>
{
    public void Configure(EntityTypeBuilder<TicketStatusHistory> builder)
    {
        builder.ToTable("TicketStatusHistories");
        builder.HasKey(h => h.Id);
        builder.Property(h => h.FromStatus).HasConversion<int>();
        builder.Property(h => h.ToStatus).HasConversion<int>();
        builder.Property(h => h.ChangedByUserId).HasMaxLength(128).IsRequired();

        builder.HasIndex(h => new { h.TicketId, h.CreatedOnUtc });
    }
}

internal sealed class TicketAuditLogConfiguration : IEntityTypeConfiguration<TicketAuditLog>
{
    public void Configure(EntityTypeBuilder<TicketAuditLog> builder)
    {
        builder.ToTable("TicketAuditLogs");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Action).HasConversion<int>();
        builder.Property(l => l.ActorUserId).HasMaxLength(128);
        builder.Property(l => l.DetailsJson).HasColumnType("nvarchar(max)");

        builder.HasIndex(l => new { l.TicketId, l.CreatedOnUtc });
    }
}

internal sealed class TicketTagConfiguration : IEntityTypeConfiguration<TicketTag>
{
    public void Configure(EntityTypeBuilder<TicketTag> builder)
    {
        builder.ToTable("TicketTags");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Name).HasMaxLength(50).IsRequired();
        builder.Property(t => t.Color).HasMaxLength(20).IsRequired();
        builder.Property(t => t.CreatedBy).HasMaxLength(128);
        builder.Property(t => t.ModifiedBy).HasMaxLength(128);

        builder.HasIndex(t => t.Name).IsUnique();
        builder.Ignore(t => t.DomainEvents);
    }
}

internal sealed class TicketTagAssignmentConfiguration : IEntityTypeConfiguration<TicketTagAssignment>
{
    public void Configure(EntityTypeBuilder<TicketTagAssignment> builder)
    {
        builder.ToTable("TicketTagAssignments");
        builder.HasKey(a => a.Id);

        builder.HasIndex(a => new { a.TicketId, a.TagId }).IsUnique();
    }
}

internal sealed class TicketCategoryConfiguration : IEntityTypeConfiguration<TicketCategory>
{
    public void Configure(EntityTypeBuilder<TicketCategory> builder)
    {
        builder.ToTable("TicketCategories");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Name).HasMaxLength(50).IsRequired();
        builder.Property(c => c.Color).HasMaxLength(20).IsRequired();
        builder.Property(c => c.Icon).HasMaxLength(50).IsRequired();
        builder.Property(c => c.CreatedBy).HasMaxLength(128);
        builder.Property(c => c.ModifiedBy).HasMaxLength(128);

        builder.HasIndex(c => c.Name).IsUnique();
        builder.Ignore(c => c.DomainEvents);
    }
}

internal sealed class TicketPriorityConfiguration : IEntityTypeConfiguration<TicketPriority>
{
    public void Configure(EntityTypeBuilder<TicketPriority> builder)
    {
        builder.ToTable("TicketPriorities");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Name).HasMaxLength(50).IsRequired();
        builder.Property(p => p.Color).HasMaxLength(20).IsRequired();
        builder.Property(p => p.CreatedBy).HasMaxLength(128);
        builder.Property(p => p.ModifiedBy).HasMaxLength(128);

        builder.HasIndex(p => p.Name).IsUnique();
        builder.Ignore(p => p.DomainEvents);
    }
}
