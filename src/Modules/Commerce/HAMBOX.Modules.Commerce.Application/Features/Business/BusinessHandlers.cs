using FluentValidation;
using HAMBOX.Application.Abstractions;
using HAMBOX.Application.PlatformSettings;
using HAMBOX.Modules.Catalog.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Application.Features.Checkout;
using HAMBOX.Modules.Commerce.Domain.Business;
using HAMBOX.Modules.Commerce.Domain.Operations;
using HAMBOX.Modules.Commerce.Domain.Orders;
using HAMBOX.SharedKernel.Errors;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Business;

// ELITE Business (B2B): accounts needing approval, wholesale prices, quotes, and manual orders recorded by sales.
// Collapsed into one file like the other loyalty handler sets.

public static class BusinessErrors
{
    public static readonly Error Disabled = new("Business.Disabled", "ELITE Business is not available right now.");
    public static readonly Error NotApproved = new("Business.NotApproved", "Your business account has not been approved.");
    public static readonly Error AccountNotFound = new("Business.AccountNotFound", "The business account was not found.");
    public static readonly Error PriceNotFound = new("Business.PriceNotFound", "The price was not found.");
    public static readonly Error QuoteNotFound = new("Business.QuoteNotFound", "The quote was not found.");
    public static readonly Error QuoteNotOpen = new("Business.QuoteNotOpen", "This quote is no longer open to answer.");
    public static readonly Error QuoteNotAccepted = new("Business.QuoteNotAccepted", "Only an accepted quote can become an order.");
    public static readonly Error NoPrice = new("Business.NoPrice", "No wholesale price applies to one of the lines. Set a unit price or add a price-list entry.");
    public static readonly Error ProductNotFound = new("Business.ProductNotFound", "A product or variant on the quote was not found.");
    public static readonly Error OrderNotFound = new("Business.OrderNotFound", "The order was not found.");
    public static readonly Error NotB2BOrder = new("Business.NotB2BOrder", "This is not a business order.");
}

public sealed record BusinessAccountDto(
    Guid Id, string UserId, string CompanyName, string? TaxId, string Country, string? Website, string? ContactPhone, string? Notes,
    string Status, string? SalesRepUserId, bool StarsEligible, string? DecisionReason, DateTimeOffset? ReviewedOnUtc, DateTimeOffset CreatedOnUtc);

public sealed record BusinessPriceDto(
    Guid Id, Guid? BusinessAccountId, Guid ProductId, string ProductName, Guid? ProductVariantId, int MinQuantity, decimal UnitPriceUsd,
    DateTimeOffset? ValidFromUtc, DateTimeOffset? ValidToUtc, bool IsActive);

public sealed record BusinessQuoteLineDto(Guid ProductId, Guid ProductVariantId, string ProductName, string Sku, int Quantity, decimal UnitPriceUsd, decimal LineTotal);

public sealed record BusinessQuoteDto(
    Guid Id, string QuoteNumber, Guid BusinessAccountId, string? CompanyName, string Status, DateTimeOffset ValidUntilUtc, string? Terms,
    decimal Total, Guid? OrderId, IReadOnlyList<BusinessQuoteLineDto> Lines);

public sealed record MyBusinessDto(
    bool Enabled, BusinessAccountDto? Account, IReadOnlyList<BusinessPriceDto> Prices, IReadOnlyList<BusinessQuoteDto> Quotes);

public sealed record BusinessApplicationInput(string CompanyName, string? TaxId, string Country, string? Website, string? ContactPhone, string? Notes);

public sealed record QuoteLineInput(Guid ProductId, Guid ProductVariantId, int Quantity, decimal? UnitPriceUsd);

public sealed record BusinessPriceInput(
    Guid? BusinessAccountId, Guid ProductId, Guid? ProductVariantId, int MinQuantity, decimal UnitPriceUsd,
    DateTimeOffset? ValidFromUtc, DateTimeOffset? ValidToUtc, bool IsActive);

public sealed record BusinessOrderResult(Guid OrderId, string OrderNumber, string Status, string PaymentStatus);

internal static class BusinessMapper
{
    public static BusinessAccountDto ToDto(BusinessAccount a) => new(
        a.Id, a.UserId, a.CompanyName, a.TaxId, a.Country, a.Website, a.ContactPhone, a.Notes, a.Status.ToString(), a.SalesRepUserId,
        a.StarsEligible, a.DecisionReason, a.ReviewedOnUtc, a.CreatedOnUtc);

