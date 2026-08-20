using HAMBOX.Modules.Messaging.Application.Abstractions;

namespace HAMBOX.UnitTests.Messaging.TestDoubles;

/// <summary>Not exercised by the Browse/Search/Cart scenario — no session in that flow is linked, so
/// <see cref="ActAsCustomer"/> is never called.</summary>
internal sealed class NoOpWhatsAppUserContextScope : IWhatsAppUserContextScope
{
    public IDisposable ActAsCustomer(string customerUserId) => throw new NotSupportedException("Not needed by these tests.");
}
