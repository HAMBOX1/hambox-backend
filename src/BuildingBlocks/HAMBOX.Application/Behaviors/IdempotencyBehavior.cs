using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentValidation;
using FluentValidation.Results;
using HAMBOX.Application.Abstractions;
using HAMBOX.Application.Idempotency;
using HAMBOX.SharedKernel.Errors;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.Extensions.Logging;

namespace HAMBOX.Application.Behaviors;

/// <summary>
/// Deduplicates commands that opt in via <see cref="IIdempotentRequest{TValue}"/>: the first
/// request with a given <c>Idempotency-Key</c> header executes the handler and caches its
/// <see cref="Result{TValue}"/>; every subsequent request with the same key replays that
/// cached result instead of re-executing the handler.
/// </summary>
/// <typeparam name="TRequest">The command type, which must implement <see cref="IIdempotentRequest{TValue}"/>.</typeparam>
/// <typeparam name="TValue">The value type wrapped by the command's <c>Result&lt;TValue&gt;</c> response.</typeparam>
public sealed class IdempotencyBehavior<TRequest, TValue>(
    IIdempotencyKeyAccessor keyAccessor,
    IIdempotencyService idempotencyService,
    ICurrentUserService currentUserService,
    ILogger<IdempotencyBehavior<TRequest, TValue>> logger)
    : IPipelineBehavior<TRequest, Result<TValue>>
    where TRequest : notnull, IIdempotentRequest<TValue>
{
    private const int SuccessStatusCode = 200;
    private const int FailureStatusCode = 400;

    public async Task<Result<TValue>> Handle(
        TRequest request,
        RequestHandlerDelegate<Result<TValue>> next,
        CancellationToken cancellationToken)
    {
        var key = keyAccessor.GetIdempotencyKey();
        var endpoint = typeof(TRequest).Name;

        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ValidationException(
                [new ValidationFailure("Idempotency-Key", "The Idempotency-Key header is required for this request.")]);
        }

        var requestHash = ComputeRequestHash(request);

        var outcome = await idempotencyService.BeginAsync(
            key,
            currentUserService.UserId,
            endpoint,
            requestHash,
            cancellationToken);

        switch (outcome.State)
        {
            case IdempotencyState.PayloadMismatch:
                logger.LogWarning(
                    "Idempotency payload mismatch for key {IdempotencyKey} on {Endpoint}",
                    key,
                    endpoint);

                throw new ValidationException(
                    [new ValidationFailure("Idempotency-Key", "This idempotency key was already used with a different request.")]);

            case IdempotencyState.Conflict:
                logger.LogWarning(
                    "Idempotency conflict for key {IdempotencyKey} on {Endpoint}: another request is still processing",
                    key,
                    endpoint);

                throw new IdempotencyConflictException(key);

            case IdempotencyState.Replayed:
                logger.LogInformation(
                    "Replaying cached response for idempotency key {IdempotencyKey} on {Endpoint}",
                    key,
                    endpoint);

                return DeserializeResponse(outcome.StoredResponse);

            case IdempotencyState.Started:
            default:
                logger.LogInformation(
                    "Idempotency key {IdempotencyKey} created for {Endpoint}",
                    key,
                    endpoint);
                break;
        }

        try
        {
            var response = await next(cancellationToken).ConfigureAwait(false);

            await idempotencyService.CompleteAsync(
                key,
                response.IsSuccess ? SuccessStatusCode : FailureStatusCode,
                SerializeResponse(response),
                cancellationToken);

            return response;
        }
        catch
        {
            await idempotencyService.FailAsync(key, cancellationToken);
            throw;
        }
    }

    private static string ComputeRequestHash(TRequest request)
    {
        var json = JsonSerializer.Serialize(request, request.GetType());
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexStringLower(bytes);
    }

    private static string SerializeResponse(Result<TValue> response) =>
        JsonSerializer.Serialize(new ResponseEnvelope(
            response.IsSuccess,
            response.IsSuccess ? null : response.Error.Code,
            response.IsSuccess ? null : response.Error.Description,
            response.IsSuccess ? response.Value : default));

    private static Result<TValue> DeserializeResponse(string? serializedResponse)
    {
        if (string.IsNullOrWhiteSpace(serializedResponse))
        {
            throw new InvalidOperationException("The cached idempotent response is missing or empty.");
        }

        var envelope = JsonSerializer.Deserialize<ResponseEnvelope>(serializedResponse)
            ?? throw new InvalidOperationException("The cached idempotent response could not be deserialized.");

        return envelope.IsSuccess
            ? Result<TValue>.Success(envelope.Value!)
            : Result<TValue>.Failure(new Error(envelope.ErrorCode!, envelope.ErrorDescription!));
    }

    private sealed record ResponseEnvelope(bool IsSuccess, string? ErrorCode, string? ErrorDescription, TValue? Value);
}
