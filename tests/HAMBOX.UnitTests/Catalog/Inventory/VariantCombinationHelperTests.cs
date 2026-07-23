using HAMBOX.Modules.Catalog.Application.Features.Inventory;
using HAMBOX.Modules.Catalog.Domain.Inventory;

namespace HAMBOX.UnitTests.Catalog.Inventory;

public class VariantCombinationHelperTests
{
    [Fact]
    public void Expand_NoChildGroups_ProducesOneLeafPerOption()
    {
        var productId = Guid.NewGuid();
        var platform = ProductOptionGroup.Create(productId, "platform", "Platform", sortOrder: 0);
        platform.AddOption("xbox", "Xbox", 0);
        platform.AddOption("psn", "PSN", 1);
        platform.AddOption("steam", "Steam", 2);

        var combinations = VariantCombinationHelper.Expand([platform]);

        Assert.Equal(3, combinations.Count);
        Assert.All(combinations, combo => Assert.Single(combo));
    }

    [Fact]
    public void Expand_MultipleRootGroups_CrossMultipliesLikeOldFlatBehavior()
    {
        var productId = Guid.NewGuid();
        var platform = ProductOptionGroup.Create(productId, "platform", "Platform", sortOrder: 0);
        platform.AddOption("xbox", "Xbox", 0);
        platform.AddOption("psn", "PSN", 1);
        platform.AddOption("steam", "Steam", 2);

        var region = ProductOptionGroup.Create(productId, "region", "Region", sortOrder: 1);
        region.AddOption("us", "US", 0);
        region.AddOption("eu", "EU", 1);

        var combinations = VariantCombinationHelper.Expand([platform, region]);

        Assert.Equal(6, combinations.Count);
        Assert.All(combinations, combo => Assert.Equal(2, combo.Count));
        AssertLeafCountMatches([platform, region]);
    }

    [Fact]
    public void Expand_OneChildLevel_MultipliesParentOptionByChildOptions()
    {
        var productId = Guid.NewGuid();
        var platform = ProductOptionGroup.Create(productId, "platform", "Platform", sortOrder: 0);
        var xbox = platform.AddOption("xbox", "Xbox", 0);
        platform.AddOption("steam", "Steam", 1);

        var accountType = ProductOptionGroup.Create(productId, "account-type", "Account Type", sortOrder: 0, parentOptionId: xbox.Id);
        accountType.AddOption("redeem-code", "Redeem Code", 0);
        accountType.AddOption("home-account", "Home Account", 1);

        var allGroups = new[] { platform, accountType };
        var combinations = VariantCombinationHelper.Expand(allGroups);

        // Xbox x 2 account types + Steam (leaf) = 3.
        Assert.Equal(3, combinations.Count);
        AssertLeafCountMatches(allGroups);
    }

    [Fact]
    public void Expand_MultipleChildLevels_RecursesArbitraryDepth()
    {
        var productId = Guid.NewGuid();
        var platform = ProductOptionGroup.Create(productId, "platform", "Platform", sortOrder: 0);
        var xbox = platform.AddOption("xbox", "Xbox", 0);

        var accountType = ProductOptionGroup.Create(productId, "account-type", "Account Type", sortOrder: 0, parentOptionId: xbox.Id);
        var redeemCode = accountType.AddOption("redeem-code", "Redeem Code", 0);
        accountType.AddOption("home-account", "Home Account", 1);

        // A third nesting level: Redeem Code itself has a Region child group.
        var region = ProductOptionGroup.Create(productId, "region", "Region", sortOrder: 0, parentOptionId: redeemCode.Id);
        region.AddOption("us", "US", 0);
        region.AddOption("eu", "EU", 1);

        var allGroups = new[] { platform, accountType, region };
        var combinations = VariantCombinationHelper.Expand(allGroups);

        // Redeem Code x 2 regions + Home Account (leaf, no region) = 3.
        Assert.Equal(3, combinations.Count);
        Assert.Contains(combinations, combo => combo.Contains(xbox.Id) && combo.Contains(redeemCode.Id) && combo.Count == 3);
        AssertLeafCountMatches(allGroups);
    }

