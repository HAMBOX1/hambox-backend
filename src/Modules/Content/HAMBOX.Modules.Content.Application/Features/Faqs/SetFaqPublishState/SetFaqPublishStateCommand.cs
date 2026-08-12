using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Content.Application.Features.Faqs.SetFaqPublishState;

public sealed record SetFaqPublishStateCommand(Guid Id, bool Publish) : IRequest<Result>;