    public static BusinessQuoteDto ToDto(BusinessQuote q, string? company, DateTimeOffset now) => new(
        q.Id, q.QuoteNumber, q.BusinessAccountId, company, q.EffectiveStatus(now).ToString(), q.ValidUntilUtc, q.Terms, q.Total, q.OrderId,
        q.Lines.Select(l => new BusinessQuoteLineDto(l.ProductId, l.ProductVariantId, l.ProductName, l.Sku, l.Quantity, l.UnitPriceUsd, l.LineTotal)).ToList());

    public static async Task<bool> IsEnabledAsync(IPlatformSettingsProvider settings, CancellationToken cancellationToken) =>
        (await settings.GetAsync<FeatureCenterSettingsPayload>(PlatformSettingsCategoryKeys.Features, cancellationToken)).EliteBusiness;
}

// ---- Customer ----------------------------------------------------------------------------------------------------

public sealed record GetMyBusinessQuery : IRequest<Result<MyBusinessDto>>;

internal sealed class GetMyBusinessQueryHandler(
    ICommerceDbContext db, ICatalogDbContext catalog, ICurrentUserService currentUser, IPlatformSettingsProvider settings)
    : IRequestHandler<GetMyBusinessQuery, Result<MyBusinessDto>>
{
    public async Task<Result<MyBusinessDto>> Handle(GetMyBusinessQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } userId)
        {
            return Result.Failure<MyBusinessDto>(CommerceErrors.AuthenticationRequired);
        }

        if (!await BusinessMapper.IsEnabledAsync(settings, cancellationToken))
        {
            return Result.Success(new MyBusinessDto(false, null, [], []));
        }

        var account = await db.BusinessAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.UserId == userId, cancellationToken);
        if (account is null || !account.IsApproved)
        {
            // Wholesale prices and quotes stay invisible until the account is approved.
            return Result.Success(new MyBusinessDto(true, account is null ? null : BusinessMapper.ToDto(account), [], []));
        }

        var now = DateTimeOffset.UtcNow;
        var prices = (await db.BusinessPrices.AsNoTracking()
                .Where(p => p.IsActive && (p.BusinessAccountId == null || p.BusinessAccountId == account.Id)).ToListAsync(cancellationToken))
            .Where(p => p.AppliesAt(now)).ToList();
        var names = await catalog.Products.AsNoTracking()
            .Where(p => prices.Select(x => x.ProductId).Distinct().Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.NameEn, cancellationToken);
        var quotes = await db.BusinessQuotes.AsNoTracking().Include(q => q.Lines)
            .Where(q => q.BusinessAccountId == account.Id && q.Status != BusinessQuoteStatus.Draft)
            .OrderByDescending(q => q.CreatedOnUtc).Take(50).ToListAsync(cancellationToken);

        return Result.Success(new MyBusinessDto(
            true, BusinessMapper.ToDto(account),
            prices.Select(p => new BusinessPriceDto(p.Id, p.BusinessAccountId, p.ProductId, names.GetValueOrDefault(p.ProductId, "?"), p.ProductVariantId, p.MinQuantity, p.UnitPriceUsd, p.ValidFromUtc, p.ValidToUtc, p.IsActive)).ToList(),
            quotes.Select(q => BusinessMapper.ToDto(q, account.CompanyName, now)).ToList()));
    }
}

public sealed record ApplyBusinessCommand(BusinessApplicationInput Input) : IRequest<Result<BusinessAccountDto>>;

internal sealed class ApplyBusinessCommandValidator : AbstractValidator<ApplyBusinessCommand>
{
    public ApplyBusinessCommandValidator()
    {
        RuleFor(x => x.Input).NotNull();
        RuleFor(x => x.Input.CompanyName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Input.Country).NotEmpty().MaximumLength(10);
        RuleFor(x => x.Input.TaxId).MaximumLength(100);
        RuleFor(x => x.Input.Website).MaximumLength(300);
        RuleFor(x => x.Input.ContactPhone).MaximumLength(50);
        RuleFor(x => x.Input.Notes).MaximumLength(2000);
    }
}

