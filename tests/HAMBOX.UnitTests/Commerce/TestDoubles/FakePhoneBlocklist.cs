using HAMBOX.Application.Security;

namespace HAMBOX.UnitTests.Commerce.TestDoubles;

/// <summary>An <see cref="IPhoneBlocklist"/> that blocks nothing unless a number is added to <see cref="Blocked"/>.</summary>
internal sealed class FakePhoneBlocklist : IPhoneBlocklist
{
    public HashSet<string> Blocked { get; } = [];

    public Task<bool> IsPhoneBlockedAsync(string phoneNumber, CancellationToken cancellationToken = default) =>
        Task.FromResult(Blocked.Contains(phoneNumber));
}
