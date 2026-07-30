using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Support.Application.Features.SavedReplies.RenderSavedReply;

public sealed record RenderSavedReplyQuery(Guid ReplyId, Guid TicketId) : IRequest<Result<string>>;
