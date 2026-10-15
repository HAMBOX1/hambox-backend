using HAMBOX.Modules.Commerce.Domain.Account;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Commerce.Infrastructure.Configurations.Account;

internal sealed class ReviewVoteConfiguration : IEntityTypeConfiguration<ReviewVote>
{
    public void Configure(EntityTypeBuilder<ReviewVote> builder)
    {
        builder.ToTable("ReviewVotes");

        builder.HasKey(v => v.Id);

        builder.Property(v => v.UserId).IsRequired().HasMaxLength(450);
        builder.Property(v => v.ReviewId).IsRequired();
        builder.Property(v => v.IsHelpful).IsRequired();
        builder.Property(v => v.CreatedOnUtc).IsRequired();
        builder.Property(v => v.ModifiedOnUtc);

        builder.HasIndex(v => new { v.ReviewId, v.UserId })
            .IsUnique()
            .HasDatabaseName("IX_ReviewVotes_ReviewId_UserId");
    }
}
