using HAMBOX.Modules.Communication.Domain.Communication;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Communication.Infrastructure.Configurations;

internal sealed class CommunicationMessageConfiguration : IEntityTypeConfiguration<CommunicationMessage>
{
    public void Configure(EntityTypeBuilder<CommunicationMessage> builder)
    {
        builder.ToTable("CommunicationMessages");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.UserId).HasMaxLength(450).IsRequired();
        builder.Property(m => m.Channel).HasMaxLength(50).IsRequired();
        builder.Property(m => m.Category).HasConversion<int>();
        builder.Property(m => m.Priority).HasConversion<int>();
        builder.Property(m => m.Status).HasConversion<int>();
        builder.Property(m => m.TemplateKey).HasMaxLength(100).IsRequired();
        builder.Property(m => m.Subject).HasMaxLength(500).IsRequired();
        builder.Property(m => m.Body).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(m => m.CorrelationId).HasMaxLength(100);
        builder.Property(m => m.LastError).HasMaxLength(2000);
        builder.Property(m => m.MetadataJson).HasColumnType("nvarchar(max)");
        builder.Property(m => m.RelatedEntityType).HasMaxLength(100);
        builder.Property(m => m.RelatedEntityId).HasMaxLength(100);

        builder.HasIndex(m => new { m.UserId, m.CreatedOnUtc });
        builder.HasIndex(m => m.Status);
        builder.HasIndex(m => m.CorrelationId);

        builder.Ignore(m => m.DomainEvents);
    }
}

internal sealed class CommunicationTemplateConfiguration : IEntityTypeConfiguration<CommunicationTemplate>
{
    public void Configure(EntityTypeBuilder<CommunicationTemplate> builder)
    {
        builder.ToTable("CommunicationTemplates");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Key).HasMaxLength(100).IsRequired();
        builder.Property(t => t.Channel).HasMaxLength(50).IsRequired();
        builder.Property(t => t.Category).HasConversion<int>();

        builder.HasIndex(t => new { t.Key, t.Channel }).IsUnique();

        builder.HasMany(t => t.Versions)
            .WithOne()
            .HasForeignKey(v => v.TemplateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata.FindNavigation(nameof(CommunicationTemplate.Versions))!.SetPropertyAccessMode(PropertyAccessMode.Field);
        builder.Ignore(t => t.DomainEvents);
    }
}

internal sealed class CommunicationTemplateVersionConfiguration : IEntityTypeConfiguration<CommunicationTemplateVersion>
{
    public void Configure(EntityTypeBuilder<CommunicationTemplateVersion> builder)
    {
        builder.ToTable("CommunicationTemplateVersions");
        builder.HasKey(v => v.Id);

        builder.Property(v => v.SubjectEn).HasMaxLength(500).IsRequired();
        builder.Property(v => v.SubjectAr).HasMaxLength(500);
        builder.Property(v => v.BodyEn).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(v => v.BodyAr).HasColumnType("nvarchar(max)");
        builder.Property(v => v.VariablesJson).HasColumnType("nvarchar(max)");
        builder.Property(v => v.PublishedBy).HasMaxLength(128);

        builder.HasIndex(v => new { v.TemplateId, v.VersionNumber }).IsUnique();
    }
}

internal sealed class CommunicationProviderConfigConfiguration : IEntityTypeConfiguration<CommunicationProviderConfig>
{
    public void Configure(EntityTypeBuilder<CommunicationProviderConfig> builder)
    {
        builder.ToTable("CommunicationProviderConfigs");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name).HasMaxLength(100).IsRequired();
        builder.Property(p => p.ProviderType).HasMaxLength(100).IsRequired();
        builder.Property(p => p.Channel).HasMaxLength(50).IsRequired();
        builder.Property(p => p.CreatedBy).HasMaxLength(128);
        builder.Property(p => p.ModifiedBy).HasMaxLength(128);

        builder.HasIndex(p => p.Channel).IsUnique();

        builder.Ignore(p => p.DomainEvents);
    }
}

internal sealed class CommunicationPreferenceConfiguration : IEntityTypeConfiguration<CommunicationPreference>
{
    public void Configure(EntityTypeBuilder<CommunicationPreference> builder)
    {
        builder.ToTable("CommunicationPreferences");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.UserId).HasMaxLength(450).IsRequired();

        builder.HasIndex(p => p.UserId).IsUnique();
    }
}

internal sealed class CommunicationAuditLogConfiguration : IEntityTypeConfiguration<CommunicationAuditLog>
{
    public void Configure(EntityTypeBuilder<CommunicationAuditLog> builder)
    {
        builder.ToTable("CommunicationAuditLogs");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Action).HasConversion<int>();
        builder.Property(l => l.ActorUserId).HasMaxLength(450);
        builder.Property(l => l.EntityType).HasMaxLength(100);
        builder.Property(l => l.EntityId).HasMaxLength(100);
        builder.Property(l => l.Details).HasColumnType("nvarchar(max)");

        builder.HasIndex(l => l.CreatedOnUtc);
    }
}
