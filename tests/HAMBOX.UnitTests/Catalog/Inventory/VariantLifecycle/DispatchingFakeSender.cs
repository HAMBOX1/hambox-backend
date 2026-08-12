using MediatR;

namespace HAMBOX.UnitTests.Catalog.Inventory.VariantLifecycle;

/// <summary>
/// Minimal <see cref="ISender"/> that dispatches a single request type straight to a real handler
/// instance — enough for <c>BulkDeleteProductVariantsCommandHandler</c> (which only ever sends
/// <c>DeleteProductVariantCommand</c> through <see cref="ISender"/>) without pulling in the full
/// MediatR DI pipeline for a unit test.
/// </summary>
internal sealed class DispatchingFakeSender<TRequest, TResponse>(IRequestHandler<TRequest, TResponse> handler) : ISender
    where TRequest : IRequest<TResponse>
{
    public Task<TResponse1> Send<TResponse1>(IRequest<TResponse1> request, CancellationToken cancellationToken = default)
    {
        if (request is TRequest typed)
        {
            return (Task<TResponse1>)(object)handler.Handle(typed, cancellationToken);
        }

        throw new NotSupportedException($"This fake only dispatches {typeof(TRequest).Name}.");
    }

    public Task Send<TDispatchRequest>(TDispatchRequest request, CancellationToken cancellationToken = default)
        where TDispatchRequest : IRequest =>
        throw new NotSupportedException("Not needed by these tests.");

    public Task<object?> Send(object request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Not needed by these tests.");

    public IAsyncEnumerable<TResponse1> CreateStream<TResponse1>(IStreamRequest<TResponse1> request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Not needed by these tests.");

    public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Not needed by these tests.");
}
