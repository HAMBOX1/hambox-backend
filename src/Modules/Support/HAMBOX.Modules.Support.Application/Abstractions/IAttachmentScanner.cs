using HAMBOX.Modules.Support.Domain.Tickets;

namespace HAMBOX.Modules.Support.Application.Abstractions;

/// <summary>
/// Extension point for virus/malware scanning of uploaded attachments. No AV vendor is
/// integrated anywhere in the platform today (ponytail: this is a no-op hook, wire a real
/// scanner — e.g. ClamAV or a cloud AV API — behind this interface if/when compliance requires
/// it; every attachment is currently trusted after upload).
/// </summary>
public interface IAttachmentScanner
{
    Task<AttachmentScanStatus> ScanAsync(Stream content, string fileName, CancellationToken cancellationToken = default);
}
