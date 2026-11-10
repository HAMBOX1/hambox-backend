using HAMBOX.Application.Abstractions;

namespace HAMBOX.UnitTests.Commerce.TestDoubles;

/// <summary>Grants exactly the listed permissions (none when constructed empty, like an anonymous caller).</summary>
internal sealed class FakeCurrentUserPermissions(params string[] granted) : ICurrentUserPermissions
{
    public static readonly FakeCurrentUserPermissions None = new();

    public bool Has(string permission) => granted.Contains(permission);
}
