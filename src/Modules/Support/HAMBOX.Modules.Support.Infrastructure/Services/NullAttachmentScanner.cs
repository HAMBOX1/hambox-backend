using HAMBOX.Modules.Support.Application.Abstractions;
using HAMBOX.Modules.Support.Domain.Tickets;

namespace HAMBOX.Modules.Support.Infrastructure.Services;

/// <summary>No-op default for <see cref="IAttachmentScanner"/> — see the interface's doc
/// comment. Every attachment is marked <see cref="AttachmentScanStatus.Clean"/> without ever
/// being scanned; replace with a real AV integration behind the same interface if needed.</summary>
internal sealed class NullAttachmentScanner : IAttachmentScanner
{
    public Task<AttachmentScanStatus> ScanAsync(Stream content, string fileName, CancellationToken cancellationToken = default) =>
        Task.FromResult(AttachmentScanStatus.Clean);
}
