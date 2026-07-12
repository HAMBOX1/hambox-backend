using HAMBOX.Modules.Commerce.Application.Contracts.Operations;

namespace HAMBOX.Modules.Commerce.Application.Abstractions;

public interface IOperationsMonitorService
{
    Task<OperationsDashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default);
}
