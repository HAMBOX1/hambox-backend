using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Support.Application.Features.SavedReplies.UpdateSavedReply;

public sealed record UpdateSavedReplyCommand(Guid ReplyId, Guid? FolderId, string Title, string Body) : IRequest<Result>;
