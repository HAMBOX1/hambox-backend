namespace HAMBOX.Modules.Content.Domain.Faqs;

/// <summary>
/// What a <see cref="Faq"/> applies to. <see cref="Global"/> FAQs have no target (<c>TargetId</c> is
/// null) and are shown everywhere as a fallback; <see cref="Product"/>/<see cref="Category"/> FAQs are
/// scoped to exactly one Catalog entity via <c>TargetId</c> and are shown only alongside that target's
/// Global FAQs (never leaking to a different product/category).
/// </summary>
public enum FaqScope
{
    Global = 0,
    Product = 1,
    Category = 2,
}

public enum FaqAuditAction
{
    Created = 0,
    Updated = 1,
    Published = 2,
    Unpublished = 3,
    Duplicated = 4,
    Reordered = 5,
    Deleted = 6,
}
