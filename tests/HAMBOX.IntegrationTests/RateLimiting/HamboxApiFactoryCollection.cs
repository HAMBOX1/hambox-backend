namespace HAMBOX.IntegrationTests.RateLimiting;

/// <summary>
/// <see cref="HamboxRateLimitWebApplicationFactory"/> sets process-wide environment variables
/// (<c>ConnectionStrings__Database</c>, <c>JwtSettings__SecretKey</c>) in its constructor, read by
/// <c>WebApplication.CreateBuilder(args)</c> whenever the host actually boots (lazily, on first
/// <c>Services</c>/<c>CreateClient()</c> access). xUnit parallelizes across test classes by default,
/// so two classes each constructing their own factory concurrently can race on those env vars —
/// one instance's connection string can leak into another's host. Every test class that uses this
/// factory must carry <c>[Collection(Name)]</c> so xUnit runs them sequentially relative to each
/// other (they still run in parallel with unrelated collections).
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class HamboxApiFactoryCollection
{
    public const string Name = "HamboxApiFactory (sequential — shared process env vars)";
}
