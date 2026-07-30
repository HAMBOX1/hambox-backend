using HAMBOX.Modules.Support.Domain.SavedReplies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Support.Infrastructure.Configurations;

internal sealed class SavedReplyFolderConfiguration : IEntityTypeConfiguration<SavedReplyFolder>
{
    public void Configure(EntityTypeBuilder<SavedReplyFolder> builder)
    {
        builder.ToTable("SavedReplyFolders");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Name).HasMaxLength(100).IsRequired();
        builder.Property(f => f.CreatedBy).HasMaxLength(128);
        builder.Property(f => f.ModifiedBy).HasMaxLength(128);

        builder.Ignore(f => f.DomainEvents);
    }
}

internal sealed class SavedReplyConfiguration : IEntityTypeConfiguration<SavedReply>
{
    public void Configure(EntityTypeBuilder<SavedReply> builder)
    {
        builder.ToTable("SavedReplies");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Title).HasMaxLength(200).IsRequired();
        builder.Property(r => r.Body).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(r => r.CreatedBy).HasMaxLength(128);
        builder.Property(r => r.ModifiedBy).HasMaxLength(128);

        builder.HasIndex(r => r.FolderId);
        builder.Ignore(r => r.DomainEvents);
    }
}
