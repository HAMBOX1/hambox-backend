using HAMBOX.Modules.Messaging.Application.Contracts;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Messaging.Application.Features.BotConfiguration.GetWhatsAppBotConfiguration;

public sealed record GetWhatsAppBotConfigurationQuery : IRequest<Result<WhatsAppBotConfigurationDto>>;
