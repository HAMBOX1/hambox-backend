namespace HAMBOX.Domain.Entities;

/// <summary>
/// An audit record that carries where the action came from. The persistence layer stamps the caller's network
/// address and device onto new rows automatically, so no individual handler can forget to (audit R43).
/// </summary>
public interface IRequestOriginStamped
{
    /// <summary>Called once, for a newly added row, with the current request's origin when it is known.</summary>
    void StampOrigin(string? ipAddress, string? userAgent);
}
