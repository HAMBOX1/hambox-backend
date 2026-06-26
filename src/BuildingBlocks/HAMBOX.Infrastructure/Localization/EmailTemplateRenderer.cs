using HAMBOX.Application.Abstractions;
using HAMBOX.Infrastructure.Resources;
using Microsoft.Extensions.Localization;

namespace HAMBOX.Infrastructure.Localization;

/// <inheritdoc />
internal sealed class EmailTemplateRenderer(IStringLocalizer<SharedResources> localizer) : IEmailTemplateRenderer
{
    /// <inheritdoc />
    public Task<EmailTemplateContent> RenderAsync(
        string templateName,
        IReadOnlyDictionary<string, string> model,
        CancellationToken cancellationToken = default)
    {
        var subjectKey = $"Email.{templateName}.Subject";
        var bodyKey = $"Email.{templateName}.Body";

        var subject = localizer[subjectKey];
        var body = localizer[bodyKey];

        var subjectText = subject.ResourceNotFound
            ? templateName
            : ApplyTokens(subject.Value, model);

        var bodyText = body.ResourceNotFound
            ? $"<p>{templateName}</p>"
            : ApplyTokens(body.Value, model);

        return Task.FromResult(new EmailTemplateContent(subjectText, bodyText));
    }

    private static string ApplyTokens(string template, IReadOnlyDictionary<string, string> model)
    {
        var result = template;
        foreach (var (key, value) in model)
        {
            result = result.Replace($"{{{{{key}}}}}", value, StringComparison.Ordinal);
        }

        return result;
    }
}
