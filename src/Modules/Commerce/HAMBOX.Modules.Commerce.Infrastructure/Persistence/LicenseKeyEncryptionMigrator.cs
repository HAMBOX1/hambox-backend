using System.Data;
using System.Security.Cryptography;
using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Commerce.Domain.Account;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HAMBOX.Modules.Commerce.Infrastructure.Persistence;

/// <summary>
/// One-time, idempotent backfill that encrypts any pre-existing plaintext license keys and backfills
/// <see cref="OrderLicenseKey.LicenseKeyHash"/> for legacy rows. Runs via raw ADO so it can inspect the
/// real column bytes regardless of the EF value converter (which would otherwise try to decrypt legacy
/// plaintext rows and throw). Safe to run every startup: an already-migrated row is left untouched.
/// </summary>
public sealed class LicenseKeyEncryptionMigrator(
    CommerceDbContext db,
    ICodeProtector protector,
    ILogger<LicenseKeyEncryptionMigrator> logger)
{
    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var migrator = new LicenseKeyEncryptionMigrator(
            scope.ServiceProvider.GetRequiredService<CommerceDbContext>(),
            scope.ServiceProvider.GetRequiredService<ICodeProtector>(),
            scope.ServiceProvider.GetRequiredService<ILogger<LicenseKeyEncryptionMigrator>>());

        await migrator.RunAsync(cancellationToken);
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        var rows = new List<(Guid Id, string LicenseKey, string LicenseKeyHash)>();

        await using (var select = connection.CreateCommand())
        {
            select.CommandText = "SELECT Id, LicenseKey, LicenseKeyHash FROM commerce.OrderLicenseKeys";
            await using var reader = await select.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add((
                    reader.GetGuid(0),
                    reader.GetString(1),
                    reader.IsDBNull(2) ? string.Empty : reader.GetString(2)));
            }
        }

        var migrated = 0;

        foreach (var row in rows)
        {
            string plaintext;
            string cipherText;

            try
            {
                protector.Unprotect(row.LicenseKey);
                plaintext = row.LicenseKey;
                cipherText = row.LicenseKey;
            }
            catch (CryptographicException)
            {
                if (ProtectedValueFormat.LooksAlreadyProtected(row.LicenseKey))
                {
                    logger.LogWarning("License key {Id} already encrypted but unreadable by the current key ring (key rotated?); left as-is.", row.Id);
                    continue;
                }

                plaintext = row.LicenseKey;
                cipherText = protector.Protect(row.LicenseKey);
            }

            var expectedHash = OrderLicenseKey.Hash(plaintext);
            var needsUpdate = cipherText != row.LicenseKey || row.LicenseKeyHash != expectedHash;

            if (!needsUpdate)
            {
                continue;
            }

            await using var update = connection.CreateCommand();
            update.CommandText =
                "UPDATE commerce.OrderLicenseKeys SET LicenseKey = @licenseKey, LicenseKeyHash = @licenseKeyHash WHERE Id = @id";
            AddParameter(update, "@licenseKey", cipherText);
            AddParameter(update, "@licenseKeyHash", expectedHash);
            AddParameter(update, "@id", row.Id);
            await update.ExecuteNonQueryAsync(cancellationToken);
            migrated++;
        }

        if (migrated > 0)
        {
            logger.LogInformation("Encrypted {Count} legacy plaintext license keys at rest.", migrated);
        }
    }

    private static void AddParameter(IDbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }
}
