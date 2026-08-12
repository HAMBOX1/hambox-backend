using HAMBOX.Application.Communication;
using HAMBOX.SharedKernel.Results;

namespace HAMBOX.UnitTests.Commerce.TestDoubles;

internal sealed class FakeCommunicationService : ICommunicationService
{
    public Task<Result> SendAsync(CommunicationRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Success());
}
