namespace HAMBOX.Modules.Commerce.Domain.Reports;

public static class ReportTypes
{
    public const string Sales = "Sales";
    public const string Revenue = "Revenue";
    public const string Orders = "Orders";
    public const string Inventory = "Inventory";
    public const string Products = "Products";
    public const string Categories = "Categories";
    public const string Membership = "Membership";
    public const string Promotion = "Promotion";
    public const string Coupon = "Coupon";
    public const string Referral = "Referral";
    public const string Customer = "Customer";
    public const string Operations = "Operations";
    public const string Audit = "Audit";

    public static readonly IReadOnlyList<string> All =
    [
        Sales, Revenue, Orders, Inventory, Products, Categories,
        Membership, Promotion, Coupon, Referral, Customer, Operations, Audit,
    ];
}

public static class ReportFormats
{
    public const string Pdf = "pdf";
    public const string Xlsx = "xlsx";
    public const string Csv = "csv";
    public const string Json = "json";

    public static readonly IReadOnlyList<string> All = [Pdf, Xlsx, Csv, Json];
}

public static class ReportScheduleFrequencies
{
    public const string Daily = "Daily";
    public const string Weekly = "Weekly";
    public const string Monthly = "Monthly";
}

public static class ScheduledReportExecutionStatuses
{
    public const string Pending = "Pending";
    public const string Running = "Running";
    public const string Succeeded = "Succeeded";
    public const string Failed = "Failed";
    public const string Skipped = "Skipped";
}

public static class ScheduledReportTriggers
{
    public const string Manual = "Manual";
    public const string Schedule = "Schedule";
}
