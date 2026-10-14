namespace HAMBOX.Modules.Suppliers.Domain.Fulfillments;

/// <summary>
/// One outbound call to a supplier's API — what was sent, what came back, how long it took — so a dispute with a
/// supplier ("you never delivered", "we never ordered that") can be reconstructed from our own records (audit R40).
/// Bodies are stored redacted and truncated, and the body of a successful response is never stored: it can carry the
/// delivered codes, which must stay in the encrypted inventory only.
/// </summary>
public sealed class SupplierApiCallLog
{
    public const int MaxBodyLength = 4000;

    private SupplierApiCallLog()
    {
    }

    public SupplierApiCallLog(
        Guid id,
        string providerKey,
        string method,
        string url,
        string? requestBody,
        int? statusCode,
        string? responseBody,
        string? responseSummary,
        bool succeeded,
        string? errorMessage,
        int durationMs,
        Guid? fulfillmentId,
        Guid? hamboxReferenceId,
        Guid? supplierId,
        Guid? orderId,
        int? attempt)
    {
        Id = id;
        ProviderKey = providerKey;
        Method = method;
        Url = url;
        RequestBody = Truncate(requestBody);
        StatusCode = statusCode;
        ResponseBody = Truncate(responseBody);
        ResponseSummary = Truncate(responseSummary);
        Succeeded = succeeded;
        ErrorMessage = errorMessage is { Length: > 500 } ? errorMessage[..500] : errorMessage;
        DurationMs = durationMs;
        FulfillmentId = fulfillmentId;
        HamboxReferenceId = hamboxReferenceId;
        SupplierId = supplierId;
        OrderId = orderId;
        Attempt = attempt;
        CreatedOnUtc = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public string ProviderKey { get; private set; } = string.Empty;
    public string Method { get; private set; } = string.Empty;
    public string Url { get; private set; } = string.Empty;
    public string? RequestBody { get; private set; }
    public int? StatusCode { get; private set; }
    public string? ResponseBody { get; private set; }

    /// <summary>The identifying fields of a successful response (ids, status, reference) — never anything that could be a code.</summary>
    public string? ResponseSummary { get; private set; }

    public bool Succeeded { get; private set; }
    public string? ErrorMessage { get; private set; }
    public int DurationMs { get; private set; }
    public Guid? FulfillmentId { get; private set; }

    /// <summary>The reference HAMBOX sent as the supplier's request / idempotency id.</summary>
    public Guid? HamboxReferenceId { get; private set; }

    public Guid? SupplierId { get; private set; }
    public Guid? OrderId { get; private set; }
    public int? Attempt { get; private set; }
    public DateTimeOffset CreatedOnUtc { get; private set; }

    private static string? Truncate(string? value) =>
        value is { Length: > MaxBodyLength } ? value[..MaxBodyLength] : value;
}
