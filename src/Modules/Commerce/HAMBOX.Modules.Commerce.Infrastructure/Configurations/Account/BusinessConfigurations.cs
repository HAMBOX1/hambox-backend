using HAMBOX.Modules.Commerce.Domain.Business;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HAMBOX.Modules.Commerce.Infrastructure.Configurations.Account;

internal sealed class BusinessAccountConfiguration : IEntityTypeConfiguration<BusinessAccount>
{
    public void Configure(EntityTypeBuilder<BusinessAccount> builder)
    {
        builder.ToTable("BusinessAccounts");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.UserId).IsRequired().HasMaxLength(450);
        builder.Property(a => a.CompanyName).IsRequired().HasMaxLength(200);
        builder.Property(a => a.TaxId).HasMaxLength(100);
        builder.Property(a => a.Country).IsRequired().HasMaxLength(10);
        builder.Property(a => a.Website).HasMaxLength(300);
        builder.Property(a => a.ContactPhone).HasMaxLength(50);
        builder.Property(a => a.Notes).HasMaxLength(2000);
        builder.Property(a => a.Status).HasConversion<int>();
        builder.Property(a => a.SalesRepUserId).HasMaxLength(450);
        builder.Property(a => a.ReviewedBy).HasMaxLength(450);
        builder.Property(a => a.DecisionReason).HasMaxLength(500);
        builder.Ignore(a => a.IsApproved);
        builder.Ignore(a => a.DomainEvents);
        builder.HasIndex(a => a.UserId).IsUnique().HasDatabaseName("UX_BusinessAccounts_UserId");
        builder.HasIndex(a => a.Status).HasDatabaseName("IX_BusinessAccounts_Status");
    }
}

internal sealed class BusinessPriceConfiguration : IEntityTypeConfiguration<BusinessPrice>
{
    public void Configure(EntityTypeBuilder<BusinessPrice> builder)
    {
        builder.ToTable("BusinessPrices");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.UnitPriceUsd).HasPrecision(18, 2);
        builder.HasIndex(p => new { p.BusinessAccountId, p.ProductId }).HasDatabaseName("IX_BusinessPrices_Account_Product");
    }
}

internal sealed class BusinessQuoteConfiguration : IEntityTypeConfiguration<BusinessQuote>
{
    public void Configure(EntityTypeBuilder<BusinessQuote> builder)
    {
        builder.ToTable("BusinessQuotes");
        builder.HasKey(q => q.Id);
        builder.Property(q => q.QuoteNumber).IsRequired().HasMaxLength(50);
        builder.Property(q => q.Status).HasConversion<int>();
        builder.Property(q => q.Terms).HasMaxLength(2000);
        builder.Property(q => q.CreatedByUserId).IsRequired().HasMaxLength(450);
        builder.Ignore(q => q.Total);
        builder.Ignore(q => q.DomainEvents);
        builder.HasIndex(q => q.QuoteNumber).IsUnique().HasDatabaseName("UX_BusinessQuotes_Number");
        builder.HasIndex(q => new { q.BusinessAccountId, q.Status }).HasDatabaseName("IX_BusinessQuotes_Account_Status");
        builder.HasMany(q => q.Lines).WithOne().HasForeignKey(l => l.QuoteId).OnDelete(DeleteBehavior.Cascade);
        builder.Metadata.FindNavigation(nameof(BusinessQuote.Lines))!.SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class BusinessQuoteLineConfiguration : IEntityTypeConfiguration<BusinessQuoteLine>
{
    public void Configure(EntityTypeBuilder<BusinessQuoteLine> builder)
    {
        builder.ToTable("BusinessQuoteLines");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.ProductName).IsRequired().HasMaxLength(300);
        builder.Property(l => l.Sku).IsRequired().HasMaxLength(100);
        builder.Property(l => l.UnitPriceUsd).HasPrecision(18, 2);
        builder.Ignore(l => l.LineTotal);
    }
}
