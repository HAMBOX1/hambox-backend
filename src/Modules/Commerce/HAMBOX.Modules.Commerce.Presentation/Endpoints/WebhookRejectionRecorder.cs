using HAMBOX.Modules.Commerce.Application.Features.Checkout.RecordPaymentWebhook;
using MediatR;

namespace HAMBOX.Modules.Commerce.Presentation.Endpoints;

internal static class WebhookRejectionRecorder
{
    /// <summary>
    /// Writes the audit row for a callback that was rejected before processing. Best-effort by design: the
    /// rejection itself (403) must be returned no matter what, so a failure to write the row — an unreachable
    /// database, an unmigrated schema — never turns a refusal into a server error.
    /// </summary>
    public static async Task TryRecordRejectedWebhookAsync(this ISender sender, RecordPaymentWebhookCommand command)
    {
        try
        {
            await sender.Send(command);
        }
        catch (Exception)
        {
            // Intentionally swallowed — see summary.
        }
    }
}
