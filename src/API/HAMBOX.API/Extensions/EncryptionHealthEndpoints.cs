using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Catalog.Infrastructure.Persistence;
using HAMBOX.Modules.Commerce.Infrastructure.Persistence;
using HAMBOX.Modules.Identity.Application.Authorization;
using HAMBOX.Modules.Identity.Presentation.Extensions;
using HAMBOX.Modules.Suppliers.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.API.Extensions;

/// <summary>
/// Read-everything probe for data encrypted at rest. Digital codes, delivered license keys and supplier
/// credentials are protected with the ASP.NET Data Protection key ring; if that ring is ever lost or replaced
/// the affected rows silently turn into "[unreadable]" placeholders. This endpoint reads the raw stored value of
/// every protected column and reports how many can still be decrypted, so a lost key is noticed the day it
/// happens — not the day a customer is sold a code that cannot be shown. It never returns a secret.
/// </summary>
internal static class EncryptionHealthEndpoints
{
    private sealed record Source(string Name, string Schema, string Table, string Column, Func<IServiceProvider, DbContext> Context);

    private static readonly Source[] Sources =
    [
        new("Inventory codes", "catalog", "DigitalInventoryCodes", "DigitalCode", sp => sp.GetRequiredService<CatalogDbContext>()),
        new("Archived (deleted) codes", "catalog", "DeletedInventoryCodes", "DigitalCode", sp => sp.GetRequiredService<CatalogDbContext>()),
        new("Delivered license keys", "commerce", "OrderLicenseKeys", "LicenseKey", sp => sp.GetRequiredService<CommerceDbContext>()),
        new("Supplier API keys", "suppliers", "Suppliers", "ApiKey", sp => sp.GetRequiredService<SuppliersDbContext>()),
        new("Supplier API secrets", "suppliers", "Suppliers", "ApiSecret", sp => sp.GetRequiredService<SuppliersDbContext>()),
        new("Supplier passwords", "suppliers", "Suppliers", "Password", sp => sp.GetRequiredService<SuppliersDbContext>()),
        new("Supplier bearer tokens", "suppliers", "Suppliers", "BearerToken", sp => sp.GetRequiredService<SuppliersDbContext>()),
        new("Supplier OAuth settings", "suppliers", "Suppliers", "OAuthSettingsJson", sp => sp.GetRequiredService<SuppliersDbContext>()),
    ];

    public static IEndpointRouteBuilder MapEncryptionHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/system/encryption-health", async (
            IServiceProvider services,
            ICodeProtector protector,
            [FromQuery] int? sample,
            CancellationToken cancellationToken) =>
        {
            var results = new List<object>();
            var anyUnreadable = false;

            foreach (var source in Sources)
            {
                var db = source.Context(services);
                var sql = $"SELECT [{source.Column}] AS [Value] FROM [{source.Schema}].[{source.Table}] WHERE [{source.Column}] IS NOT NULL";
                if (sample is > 0)
                {
                    sql = $"SELECT TOP ({sample}) [{source.Column}] AS [Value] FROM [{source.Schema}].[{source.Table}] WHERE [{source.Column}] IS NOT NULL";
                }

                var values = await db.Database.SqlQueryRaw<string>(sql).ToListAsync(cancellationToken);

                int readable = 0, unreadable = 0, plaintext = 0;
                foreach (var value in values)
                {
                    if (!ProtectedValueFormat.LooksAlreadyProtected(value))
                    {
                        plaintext++;
                        continue;
                    }

                    try
                    {
                        protector.Unprotect(value);
                        readable++;
                    }
                    catch (Exception)
                    {
                        unreadable++;
                    }
                }

                anyUnreadable |= unreadable > 0;
                results.Add(new
                {
                    source = source.Name,
                    checkedRows = values.Count,
                    readable,
                    unreadable,
                    storedAsPlaintext = plaintext,
                });
            }

            return Results.Ok(new
            {
                status = anyUnreadable ? "Degraded" : "Healthy",
                checkedAtUtc = DateTimeOffset.UtcNow,
                sampled = sample is > 0,
                sources = results,
            });
        })
        .WithName("GetEncryptionHealth")
        .RequirePermission(PermissionConstants.Security.View);

        return app;
    }
}
