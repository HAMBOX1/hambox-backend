namespace HAMBOX.Modules.Content.Domain.LandingPages;

/// <summary>
/// What a <see cref="LandingPageTemplate"/> is a page for. <see cref="Homepage"/> templates have
/// no target (<c>TargetId</c> is null); <see cref="Product"/>/<see cref="Category"/> templates are
/// scoped to exactly one Catalog entity via <c>TargetId</c>.
/// </summary>
public enum LandingPageScope
{
    Homepage = 0,
    Product = 1,
    Category = 2,
}

public enum LandingPageAuditAction
{
    TemplateCreated = 0,
    DraftSaved = 1,
    Published = 2,
    DraftDiscarded = 3,
    Activated = 4,
    Duplicated = 5,
    Deleted = 6,
}
