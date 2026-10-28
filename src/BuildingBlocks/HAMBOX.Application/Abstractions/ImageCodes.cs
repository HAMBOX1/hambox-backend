namespace HAMBOX.Application.Abstractions;

/// <summary>
/// A stock "code" can be a picture (e.g. a gift-card image) that the customer receives as an image. Such a code
/// is stored as <c>img:</c> + the storage key of the uploaded file, so the normal code pipeline (stock counting,
/// reservation, delivery, reveal rules) works unchanged; only the last step — showing it — renders a picture.
/// The files live under a private folder that the public <c>/uploads</c> route refuses to serve; the customer
/// fetches them through an authenticated endpoint that re-checks ownership.
/// </summary>
public static class ImageCodes
{
    /// <summary>Marks a stored code value as a stored picture rather than text.</summary>
    public const string Prefix = "img:";

    /// <summary>Storage folder for the pictures; the first segment (<see cref="PrivateSegment"/>) is never publicly served.</summary>
    public const string Folder = "_private/code-images";

    /// <summary>First path segment under the uploads root that the public file route must answer with 404.</summary>
    public const string PrivateSegment = "_private";

    public static bool IsImageCode(string? value) =>
        !string.IsNullOrEmpty(value) && value.StartsWith(Prefix, StringComparison.Ordinal);

    public static string ToCodeValue(string storageKey) => Prefix + storageKey;

    /// <summary>The storage key behind an image code, or null when the value is not an image code.</summary>
    public static string? ToStorageKey(string? value) => IsImageCode(value) ? value![Prefix.Length..] : null;

    /// <summary>True when <paramref name="storageKey"/> really is one of the private code pictures (guards path tricks).</summary>
    public static bool IsPrivateCodeImageKey(string storageKey) =>
        storageKey.StartsWith(Folder + "/", StringComparison.Ordinal)
        && !storageKey.Contains("..", StringComparison.Ordinal)
        && !storageKey.Contains('\\', StringComparison.Ordinal);
}
