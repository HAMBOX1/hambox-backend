using HAMBOX.SharedKernel.Errors;

namespace HAMBOX.Modules.Communication.Application.Errors;

public static class CommunicationErrors
{
    public static readonly Error TemplateNotFound = new(
        "Communication.TemplateNotFound",
        "No active template is published for this key and channel.");

    public static readonly Error TemplateCategoryInvalid = new(
        "Communication.TemplateCategoryInvalid",
        "The template category is not valid.");

    public static readonly Error TemplateAlreadyExists = new(
        "Communication.TemplateAlreadyExists",
        "A template with this key and channel already exists.");

    public static readonly Error ChannelNotRegistered = new(
        "Communication.ChannelNotRegistered",
        "No communication provider is registered for this channel.");

    public static readonly Error MessageNotFound = new(
        "Communication.MessageNotFound",
        "The communication message was not found.");

    public static readonly Error TemplateVersionNotFound = new(
        "Communication.TemplateVersionNotFound",
        "The template version was not found.");

    public static readonly Error ProviderNotFound = new(
        "Communication.ProviderNotFound",
        "The communication provider configuration was not found.");
}
