using HAMBOX.Modules.Suppliers.Application.Validation;

namespace HAMBOX.UnitTests.Suppliers;

public sealed class SupplierBaseUrlValidatorTests
{
    [Fact]
    public void Null_OrWhitespace_IsAllowed_SinceBaseUrlIsOptional()
    {
        Assert.True(SupplierBaseUrlValidator.IsAllowed(null, out _));
        Assert.True(SupplierBaseUrlValidator.IsAllowed("", out _));
        Assert.True(SupplierBaseUrlValidator.IsAllowed("   ", out _));
    }

    [Theory]
    [InlineData("https://api.real-supplier.example.com")]
    [InlineData("https://api.real-supplier.example.com/v2/")]
    [InlineData("https://203.0.113.10")] // public/documentation-range IPv4 literal, not private
    public void AllowedHttpsUrls_Pass(string url)
    {
        Assert.True(SupplierBaseUrlValidator.IsAllowed(url, out var error));
        Assert.Null(error);
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("://missing-scheme")]
    public void MalformedUrls_AreRejected(string url)
    {
        Assert.False(SupplierBaseUrlValidator.IsAllowed(url, out var error));
        Assert.NotNull(error);
    }

    [Theory]
    [InlineData("http://api.example.com")]
    [InlineData("ftp://api.example.com")]
    [InlineData("file:///etc/passwd")]
    public void NonHttpsSchemes_AreRejected(string url)
    {
        Assert.False(SupplierBaseUrlValidator.IsAllowed(url, out var error));
        Assert.Contains("HTTPS", error);
    }

    [Theory]
    [InlineData("https://localhost")]
    [InlineData("https://localhost:8443")]
    [InlineData("https://LOCALHOST")]
    [InlineData("https://foo.localhost")]
    [InlineData("https://internal-service.local")]
    [InlineData("https://0.0.0.0")]
    [InlineData("https://metadata.google.internal")]
    public void LocalhostShapedHostnames_AreRejected(string url)
    {
        Assert.False(SupplierBaseUrlValidator.IsAllowed(url, out var error));
        Assert.NotNull(error);
    }

    [Theory]
    [InlineData("https://127.0.0.1")]
    [InlineData("https://127.5.5.5")]
    [InlineData("https://10.0.0.1")]
    [InlineData("https://172.16.0.1")]
    [InlineData("https://172.31.255.255")]
    [InlineData("https://192.168.1.1")]
    [InlineData("https://169.254.169.254")] // cloud metadata endpoint
    [InlineData("https://100.64.0.1")] // carrier-grade NAT
    [InlineData("https://[::1]")]
    public void LoopbackPrivateAndLinkLocalIpLiterals_AreRejected(string url)
    {
        Assert.False(SupplierBaseUrlValidator.IsAllowed(url, out var error));
        Assert.NotNull(error);
    }

    [Theory]
    [InlineData("https://172.15.255.255")] // just outside 172.16.0.0/12
    [InlineData("https://172.32.0.1")] // just outside 172.16.0.0/12
    [InlineData("https://8.8.8.8")]
    public void AddressesOutsidePrivateRanges_AreAllowed(string url)
    {
        Assert.True(SupplierBaseUrlValidator.IsAllowed(url, out var error));
        Assert.Null(error);
    }
}
