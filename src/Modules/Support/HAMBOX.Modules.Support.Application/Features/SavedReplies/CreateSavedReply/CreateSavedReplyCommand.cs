using HAMBOX.Modules.Support.Application.Contracts;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Support.Application.Features.SavedReplies.CreateSavedReply;

public sealed record CreateSavedReplyCommand(Guid? FolderId, string Title, string Body) : IRequest<Result<SavedReplyDto>>;
