using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.SharedKernel.Errors;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Identity.Application.Features.Customers;

public sealed record Customer360AccountDto(
    Guid Id,
    string Email,
    bool EmailConfirmed,
    string FirstName,
    string LastName,
    string? PhoneNumber,
    string Status,
    string PreferredLanguage,
    string PreferredCurrency,
    DateTimeOffset RegisteredOnUtc);

/// <summary>Everything the admin needs about one customer in one place: the account plus the store's side of the story.</summary>
public sealed record Customer360Dto(Customer360AccountDto Account, CustomerActivity? Activity, CustomerStoreProfile Store);

public sealed record GetCustomer360Query(Guid Id) : IRequest<Result<Customer360Dto>>;

internal sealed class GetCustomer360QueryHandler(IIdentityDbContext db, ICustomerActivityReader activity)
    : IRequestHandler<GetCustomer360Query, Result<Customer360Dto>>
{
    public static readonly Error NotFound = new("Customers.NotFound", "The customer was not found.");

    public async Task<Result<Customer360Dto>> Handle(GetCustomer360Query request, CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking()
            .Where(u => u.Id == request.Id)
            .Select(u => new Customer360AccountDto(
                u.Id, u.Email, u.EmailConfirmed, u.FirstName, u.LastName, u.PhoneNumber, u.Status.ToString(),
                u.PreferredLanguage, u.PreferredCurrency, u.CreatedOnUtc))
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null)
        {
            return Result.Failure<Customer360Dto>(NotFound);
        }

        var id = user.Id.ToString();
        var summary = (await activity.GetActivityAsync([id], cancellationToken)).GetValueOrDefault(id);
        return Result.Success(new Customer360Dto(user, summary, await activity.GetProfileAsync(id, cancellationToken)));
    }
}