internal sealed class ApplyBusinessCommandHandler(ICommerceDbContext db, ICurrentUserService currentUser, IPlatformSettingsProvider settings)
    : IRequestHandler<ApplyBusinessCommand, Result<BusinessAccountDto>>
{
    public async Task<Result<BusinessAccountDto>> Handle(ApplyBusinessCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } userId)
        {
            return Result.Failure<BusinessAccountDto>(CommerceErrors.AuthenticationRequired);
        }

        if (!await BusinessMapper.IsEnabledAsync(settings, cancellationToken))
        {
            return Result.Failure<BusinessAccountDto>(BusinessErrors.Disabled);
        }

        var i = request.Input;
        var account = await db.BusinessAccounts.FirstOrDefaultAsync(a => a.UserId == userId, cancellationToken);
        if (account is null)
        {
            account = BusinessAccount.Apply(userId, i.CompanyName, i.TaxId, i.Country, i.Website, i.ContactPhone, i.Notes);
            db.BusinessAccounts.Add(account);
        }
        else
        {
            account.UpdateApplication(i.CompanyName, i.TaxId, i.Country, i.Website, i.ContactPhone, i.Notes);
        }

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(BusinessMapper.ToDto(account));
    }
}

public sealed record RespondToQuoteCommand(Guid QuoteId, bool Accept) : IRequest<Result<BusinessQuoteDto>>;

internal sealed class RespondToQuoteCommandHandler(ICommerceDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<RespondToQuoteCommand, Result<BusinessQuoteDto>>
{
    public async Task<Result<BusinessQuoteDto>> Handle(RespondToQuoteCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } userId)
        {
            return Result.Failure<BusinessQuoteDto>(CommerceErrors.AuthenticationRequired);
        }

        var account = await db.BusinessAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.UserId == userId, cancellationToken);
        if (account is null || !account.IsApproved)
        {
            return Result.Failure<BusinessQuoteDto>(BusinessErrors.NotApproved);
        }

        var quote = await db.BusinessQuotes.Include(q => q.Lines)
            .FirstOrDefaultAsync(q => q.Id == request.QuoteId && q.BusinessAccountId == account.Id, cancellationToken);
        if (quote is null)
        {
            return Result.Failure<BusinessQuoteDto>(BusinessErrors.QuoteNotFound);
        }

        var now = DateTimeOffset.UtcNow;
        if (!(request.Accept ? quote.Accept(now) : quote.Decline(now)))
        {
            return Result.Failure<BusinessQuoteDto>(BusinessErrors.QuoteNotOpen);
        }

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(BusinessMapper.ToDto(quote, account.CompanyName, now));
    }
}

// ---- Admin: accounts ---------------------------------------------------------------------------------------------

public sealed record GetBusinessAccountsQuery(string? Status) : IRequest<Result<IReadOnlyList<BusinessAccountDto>>>;

internal sealed class GetBusinessAccountsQueryHandler(ICommerceDbContext db)
    : IRequestHandler<GetBusinessAccountsQuery, Result<IReadOnlyList<BusinessAccountDto>>>
{
    public async Task<Result<IReadOnlyList<BusinessAccountDto>>> Handle(GetBusinessAccountsQuery request, CancellationToken cancellationToken)
    {
        var query = db.BusinessAccounts.AsNoTracking().AsQueryable();
        if (Enum.TryParse<BusinessAccountStatus>(request.Status, true, out var status))
        {
            query = query.Where(a => a.Status == status);
        }

        var rows = await query.OrderBy(a => a.Status).ThenByDescending(a => a.CreatedOnUtc).Take(300).ToListAsync(cancellationToken);
        return Result.Success<IReadOnlyList<BusinessAccountDto>>(rows.Select(BusinessMapper.ToDto).ToList());
    }
}

public sealed record DecideBusinessAccountCommand(Guid Id, BusinessAccountStatus Status, string? Reason) : IRequest<Result<BusinessAccountDto>>;

internal sealed class DecideBusinessAccountCommandValidator : AbstractValidator<DecideBusinessAccountCommand>
{
    public DecideBusinessAccountCommandValidator()
    {
        RuleFor(x => x.Status).IsInEnum().NotEqual(BusinessAccountStatus.Pending);
        RuleFor(x => x.Reason).NotEmpty().When(x => x.Status is BusinessAccountStatus.Rejected or BusinessAccountStatus.Suspended).MaximumLength(500);
    }
}

