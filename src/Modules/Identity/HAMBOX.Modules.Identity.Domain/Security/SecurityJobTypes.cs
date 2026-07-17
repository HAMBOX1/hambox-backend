namespace HAMBOX.Modules.Identity.Domain.Security;

/// <summary>
/// Job type strings for Security Center background jobs, dispatched through the shared
/// <c>IBackgroundJobScheduler</c>/<c>IBackgroundJobHandler</c> contracts (see BuildingBlocks
/// <c>BackgroundJobContracts.cs</c>) and executed by Commerce's <c>OperationalJobWorker</c> —
/// the same cross-module job engine every other module uses.
/// </summary>
public static class SecurityJobTypes
{
    public const string InvalidateUserSessions = "Security.InvalidateUserSessions";
    public const string CleanupExpiredUserBlocks = "Security.CleanupExpiredUserBlocks";
    public const string CleanupExpiredIpBans = "Security.CleanupExpiredIpBans";
    public const string CleanupExpiredEmailBans = "Security.CleanupExpiredEmailBans";
}
