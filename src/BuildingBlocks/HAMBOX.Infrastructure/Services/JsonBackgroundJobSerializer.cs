using System.Text.Json;
using HAMBOX.Application.BackgroundJobs;

namespace HAMBOX.Infrastructure.Services;

internal sealed class JsonBackgroundJobSerializer : IBackgroundJobSerializer
{
    public string Serialize<TPayload>(TPayload payload) => JsonSerializer.Serialize(payload);

    public TPayload? Deserialize<TPayload>(string json) => JsonSerializer.Deserialize<TPayload>(json);
}
