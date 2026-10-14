using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Catalog.Application.Errors;
using HAMBOX.Modules.Catalog.Application.Features.Inventory;
using HAMBOX.Modules.Catalog.Domain.Enums;
using HAMBOX.Modules.Catalog.Domain.Inventory;
using HAMBOX.Modules.Catalog.Infrastructure.Services;
using HAMBOX.UnitTests.Catalog.Inventory.VariantLifecycle;
using HAMBOX.UnitTests.Commerce.TestDoubles;

namespace HAMBOX.UnitTests.Catalog.Inventory;

/// <summary>
/// Audit R15/R16/R18: the code lifecycle (incl. the Failed state and its per-code data), the per-line rejection
/// report an import returns, and the mandatory reason when an admin reveals a code.
/// </summary>
public sealed class InventoryCodeLifecycleAndImportReportTests
{
    private static DigitalInventoryCode NewCode(string code = "AAAA-BBBB-CCCC") =>
        DigitalInventoryCode.Create(Guid.NewGuid(), Guid.NewGuid(), code);

    // ─── lifecycle (R16) ────────────────────────────────────────────────

    [Fact]
    public void NewCode_IsAvailable_WithNoFailureData()
    {
        var code = NewCode();

        Assert.Equal(InventoryCodeStatus.Available, code.Status);
        Assert.Null(code.FailureReason);
        Assert.Null(code.SoldToUserId);
        Assert.Null(code.LastStatusChangedOnUtc);
    }

    [Fact]
    public void EveryTransition_StampsLastStatusChange()
    {
        var code = NewCode();

        code.Reserve();
        var reservedAt = code.LastStatusChangedOnUtc;
        Assert.NotNull(reservedAt);

        code.MarkSold(Guid.NewGuid(), Guid.NewGuid(), "user-42");
        Assert.Equal(InventoryCodeStatus.Sold, code.Status);
        Assert.Equal("user-42", code.SoldToUserId);
        Assert.True(code.LastStatusChangedOnUtc >= reservedAt);

        code.ReturnToStock();
        Assert.Equal(InventoryCodeStatus.Available, code.Status);
        Assert.Null(code.SoldToUserId);
    }

    [Fact]
    public void MarkFailed_KeepsTheReason_AndTheCodeIsNoLongerAvailable()
    {
        var code = NewCode();

        code.MarkFailed("  Rejected by the platform: already redeemed  ");

        Assert.Equal(InventoryCodeStatus.Failed, code.Status);
        Assert.Equal("Rejected by the platform: already redeemed", code.FailureReason);
        Assert.NotNull(code.LastStatusChangedOnUtc);
        Assert.Throws<InvalidOperationException>(code.Reserve);
    }

    [Fact]
    public void MarkFailed_RequiresAReason()
    {
        Assert.Throws<ArgumentException>(() => NewCode().MarkFailed("   "));
    }

    [Fact]
    public void ReEnabling_AFailedCode_ClearsTheFailure()
    {
        var code = NewCode();
        code.MarkFailed("bad");

        code.Enable();

        Assert.Equal(InventoryCodeStatus.Available, code.Status);
        Assert.Null(code.FailureReason);
    }

    [Fact]
    public async Task MarkFailedHandler_RecordsTheReasonInTheAuditTrail_AndRefusesAReservedCode()
    {
        var db = TestCatalogDbContextFactory.Create();
        var variant = ProductVariant.Create(Guid.NewGuid(), $"SKU-{Guid.NewGuid():N}");
        var batch = InventoryBatch.Create(variant.Id, "Batch 1");
        var available = DigitalInventoryCode.Create(variant.Id, batch.Id, "AVAILABLE-CODE-1");
        var reserved = DigitalInventoryCode.Create(variant.Id, batch.Id, "RESERVED-CODE-1");
        reserved.Reserve();
        db.ProductVariants.Add(variant);
        db.InventoryBatches.Add(batch);
        db.DigitalInventoryCodes.AddRange(available, reserved);
        await db.SaveChangesAsync();

        var handler = new MarkInventoryCodeFailedCommandHandler(db, new FakeCurrentUserService("admin-1"));

        var ok = await handler.Handle(new MarkInventoryCodeFailedCommand(available.Id, "Platform says invalid"), CancellationToken.None);
        Assert.True(ok.IsSuccess);
        Assert.Equal(InventoryCodeStatus.Failed, available.Status);
        var audit = Assert.Single(db.InventoryAuditLogs.Where(l => l.Action == InventoryAuditAction.CodeMarkedFailed));
        Assert.Equal("Platform says invalid", audit.Reason);
        Assert.Equal(available.Id, audit.CodeId);

        var refused = await handler.Handle(new MarkInventoryCodeFailedCommand(reserved.Id, "x"), CancellationToken.None);
        Assert.Equal(CatalogErrors.InvalidCodeStatus.Code, refused.Error.Code);

        var noReason = await handler.Handle(new MarkInventoryCodeFailedCommand(available.Id, " "), CancellationToken.None);
        Assert.Equal(CatalogErrors.FailureReasonRequired.Code, noReason.Error.Code);
    }

