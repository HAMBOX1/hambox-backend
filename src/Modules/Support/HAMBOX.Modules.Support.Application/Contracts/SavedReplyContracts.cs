namespace HAMBOX.Modules.Support.Application.Contracts;

public sealed record SavedReplyFolderDto(Guid Id, string Name, int SortOrder);

public sealed record SavedReplyDto(Guid Id, Guid? FolderId, string Title, string Body, int UsageCount);
