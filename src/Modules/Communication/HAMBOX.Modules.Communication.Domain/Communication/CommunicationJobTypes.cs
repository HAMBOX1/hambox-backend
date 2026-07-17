namespace HAMBOX.Modules.Communication.Domain.Communication;

/// <summary>
/// Job type strings for Communication Platform background jobs, dispatched through the shared
/// <c>IBackgroundJobScheduler</c>/<c>IBackgroundJobHandler</c> contracts (BuildingBlocks
/// <c>BackgroundJobContracts.cs</c>) and executed by Commerce's <c>OperationalJobWorker</c> — the same
/// cross-module job engine every other module uses. No new worker is introduced.
/// </summary>
public static class CommunicationJobTypes
{
    public const string Deliver = "Communication.Deliver";
}
