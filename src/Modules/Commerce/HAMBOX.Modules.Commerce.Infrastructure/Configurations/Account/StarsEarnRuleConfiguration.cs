using HAMBOX.Modules.Commerce.Domain.Account;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Commerce.Infrastructure.Configurations.Account;

internal sealed class StarsEarnRuleConfiguration : IEntityTypeConfiguration<StarsEarnRule>
{
    public void Configure(EntityTypeBuilder<StarsEarnRule> builder)
    {
        builder.ToTable("StarsEarnRules");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Name).IsRequired().HasMaxLength(200);
        builder.Property(r => r.Scope).HasConversion<int>();
        builder.Property(r => r.Mode).HasConversion<int>();
        builder.Property(r => r.Value).HasPrecision(18, 4);

        builder.HasIndex(r => new { r.IsActive, r.Scope, r.ScopeId }).HasDatabaseName("IX_StarsEarnRules_Active_Scope");
    }
}
