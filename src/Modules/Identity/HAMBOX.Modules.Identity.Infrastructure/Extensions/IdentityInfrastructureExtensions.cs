using System.IdentityModel.Tokens.Jwt;
using System.Text;
using FluentValidation;
using HAMBOX.Application.Abstractions;
using HAMBOX.Infrastructure.Persistence.Interceptors;
using HAMBOX.Modules.Identity.Application.Authorization;
using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Features.Register;
using HAMBOX.Modules.Identity.Application.Options;
using HAMBOX.Modules.Identity.Domain.Users;
using HAMBOX.Modules.Identity.Infrastructure.Authentication;
using HAMBOX.Modules.Identity.Infrastructure.Localization;
using HAMBOX.Modules.Identity.Infrastructure.Middleware;
using HAMBOX.Modules.Identity.Infrastructure.Persistence;
using HAMBOX.Modules.Identity.Infrastructure.PlatformSettings;
using HAMBOX.Modules.Identity.Infrastructure.Services;
using HamboxSecurityStampValidator = HAMBOX.Modules.Identity.Infrastructure.Authentication.SecurityStampValidator;
using IHamboxSecurityStampValidator = HAMBOX.Modules.Identity.Application.Abstractions.ISecurityStampValidator;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
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
                policy.AddRequirements(new CustomerContextRequirement()));
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
        services.AddSingleton<IClientInfoParser, ClientInfoParser>();
        services.AddScoped<IUserLanguagePreferenceResolver, UserLanguagePreferenceResolver>();

        services.AddMemoryCache();
        services.AddDataProtection();
        services.AddSingleton<IPlatformSettingsSecretProtector, PlatformSettingsSecretProtector>();
        services.AddScoped<IPlatformSettingsService, PlatformSettingsService>();
        services.AddScoped<IPlatformSettingsProvider>(sp => sp.GetRequiredService<IPlatformSettingsService>());

        services.AddScoped<IPermissionResolver, PermissionResolver>();
        services.AddScoped<IRbacAuthorizationService, RbacAuthorizationService>();
        services.AddScoped<IAuthorizationAuditService, AuthorizationAuditService>();
        services.AddScoped<IUserAuthorizationInvalidationService, UserAuthorizationInvalidationService>();

        // 4. Register FluentValidation Validators
        services.AddValidatorsFromAssembly(typeof(RegisterCommandValidator).Assembly);

        return services;
    }
}