internal sealed class DecideBusinessAccountCommandHandler(ICommerceDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<DecideBusinessAccountCommand, Result<BusinessAccountDto>>
{
    public async Task<Result<BusinessAccountDto>> Handle(DecideBusinessAccountCommand request, CancellationToken cancellationToken)
    {
        var account = await db.BusinessAccounts.FirstOrDefaultAsync(a => a.Id == request.Id, cancellationToken);
        if (account is null)
        {
            return Result.Failure<BusinessAccountDto>(BusinessErrors.AccountNotFound);
        }

        account.Decide(request.Status, currentUser.UserId ?? "system", request.Reason);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(BusinessMapper.ToDto(account));
    }
}

public sealed record ConfigureBusinessAccountCommand(Guid Id, string? SalesRepUserId, bool StarsEligible) : IRequest<Result<BusinessAccountDto>>;

internal sealed class ConfigureBusinessAccountCommandHandler(ICommerceDbContext db, IUserDirectory users)
    : IRequestHandler<ConfigureBusinessAccountCommand, Result<BusinessAccountDto>>
{
    public async Task<Result<BusinessAccountDto>> Handle(ConfigureBusinessAccountCommand request, CancellationToken cancellationToken)
    {
        var account = await db.BusinessAccounts.FirstOrDefaultAsync(a => a.Id == request.Id, cancellationToken);
        if (account is null)
        {
            return Result.Failure<BusinessAccountDto>(BusinessErrors.AccountNotFound);
        }

        if (!string.IsNullOrWhiteSpace(request.SalesRepUserId)
            && (await users.GetExistingUserIdsAsync([request.SalesRepUserId], cancellationToken)).Count == 0)
        {
            return Result.Failure<BusinessAccountDto>(BusinessErrors.AccountNotFound);
        }

        account.Configure(request.SalesRepUserId, request.StarsEligible);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(BusinessMapper.ToDto(account));
    }
}

// ---- Admin: prices -----------------------------------------------------------------------------------------------

public sealed record GetBusinessPricesQuery(Guid? AccountId) : IRequest<Result<IReadOnlyList<BusinessPriceDto>>>;

internal sealed class GetBusinessPricesQueryHandler(ICommerceDbContext db, ICatalogDbContext catalog)
    : IRequestHandler<GetBusinessPricesQuery, Result<IReadOnlyList<BusinessPriceDto>>>
{
    public async Task<Result<IReadOnlyList<BusinessPriceDto>>> Handle(GetBusinessPricesQuery request, CancellationToken cancellationToken)
    {
        var query = db.BusinessPrices.AsNoTracking().AsQueryable();
        if (request.AccountId is { } id)
        {
            query = query.Where(p => p.BusinessAccountId == id);
        }

        var rows = await query.OrderBy(p => p.ProductId).ThenBy(p => p.MinQuantity).Take(500).ToListAsync(cancellationToken);
        var ids = rows.Select(r => r.ProductId).Distinct().ToList();
        var names = await catalog.Products.AsNoTracking().Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.NameEn, cancellationToken);
        return Result.Success<IReadOnlyList<BusinessPriceDto>>(rows.Select(p => new BusinessPriceDto(
            p.Id, p.BusinessAccountId, p.ProductId, names.GetValueOrDefault(p.ProductId, "?"), p.ProductVariantId, p.MinQuantity,
            p.UnitPriceUsd, p.ValidFromUtc, p.ValidToUtc, p.IsActive)).ToList());
    }
}

public sealed record SaveBusinessPriceCommand(Guid? Id, BusinessPriceInput Input) : IRequest<Result<Guid>>;

internal sealed class SaveBusinessPriceCommandValidator : AbstractValidator<SaveBusinessPriceCommand>
{
    public SaveBusinessPriceCommandValidator()
    {
        RuleFor(x => x.Input).NotNull();
        RuleFor(x => x.Input.ProductId).NotEmpty();
        RuleFor(x => x.Input.MinQuantity).InclusiveBetween(1, 1_000_000);
        RuleFor(x => x.Input.UnitPriceUsd).InclusiveBetween(0.01m, 10_000_000m).PrecisionScale(18, 2, false);
        RuleFor(x => x.Input.ValidToUtc).GreaterThan(x => x.Input.ValidFromUtc).When(x => x.Input.ValidFromUtc.HasValue && x.Input.ValidToUtc.HasValue);
    }
}

