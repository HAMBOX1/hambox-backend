using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Commerce.Domain.Operations;
using HAMBOX.Modules.Commerce.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.IntegrationTests.Commerce;

/// <summary>
/// Real, executable proof of the backup/restore procedure documented in
/// <c>docs/BACKUP_RESTORE_RUNBOOK.md</c> — runs an actual T-SQL <c>BACKUP DATABASE</c>/
/// <c>RESTORE DATABASE</c> cycle against local SQL Server (LocalDB), never against any shared or
/// production database. Proves, end to end: (1) a backup file is genuinely created, (2) a separate,
/// independently-named database is restored from it, (3) HAMBOX's own <see cref="CommerceDbContext"/>
/// — not just raw SQL — can connect to and query the restored copy, (4) the data in it matches the
/// source exactly, and (5) the restored copy's <c>__EFMigrationsHistory</c> is intact (schema-consistent,
/// nothing further to migrate). The source database is asserted untouched throughout.
/// </summary>
public sealed class DatabaseBackupRestoreTests : IAsyncLifetime
{
    private readonly string _sourceDbName = $"HamboxBackupRestoreSource_{Guid.NewGuid():N}";
    private readonly string _restoredDbName = $"HamboxBackupRestoreRestored_{Guid.NewGuid():N}";
    private readonly string _backupFilePath = Path.Combine(Path.GetTempPath(), $"hambox-backup-restore-test-{Guid.NewGuid():N}.bak");

    private string MasterConnectionString => "Server=(localdb)\\MSSQLLocalDB;Database=master;Trusted_Connection=True;TrustServerCertificate=True;";
    private string SourceConnectionString => $"Server=(localdb)\\MSSQLLocalDB;Database={_sourceDbName};Trusted_Connection=True;TrustServerCertificate=True;";
    private string RestoredConnectionString => $"Server=(localdb)\\MSSQLLocalDB;Database={_restoredDbName};Trusted_Connection=True;TrustServerCertificate=True;";

    public async Task InitializeAsync()
    {
        await using var db = CreateContext(SourceConnectionString);
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await using var master = new SqlConnection(MasterConnectionString);
        await master.OpenAsync();

        foreach (var name in new[] { _sourceDbName, _restoredDbName })
        {
            await using var cmd = master.CreateCommand();
            cmd.CommandText =
                $"IF DB_ID('{name}') IS NOT NULL BEGIN ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]; END";
            await cmd.ExecuteNonQueryAsync();
        }

        if (File.Exists(_backupFilePath))
        {
            File.Delete(_backupFilePath);
        }
    }

