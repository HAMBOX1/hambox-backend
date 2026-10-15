using HAMBOX.Application.Communication;
using HAMBOX.SharedKernel.Errors;
using HAMBOX.SharedKernel.Results;
using Microsoft.Extensions.Logging;

namespace HAMBOX.Modules.Communication.Infrastructure.Services;

/// <summary>
/// Wraps the communication service so a notification problem — template, preference lookup, a database hiccup while
/// recording the message — can never surface in the code that asked for it (audit R47). The order, payment or
/// delivery that triggered the message has already happened and must not be rolled back, retried or reported as
/// failed because an e-mail could not be queued. The failure is logged and returned as a failed <see cref="Result"/>.
/// </summary>
internal sealed class FailSoftCommunicationService(
    ICommunicationService inner,
    ILogger<FailSoftCommunicationService> logger) : ICommunicationService
{
    public async Task<Result> SendAsync(CommunicationRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            return await inner.SendAsync(request, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(
                ex,
                "Notification '{TemplateKey}' for user {UserId} could not be queued; the triggering operation is unaffected.",
                request.TemplateKey, request.UserId);

            return Result.Failure(new Error("Communication.SendFailed", "The notification could not be queued."));
        }
    }
}
