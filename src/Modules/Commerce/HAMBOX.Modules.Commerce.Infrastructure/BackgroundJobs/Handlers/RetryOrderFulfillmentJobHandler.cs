using HAMBOX.Application.BackgroundJobs;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.Modules.Commerce.Domain.Operations;

namespace HAMBOX.Modules.Commerce.Infrastructure.BackgroundJobs.Handlers;

internal sealed class RetryOrderFulfillmentJobHandler(
    IBackgroundJobSerializer serializer,
    ICommerceDbContext db,
    OrderFulfillmentService fulfillment) : OrderRetryJobHandlerBase(serializer, db, fulfillment)
{
    public override string JobType => OperationalJobTypes.RetryOrderFulfillment;
}
