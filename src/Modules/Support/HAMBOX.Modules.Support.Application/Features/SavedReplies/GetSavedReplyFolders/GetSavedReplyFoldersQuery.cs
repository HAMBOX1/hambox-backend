using HAMBOX.Modules.Support.Application.Contracts;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Support.Application.Features.SavedReplies.GetSavedReplyFolders;

public sealed record GetSavedReplyFoldersQuery : IRequest<Result<IReadOnlyList<SavedReplyFolderDto>>>;
