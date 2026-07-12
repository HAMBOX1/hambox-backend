namespace HAMBOX.Modules.Commerce.Domain.Operations;

public sealed class ApiRequestLog
{
    private ApiRequestLog()
    {
    }

    private ApiRequestLog(
        Guid id,
        DateTimeOffset timestampUtc,
        string method,
        string path,
        int statusCode,
        long durationMs,
        string? correlationId,
        string? userId,
        string? ipAddress,
        long? requestSizeBytes,
        long? responseSizeBytes,
        string? exception)
    {
        Id = id;
        TimestampUtc = timestampUtc;
        Method = method;
        Path = path;
        StatusCode = statusCode;
        DurationMs = durationMs;
        CorrelationId = correlationId;
        UserId = userId;
        IpAddress = ipAddress;
        RequestSizeBytes = requestSizeBytes;
        ResponseSizeBytes = responseSizeBytes;
        Exception = exception;
    }

    public Guid Id { get; private set; }
    public DateTimeOffset TimestampUtc { get; private set; }
    public string Method { get; private set; } = string.Empty;
    public string Path { get; private set; } = string.Empty;
    public int StatusCode { get; private set; }
    public long DurationMs { get; private set; }
    public string? CorrelationId { get; private set; }
    public string? UserId { get; private set; }
    public string? IpAddress { get; private set; }
    public long? RequestSizeBytes { get; private set; }
    public long? ResponseSizeBytes { get; private set; }
    public string? Exception { get; private set; }

    public static ApiRequestLog Create(
        string method,
        string path,
        int statusCode,
        long durationMs,
        string? correlationId,
        string? userId,
        string? ipAddress,
        long? requestSizeBytes,
        long? responseSizeBytes,
        string? exception = null) =>
        new(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            method,
            path.Length > 512 ? path[..512] : path,
            statusCode,
            durationMs,
            correlationId,
            userId,
            ipAddress,
            requestSizeBytes,
            responseSizeBytes,
            exception is { Length: > 4000 } ? exception[..4000] : exception);
}