    [Fact]
    public void Expand_SiblingBranches_NeverMix()
    {
        var productId = Guid.NewGuid();
        var platform = ProductOptionGroup.Create(productId, "platform", "Platform", sortOrder: 0);
        var xbox = platform.AddOption("xbox", "Xbox", 0);
        var psn = platform.AddOption("psn", "PSN", 1);
        var steam = platform.AddOption("steam", "Steam", 2);
        platform.AddOption("epic", "Epic", 3);

        var xboxAccountType = ProductOptionGroup.Create(productId, "account-type", "Account Type", sortOrder: 0, parentOptionId: xbox.Id);
        xboxAccountType.AddOption("redeem-code", "Redeem Code", 0);
        xboxAccountType.AddOption("home-account", "Home Account", 1);

        var psnAccountType = ProductOptionGroup.Create(productId, "account-type", "Account Type", sortOrder: 0, parentOptionId: psn.Id);
        psnAccountType.AddOption("primary", "Primary", 0);
        psnAccountType.AddOption("secondary", "Secondary", 1);

        var allGroups = new[] { platform, xboxAccountType, psnAccountType };
        var combinations = VariantCombinationHelper.Expand(allGroups);

        // Xbox x 2 + PSN x 2 + Steam (leaf) + Epic (leaf) = 6, never 4 platforms x 4 account types = 16.
        Assert.Equal(6, combinations.Count);

        Assert.DoesNotContain(combinations, combo => combo.Contains(steam.Id) && combo.Count > 1);
        Assert.DoesNotContain(combinations, combo => combo.Contains(xbox.Id) && combo.Any(id => psnAccountType.Options.Any(o => o.Id == id)));
        Assert.DoesNotContain(combinations, combo => combo.Contains(psn.Id) && combo.Any(id => xboxAccountType.Options.Any(o => o.Id == id)));
        AssertLeafCountMatches(allGroups);
    }

    [Fact]
    public void Expand_OptionWithEmptyChildGroup_FallsBackToLeafInsteadOfBlocking()
    {
        var productId = Guid.NewGuid();
        var platform = ProductOptionGroup.Create(productId, "platform", "Platform", sortOrder: 0);
        var xbox = platform.AddOption("xbox", "Xbox", 0);
        platform.AddOption("steam", "Steam", 1);

        // Xbox's child group exists but has no values yet (mid-edit) — should not block generation.
        var xboxAccountType = ProductOptionGroup.Create(productId, "account-type", "Account Type", sortOrder: 0, parentOptionId: xbox.Id);

        var allGroups = new[] { platform, xboxAccountType };
        var combinations = VariantCombinationHelper.Expand(allGroups);

        Assert.Equal(2, combinations.Count);
        Assert.Contains(combinations, combo => combo is [var only] && only == xbox.Id);
        AssertLeafCountMatches(allGroups);
    }

    /// <summary>
    /// A non-empty child group always forces refinement, regardless of its <c>IsRequired</c> flag —
    /// that flag governs storefront selection UX only. An option is never both a bare terminal AND
    /// refined by the same child group at once.
    /// </summary>
    [Fact]
    public void Expand_ChildGroupMarkedOptional_StillForcesRefinementNoBareLeaf()
    {
        var productId = Guid.NewGuid();
        var platform = ProductOptionGroup.Create(productId, "platform", "Platform", sortOrder: 0);
        var steam = platform.AddOption("steam", "Steam", 0);
        platform.AddOption("not-steam", "Not Steam", 1);
        platform.AddOption("half-steam", "Half Steam", 2);

        var console = ProductOptionGroup.Create(
            productId, "console", "Console", sortOrder: 0, isRequired: false, parentOptionId: steam.Id);
        console.AddOption("xbox", "Xbox", 0);
        console.AddOption("ps", "PS", 1);

        var allGroups = new[] { platform, console };
        var combinations = VariantCombinationHelper.Expand(allGroups);

        // Steam/Xbox + Steam/PS + Not Steam (leaf) + Half Steam (leaf) = 4. Never a bare "Steam" too.
        Assert.Equal(4, combinations.Count);
        Assert.DoesNotContain(combinations, combo => combo is [var only] && only == steam.Id);
        Assert.Contains(combinations, combo => combo.Count == 2 && combo.Contains(steam.Id) && combo.Contains(console.Options.First(o => o.Value == "xbox").Id));
        Assert.Contains(combinations, combo => combo.Count == 2 && combo.Contains(steam.Id) && combo.Contains(console.Options.First(o => o.Value == "ps").Id));
        AssertLeafCountMatches(allGroups);
    }

