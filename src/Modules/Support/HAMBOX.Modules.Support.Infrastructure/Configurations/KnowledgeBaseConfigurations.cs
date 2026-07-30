using HAMBOX.Modules.Support.Domain.KnowledgeBase;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Support.Infrastructure.Configurations;

internal sealed class KnowledgeCategoryConfiguration : IEntityTypeConfiguration<KnowledgeCategory>
{
    public void Configure(EntityTypeBuilder<KnowledgeCategory> builder)
    {
        builder.ToTable("KnowledgeCategories");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Name).HasMaxLength(100).IsRequired();
        builder.Property(c => c.Slug).HasMaxLength(120).IsRequired();
        builder.Property(c => c.CreatedBy).HasMaxLength(128);
        builder.Property(c => c.ModifiedBy).HasMaxLength(128);

        builder.HasIndex(c => c.Slug).IsUnique();
        builder.Ignore(c => c.DomainEvents);
    }
}

internal sealed class KnowledgeArticleConfiguration : IEntityTypeConfiguration<KnowledgeArticle>
{
    public void Configure(EntityTypeBuilder<KnowledgeArticle> builder)
    {
        builder.ToTable("KnowledgeArticles");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Title).HasMaxLength(200).IsRequired();
        builder.Property(a => a.Slug).HasMaxLength(220).IsRequired();
        builder.Property(a => a.Body).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(a => a.Status).HasConversion<int>();
        builder.Property(a => a.Visibility).HasConversion<int>();
        builder.Property(a => a.RelatedArticleIdsJson).HasColumnType("nvarchar(max)");
        builder.Property(a => a.CreatedBy).HasMaxLength(128);
        builder.Property(a => a.ModifiedBy).HasMaxLength(128);

        builder.HasIndex(a => a.Slug).IsUnique();
        builder.HasIndex(a => a.CategoryId);
        builder.HasIndex(a => a.Status);
        builder.Ignore(a => a.DomainEvents);
    }
}
