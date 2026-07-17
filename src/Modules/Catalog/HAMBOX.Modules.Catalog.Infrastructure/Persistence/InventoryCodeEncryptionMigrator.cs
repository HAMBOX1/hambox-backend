using System.Data;
using System.Security.Cryptography;
using HAMBOX.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HAMBOX.Modules.Catalog.Infrastructure.Persistence;

/// <summary>
/// One-time, idempotent backfill that encrypts any pre-existing plaintext digital inventory codes.
/// Runs via raw ADO so it can inspect the real column bytes regardless of the EF value converter
/// (which would otherwise try to decrypt legacy plaintext rows and throw). Safe to run every startup:
/// a row already holding ciphertext unprotects successfully and is left untouched.
/// </summary>
public sealed class InventoryCodeEncryptionMigrator(
    CatalogDbContext db,
    ICodeProtector protector,
    ILogger<InventoryCodeEncryptionMigrator> logger)
{
    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var migrator = new InventoryCodeEncryptionMigrator(
            scope.ServiceProvider.GetRequiredService<CatalogDbContext>(),
            scope.ServiceProvider.GetRequiredService<ICodeProtector>(),
            scope.ServiceProvider.GetRequiredService<ILogger<InventoryCodeEncryptionMigrator>>());

        await migrator.RunAsync(cancellationToken);
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        var rows = new List<(Guid Id, string DigitalCode, string? SerialNumber, string? Pin)>();

        await using (var select = connection.CreateCommand())
        {
            select.CommandText = "SELECT Id, DigitalCode, SerialNumber, Pin FROM catalog.DigitalInventoryCodes";
            await using var reader = await select.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add((
                    reader.GetGuid(0),
                    reader.GetString(1),
                    reader.IsDBNull(2) ? null : reader.GetString(2),
                    reader.IsDBNull(3) ? null : reader.GetString(3)));
            }
        }

        var migrated = 0;

        foreach (var row in rows)
        {
            var (needsUpdate, digitalCode) = ProtectIfPlaintext(row.DigitalCode);
            var (serialNeedsUpdate, serialNumber) = ProtectIfPlaintext(row.SerialNumber);
            var (pinNeedsUpdate, pin) = ProtectIfPlaintext(row.Pin);

            if (!needsUpdate && !serialNeedsUpdate && !pinNeedsUpdate)
            {
                continue;
            }

            await using var update = connection.CreateCommand();
            update.CommandText =
                "UPDATE catalog.DigitalInventoryCodes SET DigitalCode = @digitalCode, SerialNumber = @serialNumber, Pin = @pin WHERE Id = @id";
            AddParameter(update, "@digitalCode", digitalCode);
            AddParameter(update, "@serialNumber", serialNumber);
            AddParameter(update, "@pin", pin);
            AddParameter(update, "@id", row.Id);
            await update.ExecuteNonQueryAsync(cancellationToken);
            migrated++;
        }

        if (migrated > 0)
        {
            logger.LogInformation("Encrypted {Count} legacy plaintext digital inventory codes at rest.", migrated);
        }
    }

    private (bool NeedsUpdate, string? Value) ProtectIfPlaintext(string? value)
    {
        if (value is null)
        {
            return (false, null);
        }

        try
        {
            protector.Unprotect(value);
            return (false, value);
        }
        catch (CryptographicException)
        {
            return (true, protector.Protect(value));
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