internal sealed class SaveBusinessPriceCommandHandler(ICommerceDbContext db, ICatalogDbContext catalog)
    : IRequestHandler<SaveBusinessPriceCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(SaveBusinessPriceCommand request, CancellationToken cancellationToken)
    {
        var i = request.Input;
        if (!await catalog.Products.AnyAsync(p => p.Id == i.ProductId, cancellationToken)
            || (i.ProductVariantId is { } v && !await catalog.ProductVariants.AnyAsync(x => x.Id == v && x.ProductId == i.ProductId, cancellationToken)))
        {
            return Result.Failure<Guid>(BusinessErrors.ProductNotFound);
        }

        if (i.BusinessAccountId is { } accountId && !await db.BusinessAccounts.AnyAsync(a => a.Id == accountId, cancellationToken))
        {
            return Result.Failure<Guid>(BusinessErrors.AccountNotFound);
        }

        BusinessPrice price;
        if (request.Id is { } id)
        {
            var existing = await db.BusinessPrices.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
            if (existing is null)
            {
                return Result.Failure<Guid>(BusinessErrors.PriceNotFound);
            }

            price = existing;
            price.Update(i.ProductVariantId, i.MinQuantity, i.UnitPriceUsd, i.ValidFromUtc, i.ValidToUtc, i.IsActive);
        }
        else
        {
            price = BusinessPrice.Create(i.BusinessAccountId, i.ProductId, i.ProductVariantId, i.MinQuantity, i.UnitPriceUsd, i.ValidFromUtc, i.ValidToUtc);
            price.Update(i.ProductVariantId, i.MinQuantity, i.UnitPriceUsd, i.ValidFromUtc, i.ValidToUtc, i.IsActive);
            db.BusinessPrices.Add(price);
        }

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(price.Id);
    }
}

public sealed record DeleteBusinessPriceCommand(Guid Id) : IRequest<Result>;

internal sealed class DeleteBusinessPriceCommandHandler(ICommerceDbContext db) : IRequestHandler<DeleteBusinessPriceCommand, Result>
{
    public async Task<Result> Handle(DeleteBusinessPriceCommand request, CancellationToken cancellationToken)
    {
        var price = await db.BusinessPrices.FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);
        if (price is null)
        {
            return Result.Failure(BusinessErrors.PriceNotFound);
        }

