using HAMBOX.Modules.Commerce.Application.Contracts.Operations;

namespace HAMBOX.Modules.Commerce.Application.Abstractions;

public interface ISystemHealthService
{
    Task<SystemHealthDto> GetHealthAsync(CancellationToken cancellationToken = default);
}
