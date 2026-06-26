namespace HAMBOX.Modules.Identity.Infrastructure.Services;

/// <summary>
/// Helpers for structured email logging.
/// </summary>
internal static class EmailLogHelper
{
    /// <summary>
    /// Masks an email address for information-level logs.
    /// </summary>
    public static string MaskEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return "unknown";
        }

        var atIndex = email.IndexOf('@');
        if (atIndex <= 0)
        {
            return "***";
        }

        var local = email[..atIndex];
        var domain = email[atIndex..];
        var visible = local.Length <= 1 ? "*" : $"{local[0]}***";
        return $"{visible}{domain}";
    }
}
