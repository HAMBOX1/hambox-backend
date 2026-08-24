using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Threading.RateLimiting;
using FluentValidation;
using HAMBOX.Application.Abstractions;
using HAMBOX.Application.BackgroundJobs;
using HAMBOX.Infrastructure.Persistence.Interceptors;
using HAMBOX.Modules.Identity.Application.Authorization;
using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Features.Register;
using HAMBOX.Modules.Identity.Application.Options;
using HAMBOX.Modules.Identity.Application.RateLimiting;
using HAMBOX.Modules.Identity.Domain.Users;
using HAMBOX.Modules.Identity.Infrastructure.Authentication;
using HAMBOX.Modules.Identity.Infrastructure.BackgroundJobs;
using HAMBOX.Modules.Identity.Infrastructure.Localization;
using HAMBOX.Modules.Identity.Infrastructure.Middleware;
using HAMBOX.Modules.Identity.Infrastructure.Persistence;
using HAMBOX.Modules.Identity.Infrastructure.PlatformSettings;
using HAMBOX.Modules.Identity.Infrastructure.Services;
using HamboxSecurityStampValidator = HAMBOX.Modules.Identity.Infrastructure.Authentication.SecurityStampValidator;
using IHamboxSecurityStampValidator = HAMBOX.Modules.Identity.Application.Abstractions.ISecurityStampValidator;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace HAMBOX.Modules.Identity.Infrastructure.Extensions;

/// <summary>
/// Extension methods for registering Identity module infrastructure services.
/// </summary>
public static class IdentityInfrastructureExtensions
{
    /// <summary>
    /// Registers database context, authentication, security settings, and services for the Identity module.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddIdentityInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 1. Configure DbContext
        var connectionString = configuration.GetConnectionString("Database");
        services.AddDbContext<IdentityDbContext>((sp, options) =>
            options.UseSqlServer(connectionString,
                o => o.MigrationsHistoryTable("__EFMigrationsHistory", "identity"))
            .AddInterceptors(
                sp.GetRequiredService<SoftDeleteInterceptor>(),
                sp.GetRequiredService<AuditInterceptor>()));

        services.AddScoped<IIdentityDbContext>(sp => sp.GetRequiredService<IdentityDbContext>());

        // 2. Configure JWT Settings and Authentication
        var jwtSettingsSection = configuration.GetSection(JwtSettings.SectionName);
        services.Configure<JwtSettings>(jwtSettingsSection);
        services.Configure<LockoutSettings>(configuration.GetSection(LockoutSettings.SectionName));
        services.Configure<AdminOtpSettings>(configuration.GetSection(AdminOtpSettings.SectionName));
        services.Configure<GoogleAuthSettings>(configuration.GetSection(GoogleAuthSettings.SectionName));
        services.Configure<RefreshCookieSettings>(configuration.GetSection(RefreshCookieSettings.SectionName));

        // CSRF defense for the two endpoints that authenticate via the ambient refresh cookie
        // (refresh, logout) lives in CsrfCookieWriter (Identity.Presentation) — a plain
        // double-submit cookie built on the existing ITokenGenerator, deliberately not
        // Microsoft.AspNetCore.Antiforgery: that service pairs a cookie token with a
        // cryptographically-derived (not equal) header token, which Angular's built-in
        // withXsrfConfiguration interceptor — designed for the classic double-submit pattern, where
        // the header simply echoes the cookie verbatim — cannot produce without extra glue code.
        // Every other endpoint is Bearer-header-authenticated and therefore not CSRF-exposed (a
        // cross-site page can't make the victim's browser send an Authorization header it doesn't
        // already have), so nothing else needs this.