    // ─── reveal reason (R18) ────────────────────────────────────────────

    [Fact]
    public async Task Reveal_WithoutAReason_IsRefused_AndNothingIsLogged()
    {
        var db = TestCatalogDbContextFactory.Create();
        var variant = ProductVariant.Create(Guid.NewGuid(), $"SKU-{Guid.NewGuid():N}");
        var code = DigitalInventoryCode.Create(variant.Id, Guid.NewGuid(), "SECRET-CODE-9999");
        db.ProductVariants.Add(variant);
        db.DigitalInventoryCodes.Add(code);
        await db.SaveChangesAsync();
        var handler = new RevealInventoryCodeCommandHandler(db, new FakeCurrentUserService("admin-1"));

        var result = await handler.Handle(new RevealInventoryCodeCommand(code.Id, " ", "203.0.113.5", "UA"), CancellationToken.None);

        Assert.Equal(CatalogErrors.RevealReasonRequired.Code, result.Error.Code);
        Assert.Empty(db.InventoryAuditLogs);
    }

    [Fact]
    public async Task Reveal_WithAReason_StoresReason_Ip_AndDevice_ButNeverThePlaintext()
    {
        var db = TestCatalogDbContextFactory.Create();
        var variant = ProductVariant.Create(Guid.NewGuid(), $"SKU-{Guid.NewGuid():N}");
        var code = DigitalInventoryCode.Create(variant.Id, Guid.NewGuid(), "SECRET-CODE-9999");
        db.ProductVariants.Add(variant);
        db.DigitalInventoryCodes.Add(code);
        await db.SaveChangesAsync();
        var handler = new RevealInventoryCodeCommandHandler(db, new FakeCurrentUserService("admin-1"));

        var result = await handler.Handle(
            new RevealInventoryCodeCommand(code.Id, "Customer ticket #4812", "203.0.113.5", "Mozilla/5.0"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("SECRET-CODE-9999", result.Value.DigitalCode);
        var audit = Assert.Single(db.InventoryAuditLogs);
        Assert.Equal(InventoryAuditAction.CodeRevealed, audit.Action);
        Assert.Equal("Customer ticket #4812", audit.Reason);
        Assert.Equal("203.0.113.5", audit.IpAddress);
        Assert.Equal("Mozilla/5.0", audit.UserAgent);
        Assert.DoesNotContain("SECRET-CODE-9999", audit.Details ?? string.Empty);
    }

    // ─── import rejection report (R15) ──────────────────────────────────

    [Fact]
    public async Task Import_ReportsEveryRejectedLine_WithPosition_Reason_AndAMaskedCode()
    {
        var db = TestCatalogDbContextFactory.Create();
        var product = HAMBOX.Modules.Catalog.Domain.Products.Product.Create(
            "منتج", "Product", "وصف", "Description", 10m, Guid.NewGuid());
        var variant = ProductVariant.Create(product.Id, $"SKU-{Guid.NewGuid():N}");
        variant.Activate();
        var batch = InventoryBatch.Create(variant.Id, "Batch 1");
        db.Products.Add(product);
        db.ProductVariants.Add(variant);
        db.InventoryBatches.Add(batch);
        db.DigitalInventoryCodes.Add(DigitalInventoryCode.Create(variant.Id, batch.Id, "ALREADY-HERE-0001"));
        await db.SaveChangesAsync();

        var engine = new InventoryEngine(
            db, new FakeCurrentUserService("admin-1"), new FakePlatformSettingsProvider(), new FakeCommerceVariantUsageProvider());

        var tooLong = new string('X', 1001);
        var result = await engine.ImportCodesAsync(
            variant.Id,
            batch.Id,
            [
                new ImportCodeItem("GOOD-CODE-0001"),
                new ImportCodeItem("   "),
                new ImportCodeItem("ALREADY-HERE-0001"),
                new ImportCodeItem(tooLong),
                new ImportCodeItem("GOOD-CODE-0002"),
            ],
            "admin-1");

        Assert.Equal(5, result.TotalSubmitted);
        Assert.Equal(2, result.Imported);
        Assert.Equal(3, result.Rejections!.Count);

        var empty = Assert.Single(result.Rejections!, r => r.Reason == ImportRejectionReasons.Empty);
        Assert.Equal(2, empty.Position);

        var duplicate = Assert.Single(result.Rejections!, r => r.Reason == ImportRejectionReasons.Duplicate);
        Assert.Equal(3, duplicate.Position);
        Assert.DoesNotContain("ALREADY-HERE-0001", duplicate.MaskedCode);
        Assert.Contains("Already in stock", duplicate.Detail);

        var longOne = Assert.Single(result.Rejections!, r => r.Reason == ImportRejectionReasons.TooLong);
        Assert.Equal(4, longOne.Position);
        Assert.True(longOne.MaskedCode.Length < tooLong.Length);
    }
}
