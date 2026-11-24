using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Domain.Inventory;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Rewards;

/// <summary>Resolves a variant's platform / edition / region labels (the product facts a customer must see before spending Stars).</summary>
internal static class RewardOptionLabels
{
    public static async Task<IReadOnlyDictionary<Guid, (string? Platform, string? Edition, string? Region, string? Summary)>> LoadAsync(
        ICatalogDbContext catalog,
        IReadOnlyCollection<Guid> productIds,
        IEnumerable<ProductVariant> variants,
        CancellationToken cancellationToken)
    {
        var groups = await catalog.ProductOptionGroups.AsNoTracking()
            .Include(g => g.Options)
            .Where(g => productIds.Contains(g.ProductId))
            .ToListAsync(cancellationToken);
        var options = groups.SelectMany(g => g.Options.Select(o => (o.Id, o.Label, Key: g.Key.ToLowerInvariant()))).ToDictionary(x => x.Id);

        var result = new Dictionary<Guid, (string?, string?, string?, string?)>();
        foreach (var variant in variants)
        {
            string? platform = null, edition = null, region = null;
            var parts = new List<string>();
            foreach (var selected in variant.SelectedOptions)
            {
                if (!options.TryGetValue(selected.OptionId, out var option))
                {
                    continue;
                }

                parts.Add(option.Label);
                switch (option.Key)
                {
                    case "platform": platform = option.Label; break;
                    case "edition": edition = option.Label; break;
                    case "region": region = option.Label; break;
                }
            }

            result[variant.Id] = (platform, edition, region, parts.Count > 0 ? string.Join(" · ", parts) : null);
        }

        return result;
    }
}
