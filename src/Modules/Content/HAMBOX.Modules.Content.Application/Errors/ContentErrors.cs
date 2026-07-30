using HAMBOX.SharedKernel.Errors;

namespace HAMBOX.Modules.Content.Application.Errors;

public static class ContentErrors
{
    public static readonly Error TemplateNotFound = new(
        "Content.TemplateNotFound",
        "The landing page template was not found.");

    public static readonly Error NoDraftToPublish = new(
        "Content.NoDraftToPublish",
        "This template has no unpublished draft to publish.");

    public static readonly Error CannotDeleteActiveTemplate = new(
        "Content.CannotDeleteActiveTemplate",
        "The active landing page template cannot be deleted.");

    public static readonly Error CannotDeleteLastTemplate = new(
        "Content.CannotDeleteLastTemplate",
        "The last remaining landing page template cannot be deleted.");

    public static readonly Error NoActiveTemplate = new(
        "Content.NoActiveTemplate",
        "No active landing page template is configured.");

    public static readonly Error InvalidImage = new(
        "Content.InvalidImage",
        "The uploaded file is not a valid image, or exceeds the maximum allowed size.");
}
