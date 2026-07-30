using HAMBOX.SharedKernel.Errors;

namespace HAMBOX.Modules.Support.Application.Errors;

public static class SupportErrors
{
    public static readonly Error TicketNotFound = new(
        "Support.TicketNotFound", "The support ticket was not found.");

    public static readonly Error NotYourTicket = new(
        "Support.NotYourTicket", "This ticket does not belong to you.");

    public static readonly Error CannotReopen = new(
        "Support.CannotReopen", "This ticket can no longer be reopened.");

    public static readonly Error CannotRate = new(
        "Support.CannotRate", "Only resolved or closed tickets can be rated.");

    public static readonly Error AlreadyRated = new(
        "Support.AlreadyRated", "This ticket has already been rated.");

    public static readonly Error CannotMergeIntoSelf = new(
        "Support.CannotMergeIntoSelf", "A ticket cannot be merged into itself.");

    public static readonly Error TargetTicketNotFound = new(
        "Support.TargetTicketNotFound", "The target ticket was not found.");

    public static readonly Error CategoryNotFound = new(
        "Support.CategoryNotFound", "The ticket category was not found.");

    public static readonly Error PriorityNotFound = new(
        "Support.PriorityNotFound", "The ticket priority was not found.");

    public static readonly Error TagNotFound = new(
        "Support.TagNotFound", "The ticket tag was not found.");

    public static readonly Error TagAlreadyExists = new(
        "Support.TagAlreadyExists", "A tag with this name already exists.");

    public static readonly Error AttachmentTooLarge = new(
        "Support.AttachmentTooLarge", "The attachment exceeds the maximum allowed file size.");

    public static readonly Error AttachmentTypeNotAllowed = new(
        "Support.AttachmentTypeNotAllowed", "This attachment type is not allowed.");

    public static readonly Error AttachmentInfected = new(
        "Support.AttachmentInfected", "The attachment failed the security scan and was rejected.");

    public static readonly Error KnowledgeArticleNotFound = new(
        "Support.KnowledgeArticleNotFound", "The knowledge base article was not found.");

    public static readonly Error KnowledgeCategoryNotFound = new(
        "Support.KnowledgeCategoryNotFound", "The knowledge base category was not found.");

    public static readonly Error SavedReplyNotFound = new(
        "Support.SavedReplyNotFound", "The saved reply was not found.");

    public static readonly Error SavedReplyFolderNotFound = new(
        "Support.SavedReplyFolderNotFound", "The saved reply folder was not found.");
}
