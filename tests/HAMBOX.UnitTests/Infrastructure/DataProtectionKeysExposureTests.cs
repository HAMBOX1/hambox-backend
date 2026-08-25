using HAMBOX.Infrastructure.Extensions;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace HAMBOX.UnitTests.Infrastructure;

/// <summary>
/// Regression coverage for a critical security fix: the DataProtection keyring that encrypts every
/// inventory code and license key at rest is persisted on disk under the same "uploads" root that
/// <c>Program.cs</c> serves publicly at <c>/uploads</c>. Before this fix, nothing stopped
/// <c>GET /uploads/dataprotection-keys/key-{guid}.xml</c> from returning the plaintext key material
/// to an unauthenticated caller. <see cref="InfrastructureExtensions.IsDataProtectionKeysRequest"/>
/// is the guard Program.cs runs before the static file middleware — these tests pin down exactly
/// what it must block and what it must not (uploads must keep working normally).
/// </summary>
public sealed class DataProtectionKeysExposureTests
{
    [Theory]
    [InlineData("/uploads/dataprotection-keys/key-abc123.xml")]
    [InlineData("/uploads/dataprotection-keys/key-abc123.xml/")]
    [InlineData("/UPLOADS/DataProtection-Keys/key-abc123.xml")]
    [InlineData("/uploads/dataprotection-keys")]
    public void IsDataProtectionKeysRequest_BlocksTheKeysFolder_RegardlessOfCase(string path)
    {
        Assert.True(InfrastructureExtensions.IsDataProtectionKeysRequest(new PathString(path), "/uploads"));
    }

    [Theory]
    [InlineData("/uploads/products/ef226c2c/1d44fe40.png")]
    [InlineData("/uploads/catalog-imports/report.xlsx")]
    [InlineData("/uploads")]
    [InlineData("/api/v1/account/notifications")]
    public void IsDataProtectionKeysRequest_AllowsEverythingElse(string path)
    {
        Assert.False(InfrastructureExtensions.IsDataProtectionKeysRequest(new PathString(path), "/uploads"));
    }

    [Fact]
    public void IsDataProtectionKeysRequest_DoesNotMatchOnSubstringAlone_OnlyOnPathSegment()
    {
        // A folder that merely starts with the same characters (not a real path-segment match)
        // must not be blocked — StartsWithSegments, not a raw string prefix check.
        Assert.False(InfrastructureExtensions.IsDataProtectionKeysRequest(
            new PathString("/uploads/dataprotection-keys-backup/file.xml"), "/uploads"));
    }
}
