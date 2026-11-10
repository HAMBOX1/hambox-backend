namespace HAMBOX.Modules.Catalog.Application.Services;

/// <summary>
/// Escapes a text value for a CSV cell. Besides quoting, it neutralises spreadsheet formula injection:
/// a cell starting with = + - @ (or a tab/CR) is prefixed with an apostrophe so Excel/Sheets treat it as text.
/// </summary>
public static class CsvCell
{
    public static string Escape(string? value)
    {
        value ??= string.Empty;

        if (value.Length > 0 && value[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
        {
            value = "'" + value;
        }

        return value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
    }
}
