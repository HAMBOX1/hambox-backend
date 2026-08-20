using System.Text.Json;
using HAMBOX.Application.BackgroundJobs;

namespace HAMBOX.UnitTests.Commerce.TestDoubles;

/// <summary>Trivial JSON-backed <see cref="IBackgroundJobSerializer"/> — job handler tests call
/// <c>HandleAsync</c> directly (bypassing <c>ExecuteRawAsync</c>), so this is only needed to satisfy
/// the handler's constructor, never actually exercised.</summary>
internal sealed class FakeBackgroundJobSerializer : IBackgroundJobSerializer
{
    public string Serialize<TPayload>(TPayload payload) => JsonSerializer.Serialize(payload);

    public TPayload? Deserialize<TPayload>(string json) => JsonSerializer.Deserialize<TPayload>(json);
}
