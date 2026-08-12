using HAMBOX.Modules.Commerce.Application.Abstractions;

namespace HAMBOX.UnitTests.Commerce.TestDoubles;

/// <summary>Just runs the delegate — EF Core InMemory has no real transaction semantics to
/// coordinate across two db contexts, so there's nothing to wrap here for a unit test.</summary>
internal sealed class FakeCommerceTransactionService : ICommerceTransactionService
{
    public Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken) =>
        action(cancellationToken);
}
