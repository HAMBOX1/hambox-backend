using HAMBOX.Infrastructure.Resources;
using HAMBOX.SharedKernel.Errors;
using Microsoft.Extensions.Localization;

namespace HAMBOX.Infrastructure.Localization;

/// <inheritdoc />
internal sealed class LocalizedErrorService(IStringLocalizer<SharedResources> localizer) : ILocalizedErrorService
{
    /// <inheritdoc />
    public string Localize(Error error)
    {
        var key = $"Errors.{error.Code.Replace('.', '_')}";
        var localized = localizer[key];

        return localized.ResourceNotFound ? error.Description : localized.Value;
    }
}
