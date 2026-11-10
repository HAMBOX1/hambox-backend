using System.Globalization;
using HAMBOX.Infrastructure.Localization;
using HAMBOX.Infrastructure.Resources;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace HAMBOX.UnitTests.Infrastructure;

public sealed class SharedResourcesLocalizationTests
{
    [Fact]
    public void ErrorTitles_ResolveFromTheResourceFile_InBothLanguages()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHamboxLocalization();
        using var provider = services.BuildServiceProvider();
        var localizer = provider.GetRequiredService<IStringLocalizer<SharedResources>>();

        var original = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo("en");
            var english = localizer["Errors.BadRequest.Title"];
            Assert.False(english.ResourceNotFound, "English text missing — the resource file is not being found.");
            Assert.Equal("Bad Request", english.Value);

            CultureInfo.CurrentUICulture = new CultureInfo("ar");
            Assert.False(localizer["Errors.BadRequest.Title"].ResourceNotFound);
        }
        finally
        {
            CultureInfo.CurrentUICulture = original;
        }
    }
}
