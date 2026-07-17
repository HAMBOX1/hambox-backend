using HAMBOX.Application.Idempotency;
using HAMBOX.Infrastructure.Options;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Domain.Idempotency;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HAMBOX.Modules.Commerce.Infrastructure.Services;

/// <summary>
/// EF Core-backed implementation of <see cref="IIdempotencyService"/>. Thread-safety across
/// concurrent requests (and across API instances) is enforced at the database level: a unique
/// index on <see cref="IdempotencyKey.Key"/> arbitrates who wins the initial insert, and a
/// rowversion concurrency token arbitrates who wins reclaiming a failed/stuck record.
/// </summary>
public sealed class IdempotencyService(
    ICommerceDbContext dbContext,
    IOptions<IdempotencyOptions> options,
    ILogger<IdempotencyService> logger) : IIdempotencyService
{
    // SQL Server "Violation of UNIQUE KEY constraint" / "Cannot insert duplicate key" error numbers.
    private const int UniqueConstraintViolation = 2627;
    private const int UniqueIndexViolation = 2601;

    public async Task<IdempotencyOutcome> BeginAsync(
        string key,
        string? userId,
        string endpoint,
        string requestHash,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.IdempotencyKeys.FirstOrDefaultAsync(k => k.Key == key, cancellationToken);

        if (existing is null)
        {
            var record = IdempotencyKey.Start(key, userId, endpoint, requestHash, ExpiresAt());
            dbContext.IdempotencyKeys.Add(record);

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                return IdempotencyOutcome.Started();
            }
            catch (DbUpdateException ex) when (IsUniqueKeyViolation(ex))
            {
                logger.LogInformation(
                    "Lost the race to create idempotency key {IdempotencyKey}; re-reading the winning record.",
                    key);

                existing = await dbContext.IdempotencyKeys.FirstOrDefaultAsync(k => k.Key == key, cancellationToken)
                    ?? throw new InvalidOperationException(
                        $"Idempotency key '{key}' insert conflicted but no existing record could be found.");
            }
        }

        return await EvaluateExistingAsync(existing, endpoint, requestHash, cancellationToken);
    }

    public async Task CompleteAsync(
        string key,
        int responseStatusCode,
        string? serializedResponse,
        CancellationToken cancellationToken)
    {
        var record = await dbContext.IdempotencyKeys.FirstOrDefaultAsync(k => k.Key == key, cancellationToken);
        if (record is null)
        {
            logger.LogWarning("Idempotency key {IdempotencyKey} not found when recording completion.", key);
            return;
        }

        record.Complete(responseStatusCode, serializedResponse);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Idempotency key {IdempotencyKey} completed with status {StatusCode}.", key, responseStatusCode);
    }

    public async Task FailAsync(string key, CancellationToken cancellationToken)
    {
        var record = await dbContext.IdempotencyKeys.FirstOrDefaultAsync(k => k.Key == key, cancellationToken);
        if (record is null)
        {
            return;
        }

        record.Fail();
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogWarning("Idempotency key {IdempotencyKey} marked as failed and eligible for retry.", key);
    }

    private async Task<IdempotencyOutcome> EvaluateExistingAsync(
        IdempotencyKey existing,
        string endpoint,
        string requestHash,
        CancellationToken cancellationToken)
    {
        if (existing.Endpoint != endpoint || existing.RequestHash != requestHash)
        {
            return IdempotencyOutcome.PayloadMismatch();
        }

        if (existing.State == IdempotencyRecordState.Completed)
        {
            return IdempotencyOutcome.Replayed(existing.ResponseStatusCode ?? 200, existing.SerializedResponse);
        }

        var processingTimeout = TimeSpan.FromMinutes(options.Value.ProcessingTimeoutMinutes);
        var canReclaim = existing.State == IdempotencyRecordState.Failed || existing.IsStaleProcessing(processingTimeout);

        if (!canReclaim)
        {
            return IdempotencyOutcome.Conflict();
        }

        existing.Reclaim(requestHash, ExpiresAt());

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return IdempotencyOutcome.Started();
        }
        catch (DbUpdateConcurrencyException)
        {
            logger.LogInformation("Lost the race to reclaim idempotency key {IdempotencyKey}.", existing.Key);
            return IdempotencyOutcome.Conflict();
        }
    }

    private DateTimeOffset ExpiresAt() => DateTimeOffset.UtcNow.AddHours(options.Value.ExpirationHours);

    private static bool IsUniqueKeyViolation(DbUpdateException ex) =>
        ex.InnerException is SqlException { Number: UniqueConstraintViolation or UniqueIndexViolation };
}
