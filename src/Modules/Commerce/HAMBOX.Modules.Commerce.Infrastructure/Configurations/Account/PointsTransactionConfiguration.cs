using HAMBOX.Modules.Commerce.Domain.Account;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Commerce.Infrastructure.Configurations.Account;

/// <summary>
/// Configures the <see cref="PointsTransaction"/> ledger. The unique <c>IdempotencyKey</c> index is what makes
/// awarding and reversing safe against duplicate webhooks, retries and concurrent order processing.
/// </summary>
internal sealed class PointsTransactionConfiguration : IEntityTypeConfiguration<PointsTransaction>
{
    public void Configure(EntityTypeBuilder<PointsTransaction> builder)
    {
        builder.ToTable("PointsTransactions");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.UserId).IsRequired().HasMaxLength(450);
        builder.Property(t => t.Type).HasConversion<int>();
        builder.Property(t => t.OrderNumber).HasMaxLength(100);
        builder.Property(t => t.Status).HasConversion<int>().HasDefaultValue(StarsStatus.Available);
        builder.Property(t => t.ActorUserId).HasMaxLength(450);
        builder.Property(t => t.Reference).HasMaxLength(200);
        builder.Property(t => t.EligibleAmountUsd).HasPrecision(18, 2);
        builder.Property(t => t.PointsPerUsd).HasPrecision(18, 4);
        builder.Property(t => t.Description).IsRequired().HasMaxLength(500);
        builder.Property(t => t.IdempotencyKey).IsRequired().HasMaxLength(100);
        builder.Property(t => t.OccurredOnUtc).IsRequired();

        builder.HasIndex(t => t.IdempotencyKey).IsUnique().HasDatabaseName("UX_PointsTransactions_IdempotencyKey");
        builder.HasIndex(t => new { t.UserId, t.OccurredOnUtc }).HasDatabaseName("IX_PointsTransactions_UserId_OccurredOnUtc");
        builder.HasIndex(t => new { t.UserId, t.Status }).HasDatabaseName("IX_PointsTransactions_UserId_Status");
        builder.HasIndex(t => t.OrderId).HasDatabaseName("IX_PointsTransactions_OrderId");
    }
}
