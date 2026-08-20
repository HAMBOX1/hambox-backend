namespace HAMBOX.Modules.Suppliers.Application.BackgroundJobs;

/// <summary>
/// Mirrors the module-owns-its-job-type-constants convention already used by Commerce's
/// <c>OperationalJobTypes</c> — the string value is the actual contract with the shared job engine
/// (<c>IBackgroundJobHandlerRegistry</c> resolves handlers by this string), the constant just avoids
/// typos at the few call sites that need it.
/// </summary>
public static class SupplierFulfillmentJobTypes
{
    /// <summary>
    /// One recurring job type that sweeps every automated-supplier fulfillment attempt currently due
    /// for submission or reconciliation — not one job per attempt. See
    /// <see cref="HAMBOX.Modules.Suppliers.Application.Abstractions.ISupplierFulfillmentService.ProcessDueFulfillmentsAsync"/>
    /// for what a single sweep does.
    /// </summary>
    public const string Sweep = "SupplierFulfillmentSweep";
}