    /// <summary>
    /// A required child group produces the same result as an optional one (see above) — refinement
    /// is always mandatory whenever a non-empty child group exists.
    /// </summary>
    [Fact]
    public void Expand_ChildGroupMarkedRequired_ForcesRefinementNoBareLeaf()
    {
        var productId = Guid.NewGuid();
        var platform = ProductOptionGroup.Create(productId, "platform", "Platform", sortOrder: 0);
        var steam = platform.AddOption("steam", "Steam", 0);

        var console = ProductOptionGroup.Create(
            productId, "console", "Console", sortOrder: 0, isRequired: true, parentOptionId: steam.Id);
        console.AddOption("xbox", "Xbox", 0);
        console.AddOption("ps", "PS", 1);

        var allGroups = new[] { platform, console };
        var combinations = VariantCombinationHelper.Expand(allGroups);

        Assert.Equal(2, combinations.Count);
        Assert.DoesNotContain(combinations, combo => combo is [var only] && only == steam.Id);
        AssertLeafCountMatches(allGroups);
    }

    /// <summary>
    /// Reported example 1: Platform/Steam has a child Console group (Xbox, PS); Edition/Standard is
    /// an independent root group. Steam must never appear as a bare terminal alongside Edition —
    /// only "Steam + Xbox + Standard" and "Steam + PS + Standard".
    /// </summary>
    [Fact]
    public void Expand_OptionWithChildren_NeverProducesBareOptionVariant()
    {
        var productId = Guid.NewGuid();
        var platform = ProductOptionGroup.Create(productId, "platform", "Platform", sortOrder: 0);
        var steam = platform.AddOption("steam", "Steam", 0);

        var console = ProductOptionGroup.Create(productId, "console", "Console", sortOrder: 0, parentOptionId: steam.Id);
        var xbox = console.AddOption("xbox", "XBOX", 0);
        var ps = console.AddOption("ps", "PS", 1);

        var edition = ProductOptionGroup.Create(productId, "edition", "Edition", sortOrder: 1);
        var standard = edition.AddOption("standard", "Standard Edition", 0);

        var allGroups = new[] { platform, console, edition };
        var combinations = VariantCombinationHelper.Expand(allGroups);

        Assert.Equal(2, combinations.Count);
        Assert.Contains(combinations, combo => combo.Count == 3 && combo.Contains(steam.Id) && combo.Contains(xbox.Id) && combo.Contains(standard.Id));
        Assert.Contains(combinations, combo => combo.Count == 3 && combo.Contains(steam.Id) && combo.Contains(ps.Id) && combo.Contains(standard.Id));
        Assert.DoesNotContain(combinations, combo => combo.Count == 2 && combo.Contains(steam.Id) && combo.Contains(standard.Id));
        AssertLeafCountMatches(allGroups);
    }

    /// <summary>
    /// Reported example 2: Steam -> {Xbox -> Account Type (Redeem Code, Home Account), PS}. PS has
    /// no children so it still produces its own variant; Xbox always recurses into Account Type.
    /// </summary>
    [Fact]
    public void Expand_MixedSiblings_SomeWithChildrenSomeWithout()
    {
        var productId = Guid.NewGuid();
        var platform = ProductOptionGroup.Create(productId, "platform", "Platform", sortOrder: 0);
        var steam = platform.AddOption("steam", "Steam", 0);

        var console = ProductOptionGroup.Create(productId, "console", "Console", sortOrder: 0, parentOptionId: steam.Id);
        var xbox = console.AddOption("xbox", "XBOX", 0);
        var ps = console.AddOption("ps", "PS", 1);

        var accountType = ProductOptionGroup.Create(productId, "account-type", "Account Type", sortOrder: 0, parentOptionId: xbox.Id);
        var redeemCode = accountType.AddOption("redeem-code", "Redeem Code", 0);
        var homeAccount = accountType.AddOption("home-account", "Home Account", 1);

        var edition = ProductOptionGroup.Create(productId, "edition", "Edition", sortOrder: 1);
        var standard = edition.AddOption("standard", "Standard Edition", 0);

        var allGroups = new[] { platform, console, accountType, edition };
        var combinations = VariantCombinationHelper.Expand(allGroups);

        Assert.Equal(3, combinations.Count);
        Assert.Contains(combinations, combo => combo.Count == 4 && combo.Contains(steam.Id) && combo.Contains(xbox.Id) && combo.Contains(redeemCode.Id) && combo.Contains(standard.Id));
        Assert.Contains(combinations, combo => combo.Count == 4 && combo.Contains(steam.Id) && combo.Contains(xbox.Id) && combo.Contains(homeAccount.Id) && combo.Contains(standard.Id));
        Assert.Contains(combinations, combo => combo.Count == 3 && combo.Contains(steam.Id) && combo.Contains(ps.Id) && combo.Contains(standard.Id));
        AssertLeafCountMatches(allGroups);
    }

