using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.Modules.Suppliers.Domain.Suppliers;

namespace HAMBOX.UnitTests.Suppliers;

/// <summary>
/// Audit R45: the single decision both checkout-time readiness and post-payment routing use — a supplier product
/// counts as available only if the last sync said so AND that answer is recent enough to still be trusted.
/// </summary>
public sealed class SupplierAvailabilityFreshnessTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(30);

    private static SupplierProductAvailability Availability(SupplierAvailabilityState state, DateTimeOffset? checkedAt)
    {
        var availability = SupplierProductAvailability.CreateUnknown(Guid.NewGuid(), Guid.NewGuid(), "EXT-1");
        if (checkedAt is { } at)
        {
            availability.RecordChecked(state, availableQuantity: 5, at, "EXT-1");
        }

        return availability;
    }

    [Fact]
    public void ARecentAvailableAnswer_IsTrusted()
    {
        var availability = Availability(SupplierAvailabilityState.Available, Now.AddMinutes(-5));

        Assert.True(SupplierAvailabilityFreshness.IsAvailableAndFresh(availability, StaleAfter, Now));
    }

    [Fact]
    public void AnAvailableAnswerOlderThanTheWindow_IsNotTrusted()
    {
        var availability = Availability(SupplierAvailabilityState.Available, Now.AddMinutes(-31));

        Assert.False(SupplierAvailabilityFreshness.IsAvailableAndFresh(availability, StaleAfter, Now));
    }

    [Theory]
    [InlineData(SupplierAvailabilityState.Unavailable)]
    [InlineData(SupplierAvailabilityState.Unknown)]
    public void AnythingButAvailable_IsNeverUsable_HoweverFresh(SupplierAvailabilityState state)
    {
        var availability = Availability(state, Now.AddMinutes(-1));

        Assert.False(SupplierAvailabilityFreshness.IsAvailableAndFresh(availability, StaleAfter, Now));
    }

    [Fact]
    public void NeverChecked_OrNoRow_IsNotUsable()
    {
        Assert.False(SupplierAvailabilityFreshness.IsAvailableAndFresh(null, StaleAfter, Now));
        Assert.False(SupplierAvailabilityFreshness.IsAvailableAndFresh(
            Availability(SupplierAvailabilityState.Available, null), StaleAfter, Now));
    }
}
