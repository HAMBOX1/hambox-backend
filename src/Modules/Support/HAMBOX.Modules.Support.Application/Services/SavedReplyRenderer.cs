namespace HAMBOX.Modules.Support.Application.Services;

/// <summary>
/// Plain <c>{{VariableName}}</c> substitution for saved-reply bodies — the same "no templating
/// engine needed" approach as <c>ICommunicationTemplateRenderer</c>, reimplemented locally rather
/// than reused since that interface only renders registered Communication templates, not
/// arbitrary admin-authored strings.
/// </summary>
internal static class SavedReplyRenderer
{
    public static string Render(string body, IReadOnlyDictionary<string, string> variables)
    {
        var result = body;
        foreach (var (key, value) in variables)
        {
            result = result.Replace($"{{{{{key}}}}}", value, StringComparison.OrdinalIgnoreCase);
        }

        return result;
    }
}
