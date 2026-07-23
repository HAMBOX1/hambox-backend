namespace HAMBOX.Modules.Catalog.Domain.Packaging;

/// <summary>
/// Background job type strings for <c>IBackgroundJobScheduler</c>, executed by Commerce's
/// <c>OperationalJobWorker</c> — the same cross-module job engine every other module uses.
/// </summary>
public static class CatalogJobTypes
{
    public const string ExportPackage = "Catalog.ExportPackage";
    public const string ImportPackage = "Catalog.ImportPackage";
}
