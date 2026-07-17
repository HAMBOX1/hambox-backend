using HAMBOX.Modules.Communication.Application.Abstractions;
using HAMBOX.Modules.Communication.Application.Errors;
using HAMBOX.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Communication.Infrastructure.Services;

/// <summary>
/// Renders the active published version of a template with plain <c>{{Variable}}</c> substitution — no
/// templating-engine dependency, since the brief's "Variables" requirement doesn't need one. Falls back
/// to the English fields when the Arabic fields are empty, consistent with the codebase's existing
/// accepted Arabic-completeness gap.
/// </summary>
internal sealed class CommunicationTemplateRenderer(ICommunicationDbContext db) : ICommunicationTemplateRenderer
{
    public async Task<Result<RenderedTemplate>> RenderAsync(
        string templateKey,
        string channel,
        string languageCode,
        IReadOnlyDictionary<string, string> variables,
        CancellationToken cancellationToken = default)
    {
        var template = await db.CommunicationTemplates
            .Include(t => t.Versions)
            .FirstOrDefaultAsync(t => t.Key == templateKey && t.Channel == channel, cancellationToken);

        var activeVersion = template?.Versions.FirstOrDefault(v => v.Id == template.ActiveVersionId && v.IsPublished);
        if (activeVersion is null)
        {
            return Result.Failure<RenderedTemplate>(CommunicationErrors.TemplateNotFound);
        }

        var isArabic = string.Equals(languageCode, "ar", StringComparison.OrdinalIgnoreCase);

        var subject = isArabic && !string.IsNullOrWhiteSpace(activeVersion.SubjectAr) ? activeVersion.SubjectAr : activeVersion.SubjectEn;
        var body = isArabic && !string.IsNullOrWhiteSpace(activeVersion.BodyAr) ? activeVersion.BodyAr : activeVersion.BodyEn;

        return Result.Success(new RenderedTemplate(Substitute(subject, variables), Substitute(body, variables)));
    }

    private static string Substitute(string text, IReadOnlyDictionary<string, string> variables)
    {
        foreach (var (key, value) in variables)
        {
            text = text.Replace($"{{{{{key}}}}}", value);
        }

        return text;
    }
}
