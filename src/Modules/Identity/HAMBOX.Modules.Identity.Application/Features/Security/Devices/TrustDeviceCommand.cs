using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Identity.Application.Features.Security.Devices;

public sealed record TrustDeviceCommand(Guid DeviceId) : IRequest<Result>;
