using HAMBOX.SharedKernel.Results;

namespace HAMBOX.Modules.Communication.Application.Abstractions;

public sealed record RenderedTemplate(string Subject, string Body);

/// <summary>
/// Renders the active published version of a template for one channel/language, substituting
/// <c>{{VariableName}}</c> placeholders. Plain string substitution — the brief's "Variables" requirement
/// doesn't need a templating-engine dependency.
/// </summary>
public interface ICommunicationTemplateRenderer
{
    Task<Result<RenderedTemplate>> RenderAsync(
        string templateKey,
        string channel,
        string languageCode,
        IReadOnlyDictionary<string, string> variables,
        CancellationToken cancellationToken = default);
}