        db.BusinessPrices.Remove(price);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

// ---- Admin: quotes and orders ------------------------------------------------------------------------------------

public sealed record GetBusinessQuotesQuery(string? Status) : IRequest<Result<IReadOnlyList<BusinessQuoteDto>>>;

internal sealed class GetBusinessQuotesQueryHandler(ICommerceDbContext db)
    : IRequestHandler<GetBusinessQuotesQuery, Result<IReadOnlyList<BusinessQuoteDto>>>
{
    public async Task<Result<IReadOnlyList<BusinessQuoteDto>>> Handle(GetBusinessQuotesQuery request, CancellationToken cancellationToken)
    {
        var query = db.BusinessQuotes.AsNoTracking().Include(q => q.Lines).AsQueryable();
        if (Enum.TryParse<BusinessQuoteStatus>(request.Status, true, out var status))
        {
            query = query.Where(q => q.Status == status);
        }

        var rows = await query.OrderByDescending(q => q.CreatedOnUtc).Take(200).ToListAsync(cancellationToken);
        var accountIds = rows.Select(r => r.BusinessAccountId).Distinct().ToList();
        var companies = await db.BusinessAccounts.AsNoTracking().Where(a => accountIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, a => a.CompanyName, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        return Result.Success<IReadOnlyList<BusinessQuoteDto>>(rows.Select(q => BusinessMapper.ToDto(q, companies.GetValueOrDefault(q.BusinessAccountId), now)).ToList());
    }
}

public sealed record CreateBusinessQuoteCommand(Guid AccountId, int ValidDays, string? Terms, IReadOnlyList<QuoteLineInput> Lines, bool SendNow)
    : IRequest<Result<BusinessQuoteDto>>;

internal sealed class CreateBusinessQuoteCommandValidator : AbstractValidator<CreateBusinessQuoteCommand>
{
    public CreateBusinessQuoteCommandValidator()
    {
        RuleFor(x => x.ValidDays).InclusiveBetween(1, 365);
        RuleFor(x => x.Terms).MaximumLength(2000);
        RuleFor(x => x.Lines).NotEmpty().Must(l => l.Count <= 100);
        RuleForEach(x => x.Lines).ChildRules(l =>
        {
            l.RuleFor(x => x.ProductId).NotEmpty();
            l.RuleFor(x => x.ProductVariantId).NotEmpty();
            l.RuleFor(x => x.Quantity).InclusiveBetween(1, 1_000_000);
            l.RuleFor(x => x.UnitPriceUsd).InclusiveBetween(0.01m, 10_000_000m).When(x => x.UnitPriceUsd.HasValue);
        });
    }
}

internal sealed class CreateBusinessQuoteCommandHandler(ICommerceDbContext db, ICatalogDbContext catalog, ICurrentUserService currentUser)
    : IRequestHandler<CreateBusinessQuoteCommand, Result<BusinessQuoteDto>>
{
    public async Task<Result<BusinessQuoteDto>> Handle(CreateBusinessQuoteCommand request, CancellationToken cancellationToken)
    {
        var account = await db.BusinessAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == request.AccountId, cancellationToken);
        if (account is null || !account.IsApproved)
        {
            return Result.Failure<BusinessQuoteDto>(account is null ? BusinessErrors.AccountNotFound : BusinessErrors.NotApproved);
        }

        var productIds = request.Lines.Select(l => l.ProductId).Distinct().ToList();
        var products = await catalog.Products.AsNoTracking().Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, cancellationToken);
        var variantIds = request.Lines.Select(l => l.ProductVariantId).Distinct().ToList();
        var variants = await catalog.ProductVariants.AsNoTracking().Where(v => variantIds.Contains(v.Id)).ToDictionaryAsync(v => v.Id, cancellationToken);
        var prices = await db.BusinessPrices.AsNoTracking().Where(p => p.IsActive && productIds.Contains(p.ProductId)).ToListAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;

        var lines = new List<BusinessQuoteLine>();
        foreach (var l in request.Lines)
        {
            if (!products.TryGetValue(l.ProductId, out var product) || !variants.TryGetValue(l.ProductVariantId, out var variant) || variant.ProductId != product.Id)
            {
                return Result.Failure<BusinessQuoteDto>(BusinessErrors.ProductNotFound);
            }

            var unit = l.UnitPriceUsd ?? BusinessPricing.Resolve(prices, account.Id, product.Id, variant.Id, l.Quantity, now)?.UnitPriceUsd;
            if (unit is null)
            {
                return Result.Failure<BusinessQuoteDto>(BusinessErrors.NoPrice);
            }

            lines.Add(BusinessQuoteLine.Create(product.Id, variant.Id, product.NameEn, variant.Sku, l.Quantity, unit.Value));
        }

        var number = $"Q-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
        var quote = BusinessQuote.Create(number, account.Id, now.AddDays(request.ValidDays), request.Terms, currentUser.UserId ?? "system", lines);
        if (request.SendNow)
        {
            quote.Send();
        }

        db.BusinessQuotes.Add(quote);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(BusinessMapper.ToDto(quote, account.CompanyName, now));
    }
}

public sealed record SendBusinessQuoteCommand(Guid QuoteId) : IRequest<Result>;

