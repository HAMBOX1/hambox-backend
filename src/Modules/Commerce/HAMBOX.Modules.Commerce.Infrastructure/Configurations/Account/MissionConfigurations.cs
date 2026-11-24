using HAMBOX.Modules.Commerce.Domain.Missions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Commerce.Infrastructure.Configurations.Account;

internal sealed class MissionConfiguration : IEntityTypeConfiguration<Mission>
{
    public void Configure(EntityTypeBuilder<Mission> builder)
    {
        builder.ToTable("Missions");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.NameEn).IsRequired().HasMaxLength(150);
        builder.Property(m => m.NameAr).IsRequired().HasMaxLength(150);
        builder.Property(m => m.DescriptionEn).HasMaxLength(500);
        builder.Property(m => m.DescriptionAr).HasMaxLength(500);
        builder.Property(m => m.Kind).HasConversion<int>();
        builder.Property(m => m.Target).HasPrecision(18, 2);
        builder.Property(m => m.BadgeKey).HasMaxLength(60);
        builder.Ignore(m => m.DomainEvents);
    }
}

internal sealed class MissionCompletionConfiguration : IEntityTypeConfiguration<MissionCompletion>
{
    public void Configure(EntityTypeBuilder<MissionCompletion> builder)
    {
        builder.ToTable("MissionCompletions");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.UserId).IsRequired().HasMaxLength(450);
        builder.HasIndex(c => new { c.MissionId, c.UserId }).IsUnique().HasDatabaseName("UX_MissionCompletions_Mission_User");
    }
}
