using HAMBOX.Modules.Commerce.Domain.Account;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Commerce.Infrastructure.Configurations.Account;

internal sealed class StarsRuleAwardConfiguration : IEntityTypeConfiguration<StarsRuleAward>
{
    public void Configure(EntityTypeBuilder<StarsRuleAward> builder)
    {
        builder.ToTable("StarsRuleAwards");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.UserId).IsRequired().HasMaxLength(450);
        builder.HasIndex(a => new { a.RuleId, a.OrderId }).IsUnique().HasDatabaseName("UX_StarsRuleAwards_Rule_Order");
        builder.HasIndex(a => new { a.RuleId, a.UserId }).HasDatabaseName("IX_StarsRuleAwards_Rule_User");
    }
}
