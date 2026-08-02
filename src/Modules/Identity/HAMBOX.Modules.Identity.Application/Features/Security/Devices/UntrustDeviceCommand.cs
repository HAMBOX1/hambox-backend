using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Identity.Application.Features.Security.Devices;

public sealed record UntrustDeviceCommand(Guid DeviceId) : IRequest<Result>;
