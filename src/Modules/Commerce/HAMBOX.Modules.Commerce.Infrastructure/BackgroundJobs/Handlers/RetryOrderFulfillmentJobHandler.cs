using HAMBOX.Application.BackgroundJobs;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Referrals;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.Modules.Commerce.Domain.Operations;

namespace HAMBOX.Modules.Commerce.Infrastructure.BackgroundJobs.Handlers;

internal sealed class RetryOrderFulfillmentJobHandler(
    IBackgroundJobSerializer serializer,
    ICommerceDbContext db,
    OrderFulfillmentService fulfillment,
    ReferralLifecycleService referralLifecycle) : OrderRetryJobHandlerBase(serializer, db, fulfillment, referralLifecycle)
{
    public override string JobType => OperationalJobTypes.RetryOrderFulfillment;
}
