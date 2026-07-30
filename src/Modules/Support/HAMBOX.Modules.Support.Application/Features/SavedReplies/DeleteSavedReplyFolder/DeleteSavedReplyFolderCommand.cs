using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Support.Application.Features.SavedReplies.DeleteSavedReplyFolder;

public sealed record DeleteSavedReplyFolderCommand(Guid FolderId) : IRequest<Result>;