        // 2b. Rate limiting for brute-force/abuse-prone auth endpoints (per-client-IP, fixed window).
        // Complements the existing per-account lockout — this is a per-IP defense-in-depth layer.
        var rateLimitingSettings = configuration.GetSection(RateLimitingSettings.SectionName).Get<RateLimitingSettings>()
            ?? new RateLimitingSettings();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = (context, cancellationToken) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
                }

                return ValueTask.CompletedTask;
            };

            AddFixedWindowPolicy(options, RateLimitPolicies.Login, rateLimitingSettings.Login);
            AddFixedWindowPolicy(options, RateLimitPolicies.AccountActions, rateLimitingSettings.AccountActions);
        });

        var emailSettingsSection = configuration.GetSection(EmailSettings.SectionName);
        services.Configure<EmailSettings>(emailSettingsSection);
        services.AddSingleton<IValidateOptions<EmailSettings>, EmailSettingsValidator>();

        services.Configure<DevAdminSeedSettings>(configuration.GetSection(DevAdminSeedSettings.SectionName));
        services.AddScoped<DevAdminDataSeeder>();

        services.AddSingleton<IValidateOptions<JwtSettings>, JwtSettingsValidator>();

        var emailSettings = emailSettingsSection.Get<EmailSettings>() ?? new EmailSettings();
        var emailValidation = new EmailSettingsValidator().Validate(null, emailSettings);

        if (emailValidation.Failed)
        {
            throw new InvalidOperationException(emailValidation.FailureMessage);
        }

        var jwtSettings = jwtSettingsSection.Get<JwtSettings>() ?? new JwtSettings();
        var jwtValidation = new JwtSettingsValidator().Validate(null, jwtSettings);

        if (jwtValidation.Failed)
        {
            throw new InvalidOperationException(jwtValidation.FailureMessage);
        }

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtSettings.Issuer,
                ValidAudience = jwtSettings.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SecretKey)),
                ClockSkew = TimeSpan.Zero,
                RoleClaimType = IdentityClaimTypes.Role,
                NameClaimType = JwtRegisteredClaimNames.Email,
            };

            options.Events = new JwtBearerEvents
            {
                // SignalR's browser WebSocket/SSE transports can't set an Authorization header,
                // so the client appends ?access_token=... to the hub URL instead — this is the
                // standard ASP.NET Core SignalR JWT pattern, scoped to only the hub path prefix
                // so it never weakens auth for ordinary HTTP API calls.
                OnMessageReceived = context =>
                {
                    var accessToken = context.Request.Query["access_token"];
                    if (!string.IsNullOrEmpty(accessToken) && context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                    {
                        context.Token = accessToken;
                    }

                    return Task.CompletedTask;
                },
                OnTokenValidated = async context =>
                {
                    var userIdValue = context.Principal?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
                    var securityStamp = context.Principal?.FindFirst(IdentityClaimTypes.SecurityStamp)?.Value;
                    var sessionIdValue = context.Principal?.FindFirst(IdentityClaimTypes.SessionId)?.Value;

                    if (!Guid.TryParse(userIdValue, out var userId) || string.IsNullOrWhiteSpace(securityStamp))
                    {
                        context.Fail("The access token is missing required security claims.");
                        return;
                    }

                    var validator = context.HttpContext.RequestServices.GetRequiredService<IHamboxSecurityStampValidator>();
                    var isValid = await validator.ValidateAsync(
                        userId,
                        securityStamp,
                        context.HttpContext.RequestAborted);

                    if (!isValid)
                    {
                        context.Fail("The access token has been revoked.");
                        return;
                    }

                    if (Guid.TryParse(sessionIdValue, out var sessionId))
                    {
                        var sessionValidator = context.HttpContext.RequestServices.GetRequiredService<ISessionValidator>();
                        var sessionActive = await sessionValidator.IsSessionActiveAsync(
                            sessionId,
                            context.HttpContext.RequestAborted);

                        if (!sessionActive)
                        {
                            context.Fail("The session has ended.");
                        }
                    }
                }
            };
        });

        services.AddAuthorization(options =>
        {
            foreach (var permission in PermissionConstants.All)
            {
                options.AddPolicy(permission, policy =>
                    policy.AddRequirements(new PermissionRequirement(permission)));
            }

            options.AddPolicy(AuthorizationPolicies.PermissionMatrix, policy =>
                policy.AddRequirements(new AnyPermissionRequirement(
                    PermissionConstants.Permissions.View,
                    PermissionConstants.Roles.View)));

            options.AddPolicy(AuthorizationPolicies.CustomerContext, policy =>
                policy.RequireAuthenticatedUser().AddRequirements(new CustomerContextRequirement()));
        });

        // 3. Register Core Services
        services.AddSingleton<ITokenGenerator, TokenGenerator>();
        services.AddTransient<IPasswordHasher<ApplicationUser>, PasswordHasher<ApplicationUser>>();
        services.AddScoped<IPasswordHasher, PasswordHasherService>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<SmtpEmailService>();
        services.AddScoped<LoggingEmailService>();
        services.AddScoped<PlatformRoutingEmailService>();
        services.AddScoped<IEmailService>(sp => sp.GetRequiredService<PlatformRoutingEmailService>());
        services.AddScoped<IUserClaimsService, UserClaimsService>();
        services.AddScoped<IHamboxSecurityStampValidator, HamboxSecurityStampValidator>();
        services.AddScoped<ISessionValidator, SessionValidator>();
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddScoped<IAuthorizationHandler, AnyPermissionAuthorizationHandler>();
        services.AddScoped<IAuthorizationHandler, CustomerContextAuthorizationHandler>();
        services.AddScoped<IAdminAccessResolver, AdminAccessResolver>();
        services.AddSingleton<IOtpCodeGenerator, OtpCodeGenerator>();
        services.AddScoped<IAuthTokenIssuer, AuthTokenIssuer>();
        services.AddScoped<IGoogleTokenValidator, GoogleTokenValidator>();
        services.AddSingleton<IClientInfoParser, ClientInfoParser>();
        services.AddScoped<IUserLanguagePreferenceResolver, UserLanguagePreferenceResolver>();

        services.AddMemoryCache();
        services.AddDataProtection();
        services.AddSingleton<IPlatformSettingsSecretProtector, PlatformSettingsSecretProtector>();
        services.AddSingleton<IMaintenanceBypassTokenIssuer, MaintenanceBypassTokenIssuer>();
        services.AddScoped<IPlatformSettingsService, PlatformSettingsService>();
        services.AddScoped<IPlatformSettingsProvider>(sp => sp.GetRequiredService<IPlatformSettingsService>());

        services.AddScoped<IPermissionResolver, PermissionResolver>();
        services.AddScoped<IRbacAuthorizationService, RbacAuthorizationService>();
        services.AddScoped<IAuthorizationAuditService, AuthorizationAuditService>();
        services.AddScoped<IUserAuthorizationInvalidationService, UserAuthorizationInvalidationService>();
        services.AddScoped<ISecurityEventLogger, SecurityEventLogger>();
        services.AddScoped<ISecurityBlocklistService, SecurityBlocklistService>();
        services.AddSingleton<ILoginRiskScorer, LoginRiskScorer>();
        services.AddScoped<ITrustedDeviceService, TrustedDeviceService>();
        services.AddScoped<ISecurityAlertRecipientResolver, SecurityAlertRecipientResolver>();

        var geoIpDatabasePath = configuration["GeoIp:DatabasePath"];
        if (!string.IsNullOrWhiteSpace(geoIpDatabasePath) && File.Exists(geoIpDatabasePath))
        {
            services.AddSingleton(_ => new MaxMind.GeoIP2.DatabaseReader(geoIpDatabasePath));
            services.AddScoped<ICountryResolver, MaxMindGeoLocationResolver>();
        }
        else
        {
            services.AddScoped<ICountryResolver, NullCountryResolver>();
        }
        services.AddScoped<IBackgroundJobHandler, InvalidateUserSessionsJobHandler>();
        services.AddScoped<IBackgroundJobHandler, CleanupExpiredUserBlocksJobHandler>();
        services.AddScoped<IBackgroundJobHandler, CleanupExpiredIpBansJobHandler>();
        services.AddScoped<IBackgroundJobHandler, CleanupExpiredEmailBansJobHandler>();

        // 4. Register FluentValidation Validators
        services.AddValidatorsFromAssembly(typeof(RegisterCommandValidator).Assembly);

        return services;
    }

    private static void AddFixedWindowPolicy(RateLimiterOptions options, string policyName, RateLimitPolicySettings settings) =>
        options.AddPolicy(policyName, httpContext => RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = settings.PermitLimit,
                Window = TimeSpan.FromSeconds(settings.WindowSeconds),
                QueueLimit = settings.QueueLimit,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            }));
}
