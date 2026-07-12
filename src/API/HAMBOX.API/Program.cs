using Asp.Versioning;
using HAMBOX.API.Extensions;
using HAMBOX.Application.Behaviors;
using HAMBOX.Infrastructure.Extensions;
using HAMBOX.Infrastructure.Currency;
using HAMBOX.Infrastructure.Localization;
using HAMBOX.Infrastructure.Options;
using HAMBOX.Modules.Catalog.Application.Features.Categories.CreateCategory;
using HAMBOX.Modules.Catalog.Infrastructure.Extensions;
using HAMBOX.Modules.Catalog.Infrastructure.Persistence;
using HAMBOX.Modules.Catalog.Presentation.Extensions;
using HAMBOX.Modules.Commerce.Application.Features.Cart.GetCart;
using HAMBOX.Modules.Commerce.Application.Extensions;
using HAMBOX.Modules.Commerce.Infrastructure.Extensions;
using HAMBOX.Modules.Commerce.Infrastructure.Persistence;
using HAMBOX.Modules.Commerce.Presentation.Extensions;
using HAMBOX.Modules.Identity.Application.Features.Register;
using HAMBOX.Modules.Identity.Infrastructure.Extensions;
using HAMBOX.Modules.Identity.Infrastructure.Middleware;
using HAMBOX.Modules.Identity.Infrastructure.Persistence;
using HAMBOX.Modules.Identity.Presentation.Extensions;
using HAMBOX.Modules.Themes.Application.Features.Themes;
using HAMBOX.Modules.Themes.Infrastructure.Extensions;
using HAMBOX.Modules.Themes.Infrastructure.Persistence;
using HAMBOX.Modules.Themes.Presentation.Extensions;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.FileProviders;
using Serilog;
using System.Text.Json.Serialization;

// ──── Bootstrap Serilog ────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // ──── Serilog ──────────────────────────────────────────────
    builder.AddSerilog();

    // ──── API Versioning ──────────────────────────────────────
    builder.Services.AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new ApiVersion(1);
        options.ReportApiVersions = true;
        options.AssumeDefaultVersionWhenUnspecified = true;
        options.ApiVersionReader = new UrlSegmentApiVersionReader();
    }).AddApiExplorer(options =>
    {
        options.GroupNameFormat = "'v'V";
        options.SubstituteApiVersionInUrl = true;
    });

    // ──── Cross-Cutting Infrastructure ────────────────────────
    builder.Services.AddSharedInfrastructure(builder.Configuration);

    // ──── MediatR (single registration for all modules) ───────
    builder.Services.AddMediatR(config =>
    {
        config.RegisterServicesFromAssembly(typeof(RegisterCommand).Assembly);
        config.RegisterServicesFromAssembly(typeof(CreateCategoryCommand).Assembly);
        config.RegisterServicesFromAssembly(typeof(GetCartQuery).Assembly);
        config.RegisterServicesFromAssembly(typeof(GetThemesQuery).Assembly);
        config.AddOpenBehavior(typeof(LoggingBehavior<,>));
        config.AddOpenBehavior(typeof(ValidationBehavior<,>));
    });

    if (builder.Environment.IsDevelopment())
    {
        builder.Services.AddHamboxSwagger();
    }

    // ──── Module Registration ─────────────────────────────────
    builder.Services.AddIdentityInfrastructure(builder.Configuration);
    builder.Services.AddCatalogInfrastructure(builder.Configuration);
    builder.Services.AddCommerceInfrastructure(builder.Configuration);
    builder.Services.AddCommerceApplication();
    builder.Services.AddThemesInfrastructure(builder.Configuration);

    builder.Services.Configure<FormOptions>(options =>
    {
        options.MultipartBodyLengthLimit = 10 * 1024 * 1024;
    });

    builder.Services.ConfigureHttpJsonOptions(options =>
    {
        options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

    var app = builder.Build();

    app.UseForwardedHeaders(new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
    });

    // ──── Database Migrations (Dev-Only) ──────────────────────
    if (app.Environment.IsDevelopment())
    {
        await app.ApplyMigrationsAsync<IdentityDbContext>();
        await app.ApplyMigrationsAsync<CatalogDbContext>();
        await app.ApplyMigrationsAsync<CommerceDbContext>();
        await app.ApplyMigrationsAsync<ThemesDbContext>();
        await PromotionDataSeeder.SeedAsync(app.Services);
        await MembershipDataSeeder.SeedAsync(app.Services);
        await ThemeDataSeeder.SeedAsync(app.Services);
        await app.SeedIdentityDevelopmentDataAsync();
        app.UseHamboxSwagger();
    }
    else if (app.Environment.IsProduction())
    {
        await app.SeedProductionDemoDataAsync();
    }

    // ──── Middleware Pipeline (order matters) ──────────────────
    app.UseSerilogRequestLoggingMiddleware();
    app.UseCorrelationId();
    app.UseHamboxLocalization();
    app.UseExceptionHandler();
    app.UseResponseCompression();

    app.UseCors("HamboxCors");

    // Avoid 307 redirects to HTTPS in dev — they break the Angular proxy and strip Authorization headers.
    if (!app.Environment.IsDevelopment())
    {
        app.UseHttpsRedirection();
    }

    var fileStorageSettings = app.Configuration
        .GetSection(FileStorageSettings.SectionName)
        .Get<FileStorageSettings>() ?? new FileStorageSettings();

    var uploadsRoot = Path.IsPathRooted(fileStorageSettings.LocalRootPath)
        ? fileStorageSettings.LocalRootPath
        : Path.Combine(app.Environment.ContentRootPath, fileStorageSettings.LocalRootPath);

    if (!Directory.Exists(uploadsRoot))
    {
        Directory.CreateDirectory(uploadsRoot);
    }

    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(uploadsRoot),
        RequestPath = fileStorageSettings.PublicBasePath
    });

    app.UseAuthentication();
    app.UseApplyUserCulture();
    app.UseAuthorization();
    app.UseMiddleware<HAMBOX.Modules.Commerce.Infrastructure.Middleware.ApiRequestLoggingMiddleware>();
    app.UseMaintenanceMode();

    // ──── Endpoints ───────────────────────────────────────────
    app.MapIdentityEndpoints();
    app.MapCatalogEndpoints();
    app.MapCommerceEndpoints();
    app.MapThemesEndpoints();
    app.MapLocalizationEndpoints();
    app.MapHealthChecks("/health");

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