internal sealed class SendBusinessQuoteCommandHandler(ICommerceDbContext db) : IRequestHandler<SendBusinessQuoteCommand, Result>
{
    public async Task<Result> Handle(SendBusinessQuoteCommand request, CancellationToken cancellationToken)
    {
        var quote = await db.BusinessQuotes.FirstOrDefaultAsync(q => q.Id == request.QuoteId, cancellationToken);
        if (quote is null)
        {
            return Result.Failure(BusinessErrors.QuoteNotFound);
        }

        if (!quote.Send())
        {
            return Result.Failure(BusinessErrors.QuoteNotOpen);
        }

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

/// <summary>
/// Sales turns an accepted quote into an ordinary order (payment method "b2b") on the buyer's account. When payment is
/// already confirmed it is recorded and delivery is queued; delivery — not payment — completes the order, exactly like
/// every other order.
/// </summary>
public sealed record CreateOrderFromQuoteCommand(Guid QuoteId, bool PaymentConfirmed, string? PaymentReference)
    : IRequest<Result<BusinessOrderResult>>;

internal sealed class CreateOrderFromQuoteCommandHandler(
    ICommerceDbContext db, IUserDirectory users, IPlatformSettingsProvider settings, IOperationalJobQueue jobQueue)
    : IRequestHandler<CreateOrderFromQuoteCommand, Result<BusinessOrderResult>>
{
    public async Task<Result<BusinessOrderResult>> Handle(CreateOrderFromQuoteCommand request, CancellationToken cancellationToken)
    {
        var quote = await db.BusinessQuotes.Include(q => q.Lines).FirstOrDefaultAsync(q => q.Id == request.QuoteId, cancellationToken);
        if (quote is null)
        {
            return Result.Failure<BusinessOrderResult>(BusinessErrors.QuoteNotFound);
        }

        if (quote.Status != BusinessQuoteStatus.Accepted)
        {
            return Result.Failure<BusinessOrderResult>(BusinessErrors.QuoteNotAccepted);
        }

        var account = await db.BusinessAccounts.AsNoTracking().FirstAsync(a => a.Id == quote.BusinessAccountId, cancellationToken);
        var commerce = await settings.GetAsync<CommerceSettingsPayload>(PlatformSettingsCategoryKeys.Commerce, cancellationToken);
        var email = await users.GetEmailAsync(account.UserId, cancellationToken) ?? string.Empty;

        var total = quote.Total;
        var order = Order.Create(
            account.UserId, OrderNumberGenerator.Generate(commerce.InvoicePrefix), email, account.Country, "b2b", total, 0m, 0m, total,
            quote.Lines.Select(l => (l.ProductId, l.ProductName, l.Quantity, l.UnitPriceUsd, (Guid?)l.ProductVariantId, (string?)l.Sku,
                (Guid?)null, (Guid?)null, (decimal?)null, (decimal?)null)).ToList());

        if (request.PaymentConfirmed)
        {
            order.RecordPayment("B2B", string.IsNullOrWhiteSpace(request.PaymentReference) ? quote.QuoteNumber : request.PaymentReference.Trim());
            order.MarkProcessing();
        }

        db.Orders.Add(order);
        quote.MarkOrdered(order.Id);
        await db.SaveChangesAsync(cancellationToken);

        if (request.PaymentConfirmed)
        {
            await QueueFulfillmentAsync(jobQueue, order.Id, cancellationToken);
        }

        return Result.Success(new BusinessOrderResult(order.Id, order.OrderNumber, order.Status.ToString(), order.PaymentStatus.ToString()));
    }

    internal static Task QueueFulfillmentAsync(IOperationalJobQueue jobQueue, Guid orderId, CancellationToken cancellationToken) =>
        jobQueue.EnqueueAsync(
            OperationalJobTypes.ExecuteOrderFulfillment,
            priority: OperationalJobPriority.High,
            relatedEntityType: "Order",
            relatedEntityId: orderId.ToString(),
            cancellationToken: cancellationToken);
}

/// <summary>Sales records that the buyer's payment has arrived (bank transfer, cash…), which releases the order to delivery.</summary>
public sealed record ConfirmBusinessPaymentCommand(Guid OrderId, string Reference) : IRequest<Result<BusinessOrderResult>>;

internal sealed class ConfirmBusinessPaymentCommandValidator : AbstractValidator<ConfirmBusinessPaymentCommand>
{
    public ConfirmBusinessPaymentCommandValidator() => RuleFor(x => x.Reference).NotEmpty().MaximumLength(200);
}

internal sealed class ConfirmBusinessPaymentCommandHandler(ICommerceDbContext db, IOperationalJobQueue jobQueue)
    : IRequestHandler<ConfirmBusinessPaymentCommand, Result<BusinessOrderResult>>
{
    public async Task<Result<BusinessOrderResult>> Handle(ConfirmBusinessPaymentCommand request, CancellationToken cancellationToken)
    {
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken);
        if (order is null)
        {
            return Result.Failure<BusinessOrderResult>(BusinessErrors.OrderNotFound);
        }

        if (order.PaymentMethod != "b2b")
        {
            return Result.Failure<BusinessOrderResult>(BusinessErrors.NotB2BOrder);
        }

        if (order.PaymentStatus == HAMBOX.Modules.Commerce.Domain.Enums.PaymentStatus.Pending && order.Status == HAMBOX.Modules.Commerce.Domain.Enums.OrderStatus.Pending)
        {
            order.RecordPayment("B2B", request.Reference.Trim());
            order.MarkProcessing();
            await db.SaveChangesAsync(cancellationToken);
            await CreateOrderFromQuoteCommandHandler.QueueFulfillmentAsync(jobQueue, order.Id, cancellationToken);
        }

        return Result.Success(new BusinessOrderResult(order.Id, order.OrderNumber, order.Status.ToString(), order.PaymentStatus.ToString()));
    }
}
