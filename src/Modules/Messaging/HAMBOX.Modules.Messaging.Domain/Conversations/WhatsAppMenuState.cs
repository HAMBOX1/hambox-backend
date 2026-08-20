namespace HAMBOX.Modules.Messaging.Domain.Conversations;

/// <summary>
/// The finite set of menu states a <see cref="WhatsAppConversationSession"/> can be in. Plain string
/// constants (not an enum) so <see cref="WhatsAppConversationSession.CurrentMenu"/> stays a simple
/// persisted column and new states can be added without a migration.
/// </summary>
public static class WhatsAppMenuState
{
    public const string LanguageSelect = "LanguageSelect";
    public const string Main = "Main";
    public const string BrowseCategories = "BrowseCategories";
    public const string BrowseProducts = "BrowseProducts";
    public const string ProductDetail = "ProductDetail";
    public const string VariantSelect = "VariantSelect";
    public const string SearchPrompt = "SearchPrompt";
    public const string SearchResults = "SearchResults";
    public const string Cart = "Cart";
    public const string LinkEmailPrompt = "LinkEmailPrompt";
    public const string LinkCodePrompt = "LinkCodePrompt";
    public const string Orders = "Orders";
    public const string Alerts = "Alerts";
    public const string SupportMenu = "SupportMenu";
    public const string SupportCompose = "SupportCompose";
}
