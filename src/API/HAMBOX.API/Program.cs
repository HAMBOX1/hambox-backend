using System.Threading.RateLimiting;
using Asp.Versioning;
using HAMBOX.API.Extensions;
using HAMBOX.Application.Behaviors;
using HAMBOX.Modules.Commerce.Application.RateLimiting;
using HAMBOX.Application.BackgroundJobs;
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
using HAMBOX.Modules.Communication.Application.Features.Dashboard;
using HAMBOX.Modules.Communication.Infrastructure.Extensions;
using HAMBOX.Modules.Communication.Infrastructure.Persistence;
using HAMBOX.Modules.Communication.Presentation.Extensions;
using HAMBOX.Modules.Content.Application.Features.LandingPages.GetLandingPageTemplates;
using HAMBOX.Modules.Content.Infrastructure.Extensions;
using HAMBOX.Modules.Content.Infrastructure.Persistence;
using HAMBOX.Modules.Content.Presentation.Extensions;
using HAMBOX.Modules.Identity.Application.Features.Register;
using HAMBOX.Modules.Identity.Infrastructure.Extensions;
using HAMBOX.Modules.Identity.Domain.Security;
using HAMBOX.Modules.Identity.Infrastructure.Middleware;
using HAMBOX.Modules.Identity.Infrastructure.Persistence;
using HAMBOX.Modules.Identity.Presentation.Extensions;
using HAMBOX.Modules.Legal.Application.Features.Legal;
using HAMBOX.Modules.Legal.Infrastructure.Extensions;
using HAMBOX.Modules.Legal.Infrastructure.Persistence;
using HAMBOX.Modules.Legal.Presentation.Extensions;
using HAMBOX.Modules.Messaging.Application.Features.BotConfiguration.GetWhatsAppBotConfiguration;
using HAMBOX.Modules.Messaging.Infrastructure.Extensions;
using HAMBOX.Modules.Messaging.Infrastructure.Persistence;
using HAMBOX.Modules.Messaging.Presentation.Extensions;
using HAMBOX.Modules.Suppliers.Application.BackgroundJobs;
using HAMBOX.Modules.Suppliers.Application.Extensions;
using HAMBOX.Modules.Suppliers.Application.Features.Suppliers;
using HAMBOX.Modules.Suppliers.Infrastructure.Extensions;
using HAMBOX.Modules.Suppliers.Infrastructure.Persistence;
using HAMBOX.Modules.Suppliers.Presentation.Extensions;
using HAMBOX.Modules.Support.Application.Features.Tickets.GetTickets;
using HAMBOX.Modules.Support.Infrastructure.Extensions;
using HAMBOX.Modules.Support.Infrastructure.Persistence;
using HAMBOX.Modules.Support.Presentation.Extensions;
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
        config.RegisterServicesFromAssembly(typeof(GetLegalSectionsQuery).Assembly);
        config.RegisterServicesFromAssembly(typeof(GetSuppliersQuery).Assembly);
        config.RegisterServicesFromAssembly(typeof(GetCommunicationDashboardStatsQuery).Assembly);
        config.RegisterServicesFromAssembly(typeof(GetLandingPageTemplatesQuery).Assembly);
        config.RegisterServicesFromAssembly(typeof(GetTicketsQuery).Assembly);
        config.RegisterServicesFromAssembly(typeof(GetWhatsAppBotConfigurationQuery).Assembly);
        config.AddOpenBehavior(typeof(LoggingBehavior<,>));
        config.AddOpenBehavior(typeof(ValidationBehavior<,>));
        config.AddOpenBehavior(typeof(IdempotencyBehavior<,>));
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

    // Per-client-IP defense-in-depth on checkout/payment endpoints — additive to (never a
    // replacement for) the server-side payment verification/idempotency checks that already gate
    // these paths. Mirrors Identity's own fixed-window rate limiter registration (partitioned per
    // remote IP, not a single shared bucket — a global, non-partitioned limiter would let one
    // caller exhaust the limit for every other customer). Registered here rather than inside
    // AddCommerceInfrastructure: Commerce.Infrastructure is a plain class library and, in this
    // solution's current package graph, doesn't reliably resolve
    // Microsoft.AspNetCore.RateLimiting's extension methods on its own (Identity.Infrastructure's
    // identical call only compiles because it also references
    // Microsoft.AspNetCore.Authentication.JwtBearer for unrelated JWT setup, which incidentally
    // pulls in what's missing) — HAMBOX.API is the Web SDK host project, so it always has the full
    // ASP.NET Core surface. Values are deliberately generous so a customer retrying a failed card,
    // or a PSP resending a webhook, is never blocked.
    builder.Services.AddRateLimiter(options =>
    {
        options.AddPolicy(CommerceRateLimitPolicies.CheckoutInitiation, httpContext => RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromSeconds(60),
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            }));

        options.AddPolicy(CommerceRateLimitPolicies.PaymentCallback, httpContext => RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromSeconds(60),
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            }));
    });
    builder.Services.AddCommerceRealtime();
    builder.Services.AddThemesInfrastructure(builder.Configuration);
    builder.Services.AddLegalInfrastructure(builder.Configuration);
    builder.Services.AddSuppliersInfrastructure(builder.Configuration);
    builder.Services.AddSuppliersApplication();
    builder.Services.AddCommunicationInfrastructure(builder.Configuration);
    builder.Services.AddContentInfrastructure(builder.Configuration);
    builder.Services.AddSupportInfrastructure(builder.Configuration);
    builder.Services.AddSupportRealtime();
    builder.Services.AddMessagingInfrastructure(builder.Configuration);

    builder.Services.Configure<FormOptions>(options =>
    {
        options.MultipartBodyLengthLimit = 10 * 1024 * 1024;
    });

    builder.Services.ConfigureHttpJsonOptions(options =>
    {
        options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

    var app = builder.Build();

    // ──── Security Center recurring jobs (dispatched by Commerce's OperationalJobWorker) ────
    var recurringJobScheduler = app.Services.GetRequiredService<IRecurringJobScheduler>();
    recurringJobScheduler.Register(new RecurringJobDefinition(
        "security.cleanup-expired-user-blocks", SecurityJobTypes.CleanupExpiredUserBlocks, TimeSpan.FromMinutes(15)));
    recurringJobScheduler.Register(new RecurringJobDefinition(
        "security.cleanup-expired-ip-bans", SecurityJobTypes.CleanupExpiredIpBans, TimeSpan.FromMinutes(15)));
    recurringJobScheduler.Register(new RecurringJobDefinition(
        "security.cleanup-expired-email-bans", SecurityJobTypes.CleanupExpiredEmailBans, TimeSpan.FromMinutes(15)));
    recurringJobScheduler.Register(new RecurringJobDefinition(
        "suppliers.fulfillment-sweep", SupplierFulfillmentJobTypes.Sweep, TimeSpan.FromMinutes(2), Priority: BackgroundJobPriority.High));
    var availabilitySyncIntervalMinutes = app.Configuration.GetValue("SupplierAvailability:SyncIntervalMinutes", 5);
    recurringJobScheduler.Register(new RecurringJobDefinition(
        "suppliers.availability-sync", SupplierAvailabilityJobTypes.Sync, TimeSpan.FromMinutes(availabilitySyncIntervalMinutes)));

    // nginx runs on the host and proxies to this container's published port; Docker's own NAT makes
    // that connection appear to originate from the bridge network's gateway (not from loopback), so
    // the default KnownIPNetworks (loopback-only) never trusts nginx's X-Forwarded-For — every request
    // then reports RemoteIpAddress as the docker bridge gateway. This breaks anything keyed on the
    // real client IP: DotNotificationIpAllowlist (DOT/DOT Fawry webhook — every real call gets 403)
    // and the CheckoutInitiation/PaymentCallback rate limiters (every real caller collapses into one
    // shared partition, tripping 503s under normal traffic). Trust the private Docker bridge range so
    // the forwarded header is honored.
    var forwardedHeadersOptions = new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
    };
    forwardedHeadersOptions.KnownIPNetworks.Add(System.Net.IPNetwork.Parse("172.16.0.0/12"));
    app.UseForwardedHeaders(forwardedHeadersOptions);

    // ──── Database Migrations (Dev-Only) ──────────────────────
    if (app.Environment.IsDevelopment())
    {
        await app.ApplyMigrationsAsync<IdentityDbContext>();
        await app.ApplyMigrationsAsync<CatalogDbContext>();
        await app.ApplyMigrationsAsync<CommerceDbContext>();
        await app.ApplyMigrationsAsync<ThemesDbContext>();
        await app.ApplyMigrationsAsync<LegalDbContext>();
        await app.ApplyMigrationsAsync<SuppliersDbContext>();
        await app.ApplyMigrationsAsync<CommunicationDbContext>();
        await app.ApplyMigrationsAsync<ContentDbContext>();
        await app.ApplyMigrationsAsync<SupportDbContext>();
        await app.ApplyMigrationsAsync<MessagingDbContext>();
        await PermissionSynchronizer.SyncAsync(app.Services);
        await InventoryCodeEncryptionMigrator.SeedAsync(app.Services);
        await LicenseKeyEncryptionMigrator.SeedAsync(app.Services);
        await app.SeedProductionDemoDataAsync();
        await PromotionDataSeeder.SeedAsync(app.Services);
        await MembershipDataSeeder.SeedAsync(app.Services);
        await ThemeDataSeeder.SeedAsync(app.Services);
        await LegalDataSeeder.SeedAsync(app.Services);
        await LegalRequiredContentSeeder.SeedAsync(app.Services);
        await CommunicationDataSeeder.SeedAsync(app.Services);
        await LandingPageDataSeeder.SeedAsync(app.Services);
        await SupportDataSeeder.SeedAsync(app.Services);
        await MessagingCommunicationTemplateSeeder.SeedAsync(app.Services);
        await WhatsAppBotConfigurationSeeder.SeedAsync(app.Services);
        await app.SeedIdentityDevelopmentDataAsync();
        app.UseHamboxSwagger();
    }
    else if (app.Environment.IsProduction())
    {
        // Migrations are applied manually in Production; the schema (including LicenseKeyHash)
        // must already exist by the time this runs.
        await PermissionSynchronizer.SyncAsync(app.Services);
        await InventoryCodeEncryptionMigrator.SeedAsync(app.Services);
        await LicenseKeyEncryptionMigrator.SeedAsync(app.Services);
        await app.SeedProductionDemoDataAsync();
    }

    // ──── Middleware Pipeline (order matters) ──────────────────
    app.UseSerilogRequestLoggingMiddleware();
    app.UseCorrelationId();
    app.UseSecurityHeaders();
    app.UseCountryResolution();
    app.UseSecurityEnforcement();
    app.UseHamboxLocalization();
    app.UseExceptionHandler();
    app.UseResponseCompression();

    app.UseCors("HamboxCors");
    app.UseRateLimiter();

    // Avoid 307 redirects to HTTPS in dev — they break the Angular proxy and strip Authorization headers.
    if (!app.Environment.IsDevelopment())
    {
        app.UseHsts();
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

    // The DataProtection keyring (which encrypts every inventory code / license key at rest) is
    // persisted under this same uploads root so it survives redeploys — see the PersistKeysToFileSystem
    // call in AddSharedInfrastructure. It must never be servable over HTTP: block that one segment
    // before the static file middleware gets a chance to serve it.
    app.Use(async (context, next) =>
    {
        if (InfrastructureExtensions.IsDataProtectionKeysRequest(context.Request.Path, fileStorageSettings.PublicBasePath))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        await next();
    });

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
    app.MapLegalEndpoints();
    app.MapSuppliersEndpoints();
    app.MapCommunicationEndpoints();
    app.MapContentEndpoints();
    app.MapSupportEndpoints();
    app.MapSupportHub();
    app.MapMessagingEndpoints();
    app.MapNotificationHub();
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

/// <summary>
/// Exposes the top-level-statement-generated entry point so <c>WebApplicationFactory&lt;Program&gt;</c>
/// can boot this host in integration tests (see <c>HAMBOX.IntegrationTests</c>). Declaring this partial
/// class has no effect on runtime behavior.
/// </summary>
public partial class Program;