    /// <summary>
    /// The exact reported tree: Platform (Steam -&gt; {Xbox -&gt; Redeem Code, PS}, Not Steam -&gt;
    /// {Xbox, PS, PS2}, Half Steam) x Edition (Standard Edition). Every "console" node is a real
    /// OPTION (sibling of the others), not an empty group — that distinction is exactly what makes
    /// the tree produce 6 terminal leaves instead of collapsing/blocking.
    /// </summary>
    [Fact]
    public void Expand_MixedTree_LeafCountEqualsProductOfRootLeafCounts()
    {
        var productId = Guid.NewGuid();

        var platform = ProductOptionGroup.Create(productId, "platform", "Platform", sortOrder: 0);
        var steam = platform.AddOption("steam", "Steam", 0);
        var notSteam = platform.AddOption("not-steam", "Not Steam", 1);
        platform.AddOption("half-steam", "Half Steam", 2);

        var steamConsole = ProductOptionGroup.Create(productId, "console", "Console", sortOrder: 0, parentOptionId: steam.Id);
        var xboxUnderSteam = steamConsole.AddOption("xbox", "Xbox", 0);
        steamConsole.AddOption("ps", "PS", 1);

        var xboxAccountType = ProductOptionGroup.Create(productId, "account-type", "Account Type", sortOrder: 0, parentOptionId: xboxUnderSteam.Id);
        xboxAccountType.AddOption("redeem-code", "Redeem Code", 0);

        var notSteamConsole = ProductOptionGroup.Create(productId, "console", "Console", sortOrder: 0, parentOptionId: notSteam.Id);
        notSteamConsole.AddOption("xbox", "Xbox", 0);
        notSteamConsole.AddOption("ps", "PS", 1);
        notSteamConsole.AddOption("ps2", "PS2", 2);

        var edition = ProductOptionGroup.Create(productId, "edition", "Edition", sortOrder: 1);
        edition.AddOption("standard", "Standard Edition", 0);

        var allGroups = new[] { platform, steamConsole, xboxAccountType, notSteamConsole, edition };

        var combinations = VariantCombinationHelper.Expand(allGroups);

        Assert.Equal(6, combinations.Count);
        AssertLeafCountMatches(allGroups);

        // Add two more Edition options and confirm the count scales multiplicatively: 6 platform
        // leaves x 3 editions = 18, never additive (6 + 3) and never a flat cross of every group.
        edition.AddOption("deluxe", "Deluxe", 1);
        edition.AddOption("ultimate", "Ultimate", 2);

        var combinationsWithMoreEditions = VariantCombinationHelper.Expand(allGroups);
        Assert.Equal(18, combinationsWithMoreEditions.Count);
        AssertLeafCountMatches(allGroups);
    }

    /// <summary>
    /// Independently recomputes the expected total (product of each root group's own leaf-path
    /// count, where an option's leaf-path count is 1 if it has no non-empty child groups, or the
    /// sum over its child groups' cross-product otherwise — always mandatory, regardless of
    /// IsRequired) and asserts it matches <see cref="VariantCombinationHelper.Expand"/>'s actual
    /// output count — a mathematical cross-check independent of the production algorithm's own code
    /// path.
    /// </summary>
    private static void AssertLeafCountMatches(IReadOnlyList<ProductOptionGroup> allGroups)
    {
        var rootGroups = allGroups.Where(g => g.ParentOptionId is null).ToList();
        var childGroupsByParentOptionId = allGroups
            .Where(g => g.ParentOptionId is not null)
            .ToLookup(g => g.ParentOptionId!.Value);

        var expected = CountGroupsLeafPaths(rootGroups, childGroupsByParentOptionId);
        var actual = VariantCombinationHelper.Expand(allGroups).Count;

        Assert.Equal(expected, actual);
    }

    private static long CountGroupsLeafPaths(
        IReadOnlyList<ProductOptionGroup> groups,
        ILookup<Guid, ProductOptionGroup> childGroupsByParentOptionId)
    {
        long product = 1;
        foreach (var group in groups)
        {
            var sum = group.Options.Sum(option => CountOptionLeafPaths(option, childGroupsByParentOptionId));
            product *= sum;
        }

        return product;
    }

    private static long CountOptionLeafPaths(
        ProductOption option,
        ILookup<Guid, ProductOptionGroup> childGroupsByParentOptionId)
    {
        var childGroups = childGroupsByParentOptionId[option.Id].Where(g => g.Options.Count > 0).ToList();
        return childGroups.Count == 0 ? 1 : CountGroupsLeafPaths(childGroups, childGroupsByParentOptionId);
    }
}
