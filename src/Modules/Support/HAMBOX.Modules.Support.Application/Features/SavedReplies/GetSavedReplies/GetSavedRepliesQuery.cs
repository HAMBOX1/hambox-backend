using HAMBOX.Modules.Support.Application.Contracts;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Support.Application.Features.SavedReplies.GetSavedReplies;

public sealed record GetSavedRepliesQuery(Guid? FolderId, string? Search) : IRequest<Result<IReadOnlyList<SavedReplyDto>>>;
