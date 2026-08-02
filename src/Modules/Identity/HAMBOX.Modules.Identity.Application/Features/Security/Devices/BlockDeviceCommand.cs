using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Identity.Application.Features.Security.Devices;

public sealed record BlockDeviceCommand(Guid DeviceId, string? Reason, string? IpAddress) : IRequest<Result>;
