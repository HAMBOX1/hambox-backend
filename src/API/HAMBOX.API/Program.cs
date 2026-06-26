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
using HAMBOX.Modules.Commerce.Infrastructure.Extensions;
using HAMBOX.Modules.Commerce.Infrastructure.Persistence;
using HAMBOX.Modules.Commerce.Presentation.Extensions;
using HAMBOX.Modules.Identity.Application.Features.Register;
using HAMBOX.Modules.Identity.Infrastructure.Extensions;
using HAMBOX.Modules.Identity.Infrastructure.Persistence;
using HAMBOX.Modules.Identity.Presentation.Extensions;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.FileProviders;
using Serilog;

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

    builder.Services.Configure<FormOptions>(options =>
    {
        options.MultipartBodyLengthLimit = 10 * 1024 * 1024;
    });

    var app = builder.Build();

    // ──── Database Migrations (Dev-Only) ──────────────────────
    if (app.Environment.IsDevelopment())
    {
        await app.ApplyMigrationsAsync<IdentityDbContext>();
        await app.ApplyMigrationsAsync<CatalogDbContext>();
        await app.ApplyMigrationsAsync<CommerceDbContext>();
        await app.SeedIdentityDevelopmentDataAsync();
        app.UseHamboxSwagger();
    }

    // ──── Middleware Pipeline (order matters) ──────────────────
    app.UseSerilogRequestLoggingMiddleware();
    app.UseCorrelationId();
    app.UseHamboxLocalization();
    app.UseExceptionHandler();
    app.UseResponseCompression();

    app.UseCors("HamboxCors");
    app.UseHttpsRedirection();

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

    // ──── Endpoints ───────────────────────────────────────────
    app.MapIdentityEndpoints();
    app.MapCatalogEndpoints();
    app.MapCommerceEndpoints();
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
