using HAMBOX.SharedKernel.Errors;

namespace HAMBOX.Infrastructure.Localization;

/// <summary>
/// Localizes API error descriptions using the current request culture.
/// </summary>
public interface ILocalizedErrorService
{
  /// <summary>
  /// Returns a localized description for the given error, falling back to <see cref="Error.Description"/>.
  /// </summary>
  string Localize(Error error);
}
