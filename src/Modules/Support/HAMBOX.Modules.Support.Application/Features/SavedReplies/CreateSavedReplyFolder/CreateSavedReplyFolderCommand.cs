using HAMBOX.Modules.Support.Application.Contracts;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Support.Application.Features.SavedReplies.CreateSavedReplyFolder;

public sealed record CreateSavedReplyFolderCommand(string Name, int SortOrder) : IRequest<Result<SavedReplyFolderDto>>;
