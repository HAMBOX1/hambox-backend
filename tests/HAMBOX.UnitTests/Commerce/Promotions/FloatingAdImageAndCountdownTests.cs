using HAMBOX.Modules.Commerce.Application.Features.FloatingAds;
using HAMBOX.Modules.Commerce.Domain.Promotions;
using HAMBOX.UnitTests.Commerce.TestDoubles;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.UnitTests.Commerce.Promotions;

/// <summary>The floating card can carry a picture and a live countdown to the end of the offer.</summary>
public sealed class FloatingAdImageAndCountdownTests
{
    private static FloatingAdInput Input(
        string? imageUrl = null,
        bool showCountdown = false,
        DateTimeOffset? endsOnUtc = null) =>
        new(
            "BE GTA VI READY!", null, "Pre-order deals", null, "See the deals", null, "/products",
            FloatingAdAudience.Everyone, true, null, endsOnUtc, 0, 24, imageUrl, showCountdown);

    [Fact]
    public async Task SavesThePictureAndTheCountdown_AndShowsThemToVisitors()
    {
        var (db, _) = CommerceTestDbContextFactory.Create();
        var ends = DateTimeOffset.UtcNow.AddDays(3);
        var create = new CreateFloatingAdCommandHandler(db);

        var created = await create.Handle(
            new CreateFloatingAdCommand(Input("/uploads/gta.png", true, ends)), CancellationToken.None);

        Assert.True(created.IsSuccess);
        Assert.Equal("/uploads/gta.png", created.Value.ImageUrl);
        Assert.True(created.Value.ShowCountdown);
        var stored = await db.FloatingAds.SingleAsync();
        Assert.Equal("/uploads/gta.png", stored.ImageUrl);
        Assert.True(stored.ShowCountdown);
    }

    [Fact]
    public async Task ACountdownNeedsAnEndDate()
    {
        var (db, _) = CommerceTestDbContextFactory.Create();
        var create = new CreateFloatingAdCommandHandler(db);

        var result = await create.Handle(new CreateFloatingAdCommand(Input(showCountdown: true)), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("FloatingAds.CountdownNeedsEndDate", result.Error.Code);
        Assert.Empty(await db.FloatingAds.ToListAsync());
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("//evil.example/x.png")]
    [InlineData("http://insecure.example/x.png")]
    public async Task AnUnsafePictureAddressIsRejected(string image)
    {
        var (db, _) = CommerceTestDbContextFactory.Create();
        var create = new CreateFloatingAdCommandHandler(db);

        var result = await create.Handle(new CreateFloatingAdCommand(Input(image)), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("FloatingAds.InvalidImage", result.Error.Code);
    }

    [Fact]
    public async Task AnAdWithoutPictureOrCountdownStillSaves_LikeBefore()
    {
        var (db, _) = CommerceTestDbContextFactory.Create();
        var create = new CreateFloatingAdCommandHandler(db);

        var result = await create.Handle(new CreateFloatingAdCommand(Input()), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.ImageUrl);
        Assert.False(result.Value.ShowCountdown);
    }

    [Fact]
    public async Task TheCardDesignPositionsAndPagesAreSaved_AndUnknownValuesFallBackToTheDefaults()
    {
        var (db, _) = CommerceTestDbContextFactory.Create();
        var create = new CreateFloatingAdCommandHandler(db);

        var created = await create.Handle(new CreateFloatingAdCommand(Input() with
        {
            Style = "product",
            PositionDesktop = "BottomRight",
            PositionMobile = "Top",
            Pages = ["Home", "product", "nonsense"],
            PriceText = " $19.99 ",
            OldPriceText = "$29.99",
        }), CancellationToken.None);

        Assert.Equal("Product", created.Value.Style);
        Assert.Equal("BottomRight", created.Value.PositionDesktop);
        Assert.Equal("Top", created.Value.PositionMobile);
        Assert.Equal(["home", "product"], created.Value.Pages);
        Assert.Equal("$19.99", created.Value.PriceText);

        var fallback = await create.Handle(new CreateFloatingAdCommand(Input() with { Style = "<script>", PositionDesktop = "Center", PositionMobile = "x" }), CancellationToken.None);
        Assert.Equal("Mini", fallback.Value.Style);
        Assert.Equal("BottomLeft", fallback.Value.PositionDesktop);
        Assert.Equal("Bottom", fallback.Value.PositionMobile);
        Assert.Empty(fallback.Value.Pages!);
    }
}
