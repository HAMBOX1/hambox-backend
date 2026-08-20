using HAMBOX.Application.Communication;
using HAMBOX.SharedKernel.Results;

namespace HAMBOX.UnitTests.Commerce.TestDoubles;

internal sealed class FakeCommunicationService : ICommunicationService
{
    /// <summary>Every request handed to <see cref="SendAsync"/>, in call order — lets a test assert
    /// exactly who was notified, how many times, and with what template/category, without a real
    /// Communication module dependency.</summary>
    public List<CommunicationRequest> SentRequests { get; } = [];

    public Task<Result> SendAsync(CommunicationRequest request, CancellationToken cancellationToken = default)
    {
        SentRequests.Add(request);
        return Task.FromResult(Result.Success());
    }
}
