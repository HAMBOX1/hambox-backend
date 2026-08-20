using HAMBOX.Modules.Suppliers.Domain.Fulfillments;

namespace HAMBOX.UnitTests.Suppliers;

public sealed class SupplierFulfillmentTests
{
    private static SupplierFulfillment CreateAttempt(int requestedQuantity = 3) =>
        SupplierFulfillment.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), requestedQuantity);

    [Fact]
    public void Create_GeneratesAHamboxReferenceId_UpFront()
    {
        var attempt = CreateAttempt();

        Assert.NotEqual(Guid.Empty, attempt.HamboxReferenceId);
        Assert.Equal(SupplierFulfillmentStatus.Pending, attempt.Status);
        Assert.Equal(0, attempt.Attempts);
    }

    [Fact]
    public void Create_Throws_WhenRequestedQuantityIsNotPositive()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SupplierFulfillment.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 0));
    }

    [Fact]
    public void Claim_NeverGeneratesANewHamboxReferenceId_OnRetry()
    {
        var attempt = CreateAttempt();
        var originalReferenceId = attempt.HamboxReferenceId;

        attempt.Claim();
        attempt.MarkUnknown();
        attempt.Claim(); // retry after ambiguous failure

        Assert.Equal(originalReferenceId, attempt.HamboxReferenceId);
        Assert.Equal(2, attempt.Attempts);
    }

    [Fact]
    public void Claim_Throws_WhenAlreadySubmitting()
    {
        var attempt = CreateAttempt();
        attempt.Claim();

        Assert.Throws<InvalidOperationException>(attempt.Claim);
    }

    [Theory]
    [InlineData(SupplierFulfillmentStatus.Submitted)]
    [InlineData(SupplierFulfillmentStatus.Succeeded)]
    [InlineData(SupplierFulfillmentStatus.PartialFailed)]
    [InlineData(SupplierFulfillmentStatus.Failed)]
    public void Claim_Throws_FromAnyNonClaimableStatus(SupplierFulfillmentStatus status)
    {
        var attempt = CreateAttempt();
        attempt.Claim();
        MoveTo(attempt, status);

        Assert.Throws<InvalidOperationException>(attempt.Claim);
    }

    [Fact]
    public void MarkSubmitted_Throws_WhenNotSubmitting()
    {
        var attempt = CreateAttempt();

        Assert.Throws<InvalidOperationException>(() => attempt.MarkSubmitted("provider-order-1"));
    }

    [Fact]
    public void MarkSucceeded_RequiresExactlyTheRequestedQuantity()
    {
        var attempt = CreateAttempt(requestedQuantity: 3);
        attempt.Claim();
        attempt.MarkSubmitted("provider-order-1");

        Assert.Throws<ArgumentOutOfRangeException>(() => attempt.MarkSucceeded(2));

        attempt.MarkSucceeded(3);

        Assert.Equal(SupplierFulfillmentStatus.Succeeded, attempt.Status);
        Assert.Equal(3, attempt.DeliveredQuantity);
        Assert.Equal(0, attempt.RemainingQuantity);
        Assert.True(attempt.IsTerminal);
        Assert.NotNull(attempt.CompletedOnUtc);
    }

    [Fact]
    public void MarkPartialFailed_PreservesRemainingShortfall()
    {
        var attempt = CreateAttempt(requestedQuantity: 5);
        attempt.Claim();
        attempt.MarkSubmitted("provider-order-1");

        attempt.MarkPartialFailed(2);

        Assert.Equal(SupplierFulfillmentStatus.PartialFailed, attempt.Status);
        Assert.Equal(2, attempt.DeliveredQuantity);
        Assert.Equal(3, attempt.RemainingQuantity);
        Assert.True(attempt.IsTerminal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(6)]
    public void MarkPartialFailed_Throws_WhenDeliveredQuantityIsOutOfRange(int delivered)
    {
        var attempt = CreateAttempt(requestedQuantity: 5);
        attempt.Claim();
        attempt.MarkSubmitted("provider-order-1");

        Assert.Throws<ArgumentOutOfRangeException>(() => attempt.MarkPartialFailed(delivered));
    }

    [Fact]
    public void MarkFailed_LeavesDeliveredQuantityAtZero_AndRecordsCategory()
    {
        var attempt = CreateAttempt();
        attempt.Claim();
        attempt.MarkSubmitted("provider-order-1");

        attempt.MarkFailed(SupplierFulfillmentFailureCategory.InsufficientSupplierBalance, "balance too low");

        Assert.Equal(SupplierFulfillmentStatus.Failed, attempt.Status);
        Assert.Equal(0, attempt.DeliveredQuantity);
        Assert.Equal(SupplierFulfillmentFailureCategory.InsufficientSupplierBalance, attempt.FailureCategory);
        Assert.Equal("balance too low", attempt.FailureDetail);
        Assert.True(attempt.IsTerminal);
    }

    [Fact]
    public void MarkUnknown_ThenMarkSucceeded_ResolvesViaReconciliationPath()
    {
        var attempt = CreateAttempt(requestedQuantity: 1);
        attempt.Claim();
        attempt.MarkUnknown();

        // Reconciliation resolved the ambiguous attempt as successful without ever re-submitting.
        attempt.MarkSucceeded(1);

        Assert.Equal(SupplierFulfillmentStatus.Succeeded, attempt.Status);
    }

    [Fact]
    public void MarkSucceeded_Throws_WhenNotInAResolvableStatus()
    {
        var attempt = CreateAttempt();

        Assert.Throws<InvalidOperationException>(() => attempt.MarkSucceeded(attempt.RequestedQuantity));
    }

    [Fact]
    public void RecordReconciliationAttempt_UpdatesTimestamp_WithoutChangingStatus()
    {
        var attempt = CreateAttempt();
        attempt.Claim();
        attempt.MarkSubmitted("provider-order-1");

        attempt.RecordReconciliationAttempt();

        Assert.Equal(SupplierFulfillmentStatus.Submitted, attempt.Status);
        Assert.NotNull(attempt.LastReconciledOnUtc);
    }

    private static void MoveTo(SupplierFulfillment attempt, SupplierFulfillmentStatus status)
    {
        switch (status)
        {
            case SupplierFulfillmentStatus.Submitted:
                attempt.MarkSubmitted("provider-order-1");
                break;
            case SupplierFulfillmentStatus.Succeeded:
                attempt.MarkSubmitted("provider-order-1");
                attempt.MarkSucceeded(attempt.RequestedQuantity);
                break;
            case SupplierFulfillmentStatus.PartialFailed:
                attempt.MarkSubmitted("provider-order-1");
                attempt.MarkPartialFailed(Math.Max(1, attempt.RequestedQuantity - 1));
                break;
            case SupplierFulfillmentStatus.Failed:
                attempt.MarkFailed(SupplierFulfillmentFailureCategory.UnknownProviderState, null);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(status));
        }
    }
}
