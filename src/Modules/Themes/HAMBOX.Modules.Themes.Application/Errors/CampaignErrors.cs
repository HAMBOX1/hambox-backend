using HAMBOX.SharedKernel.Errors;

namespace HAMBOX.Modules.Themes.Application.Errors;

public static class CampaignErrors
{
    public static readonly Error CampaignNotFound = new("Campaign.NotFound", "Campaign not found.");
    public static readonly Error ThemeNotFound = new("Campaign.ThemeNotFound", "The selected theme does not exist.");
    public static readonly Error ThemeNotPublishable = new(
        "Campaign.ThemeNotPublishable",
        "The selected theme must be published before this campaign can be published. Publish the theme first, then publish the campaign.");
    public static readonly Error AlreadyPublished = new("Campaign.AlreadyPublished", "Campaign is already published.");
    public static readonly Error NotDraft = new("Campaign.NotDraft", "Only a draft campaign can be published.");
    public static readonly Error CannotDeleteWhileLive = new(
        "Campaign.CannotDeleteWhileLive",
        "A published, enabled campaign cannot be deleted directly. Disable or archive it first.");
    public static readonly Error ConcurrencyConflict = new(
        "Campaign.ConcurrencyConflict",
        "This campaign was changed by someone else. Reload and try again.");
}
