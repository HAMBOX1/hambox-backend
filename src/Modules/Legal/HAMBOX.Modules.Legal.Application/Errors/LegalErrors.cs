using HAMBOX.SharedKernel.Errors;

namespace HAMBOX.Modules.Legal.Application.Errors;

public static class LegalErrors
{
    public static readonly Error SectionNotFound = new("Legal.SectionNotFound", "Legal section not found.");
    public static readonly Error SlugAlreadyExists = new("Legal.SlugAlreadyExists", "A legal section with this slug already exists.");
    public static readonly Error NoVersion = new("Legal.NoVersion", "Legal section has no versions.");
    public static readonly Error InvalidSlug = new("Legal.InvalidSlug", "Slug must be lowercase letters, numbers, and hyphens only.");
}