    private static CommerceDbContext CreateContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<CommerceDbContext>()
            .UseSqlServer(connectionString, o => o.MigrationsHistoryTable("__EFMigrationsHistory", "commerce"))
            .Options;
        return new CommerceDbContext(options, new PassthroughCodeProtector());
    }

    [Fact]
    public async Task BackupThenRestore_ProducesAnIndependentDatabase_QueryableThroughRealHamboxDbContext_WithMatchingData()
    {
        // Arrange: seed one identifiable row through the real application DbContext — this is the
        // "important data" requirement #4 checks for after restore, not a raw-SQL-only artifact.
        const string marker = "BACKUP_RESTORE_PROOF_MARKER";
        Guid seededJobId;
        await using (var sourceDb = CreateContext(SourceConnectionString))
        {
            var job = OperationalJob.Create(marker, relatedEntityType: "Test", relatedEntityId: "backup-restore-proof");
            sourceDb.OperationalJobs.Add(job);
            await sourceDb.SaveChangesAsync();
            seededJobId = job.Id;
        }

        // Act 1: a real BACKUP DATABASE — requirement #1 ("a backup can be created").
        await using (var master = new SqlConnection(MasterConnectionString))
        {
            await master.OpenAsync();
            await using var backupCmd = master.CreateCommand();
            backupCmd.CommandTimeout = 120;
            backupCmd.CommandText = $"BACKUP DATABASE [{_sourceDbName}] TO DISK = @path WITH INIT";
            backupCmd.Parameters.AddWithValue("@path", _backupFilePath);
            await backupCmd.ExecuteNonQueryAsync();
        }

        Assert.True(File.Exists(_backupFilePath), "Backup file was not created on disk.");
        Assert.True(new FileInfo(_backupFilePath).Length > 0, "Backup file is empty.");

        // Act 2: RESTORE into a genuinely separate, differently-named database with its own physical
        // files — requirement #2 ("a separate database can be restored from it"). Never overwrites or
        // touches the source database.
        var dataDirectory = Path.GetDirectoryName(await GetSourceDataFilePathAsync()) ?? throw new InvalidOperationException("Could not resolve LocalDB data directory.");
        await using (var master = new SqlConnection(MasterConnectionString))
        {
            await master.OpenAsync();
            await using var restoreCmd = master.CreateCommand();
            restoreCmd.CommandTimeout = 120;
            restoreCmd.CommandText = $"""
                RESTORE DATABASE [{_restoredDbName}] FROM DISK = @path WITH
                    MOVE '{_sourceDbName}' TO @dataFile,
                    MOVE '{_sourceDbName}_log' TO @logFile
                """;
            restoreCmd.Parameters.AddWithValue("@path", _backupFilePath);
            restoreCmd.Parameters.AddWithValue("@dataFile", Path.Combine(dataDirectory, $"{_restoredDbName}.mdf"));
            restoreCmd.Parameters.AddWithValue("@logFile", Path.Combine(dataDirectory, $"{_restoredDbName}_log.ldf"));
            await restoreCmd.ExecuteNonQueryAsync();
        }

        // Assert: the SOURCE database is untouched throughout — requirement #6 ("no production data is
        // modified or destroyed"), verified here as "the database this test seeded is unaffected".
        await using (var sourceDb = CreateContext(SourceConnectionString))
        {
            Assert.True(await sourceDb.Database.CanConnectAsync());
            Assert.True(await sourceDb.OperationalJobs.AnyAsync(j => j.Id == seededJobId));
        }

        // Assert: HAMBOX's own CommerceDbContext — real application code, not raw SQL — can connect to
        // and query the RESTORED copy, and the data matches exactly. Requirements #3 and #4.
        await using var restoredDb = CreateContext(RestoredConnectionString);
        Assert.True(await restoredDb.Database.CanConnectAsync());

        var restoredJob = await restoredDb.OperationalJobs.AsNoTracking().SingleOrDefaultAsync(j => j.Id == seededJobId);
        Assert.NotNull(restoredJob);
        Assert.Equal(marker, restoredJob!.JobType);
        Assert.Equal("backup-restore-proof", restoredJob.RelatedEntityId);

        // Assert: requirement #5 ("application startup/migrations behave correctly") — the restored
        // copy's migration history is intact and identical to the source, so the application would
        // start against it with no pending/missing migrations, exactly as it would against the source.
        var sourceMigrations = await GetAppliedMigrationsAsync(SourceConnectionString);
        var restoredMigrations = await GetAppliedMigrationsAsync(RestoredConnectionString);
        Assert.NotEmpty(restoredMigrations);
        Assert.Equal(sourceMigrations, restoredMigrations);
    }

    private async Task<string> GetSourceDataFilePathAsync()
    {
        await using var conn = new SqlConnection(SourceConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT physical_name FROM sys.database_files WHERE type_desc = 'ROWS'";
        return (string)(await cmd.ExecuteScalarAsync())!;
    }

    private static async Task<List<string>> GetAppliedMigrationsAsync(string connectionString)
    {
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT MigrationId FROM commerce.__EFMigrationsHistory ORDER BY MigrationId";
        var results = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(reader.GetString(0));
        }

        return results;
    }

    private sealed class PassthroughCodeProtector : ICodeProtector
    {
        public string Protect(string plainText) => plainText;

        public string Unprotect(string cipherText) => cipherText;
    }
}
