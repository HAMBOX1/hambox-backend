using HAMBOX.Application.Support;

namespace HAMBOX.UnitTests.Commerce.TestDoubles;

/// <summary>Records the delivery tickets a fulfillment flow asked for, without a Support module behind it.</summary>
internal sealed class FakeDeliveryTicketService : IDeliveryTicketService
{
    public List<DeliveryTicketRequest> Requests { get; } = [];

    public Task<Guid> CreateDeliveryTicketAsync(DeliveryTicketRequest request, CancellationToken cancellationToken = default)
    {
        Requests.Add(request);
        return Task.FromResult(Guid.NewGuid());
    }
}
