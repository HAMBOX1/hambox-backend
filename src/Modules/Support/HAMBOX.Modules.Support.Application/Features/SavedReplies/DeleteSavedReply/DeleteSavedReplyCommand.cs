using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Support.Application.Features.SavedReplies.DeleteSavedReply;

public sealed record DeleteSavedReplyCommand(Guid ReplyId) : IRequest<Result>;
