using HAMBOX.Domain.Entities;

namespace HAMBOX.Modules.Support.Domain.Tickets;

public enum AttachmentScanStatus
{
    NotScanned = 0,
    Clean = 1,
    Infected = 2,
}

public sealed class TicketAttachment : Entity, IAuditable
{
    private TicketAttachment()
    {
    }

    private TicketAttachment(
        Guid id, Guid ticketId, Guid? messageId, string storageKey, string publicUrl,
        string fileName, string contentType, long fileSizeBytes, string uploadedByUserId)
        : base(id)
    {
        TicketId = ticketId;
        MessageId = messageId;
        StorageKey = storageKey;
        PublicUrl = publicUrl;
        FileName = fileName;
        ContentType = contentType;
        FileSizeBytes = fileSizeBytes;
        UploadedByUserId = uploadedByUserId;
        ScanStatus = AttachmentScanStatus.NotScanned;
    }

    public Guid TicketId { get; private set; }
    public Guid? MessageId { get; private set; }
    public string StorageKey { get; private set; } = string.Empty;
    public string PublicUrl { get; private set; } = string.Empty;
    public string FileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long FileSizeBytes { get; private set; }
    public string UploadedByUserId { get; private set; } = string.Empty;
    public AttachmentScanStatus ScanStatus { get; private set; }

    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }

    public static TicketAttachment Create(
        Guid ticketId, Guid? messageId, string storageKey, string publicUrl,
        string fileName, string contentType, long fileSizeBytes, string uploadedByUserId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(uploadedByUserId);

        return new TicketAttachment(
            Guid.NewGuid(), ticketId, messageId, storageKey, publicUrl, fileName, contentType, fileSizeBytes, uploadedByUserId);
    }

    public void AttachToMessage(Guid messageId) => MessageId = messageId;

    public void SetScanStatus(AttachmentScanStatus status) => ScanStatus = status;
}
