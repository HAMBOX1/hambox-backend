using HAMBOX.Modules.Content.Domain.Faqs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Content.Infrastructure.Configurations.Faqs;

internal sealed class FaqConfiguration : IEntityTypeConfiguration<Faq>
{
    public void Configure(EntityTypeBuilder<Faq> builder)
    {
        builder.ToTable("Faqs");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.QuestionEn).HasMaxLength(500).IsRequired();
        builder.Property(f => f.QuestionAr).HasMaxLength(500);
        builder.Property(f => f.AnswerEn).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(f => f.AnswerAr).HasColumnType("nvarchar(max)");
        builder.Property(f => f.Scope).HasConversion<int>().IsRequired();
        builder.Property(f => f.CreatedBy).HasMaxLength(128);
        builder.Property(f => f.ModifiedBy).HasMaxLength(128);

        // No FK to FaqCategories — mirrors the rest of the codebase's "no cross-aggregate FK
        // constraints, application-enforced only" convention (CLAUDE.md §3 "Repository conventions").
        builder.HasIndex(f => f.CategoryId);
        builder.HasIndex(f => new { f.Scope, f.TargetId });
        builder.HasIndex(f => f.IsPublished);

        builder.Ignore(f => f.DomainEvents);
    }
}

internal sealed class FaqCategoryConfiguration : IEntityTypeConfiguration<FaqCategory>
{
    public void Configure(EntityTypeBuilder<FaqCategory> builder)
    {
        builder.ToTable("FaqCategories");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.NameEn).HasMaxLength(100).IsRequired();
        builder.Property(c => c.NameAr).HasMaxLength(100);
        builder.Property(c => c.Slug).HasMaxLength(120).IsRequired();
        builder.Property(c => c.CreatedBy).HasMaxLength(128);
        builder.Property(c => c.ModifiedBy).HasMaxLength(128);

        builder.Ignore(c => c.DomainEvents);
    }
}

internal sealed class FaqAuditLogConfiguration : IEntityTypeConfiguration<FaqAuditLog>
{
    public void Configure(EntityTypeBuilder<FaqAuditLog> builder)
    {
        builder.ToTable("FaqAuditLogs");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Action).HasConversion<int>();
        builder.Property(l => l.ActorUserId).HasMaxLength(128);
        builder.Property(l => l.DetailsJson).HasColumnType("nvarchar(max)");

        builder.HasIndex(l => new { l.FaqId, l.CreatedOnUtc });
    }
}
