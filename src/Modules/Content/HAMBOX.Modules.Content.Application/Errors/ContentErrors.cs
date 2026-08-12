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

    public static readonly Error FaqNotFound = new(
        "Content.FaqNotFound",
        "The FAQ was not found.");

    public static readonly Error FaqCategoryNotFound = new(
        "Content.FaqCategoryNotFound",
        "The FAQ category was not found.");

    public static readonly Error FaqTargetProductNotFound = new(
        "Content.FaqTargetProductNotFound",
        "The product this FAQ targets does not exist.");

    public static readonly Error FaqTargetCategoryNotFound = new(
        "Content.FaqTargetCategoryNotFound",
        "The category this FAQ targets does not exist.");

    public static readonly Error FaqAnswerInvalid = new(
        "Content.FaqAnswerInvalid",
        "The answer contains no readable content once unsafe formatting is removed.");
}
