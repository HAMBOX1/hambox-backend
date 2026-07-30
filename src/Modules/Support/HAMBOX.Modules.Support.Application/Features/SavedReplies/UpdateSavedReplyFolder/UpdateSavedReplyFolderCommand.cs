using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Support.Application.Features.SavedReplies.UpdateSavedReplyFolder;

public sealed record UpdateSavedReplyFolderCommand(Guid FolderId, string Name, int SortOrder) : IRequest<Result>;
