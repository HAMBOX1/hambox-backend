namespace HAMBOX.Modules.Catalog.Application.Features.Inventory;

/// <summary>
/// Masks digital inventory codes/serials for any surface other than the dedicated reveal endpoint
/// (grids, search results, CSV exports). Keeps the last 4 characters visible, e.g. "************ABCD".
/// </summary>
internal static class InventoryCodeMasking
{
    private const int VisibleSuffixLength = 4;

    public static string Mask(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        // A multi-line code is a whole account block, so showing its last characters would leak part of a
        // password or 2FA code — hide it entirely.
        if (value.Contains('\n'))
        {
            return "********** (multi-line)";
        }

        return value.Length <= VisibleSuffixLength
            ? new string('*', value.Length)
            : new string('*', value.Length - VisibleSuffixLength) + value[^VisibleSuffixLength..];
    }
}
