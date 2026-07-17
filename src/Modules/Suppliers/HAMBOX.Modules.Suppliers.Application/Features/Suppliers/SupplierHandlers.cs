using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Suppliers.Application.Abstractions;
using HAMBOX.Modules.Suppliers.Application.Contracts;
using HAMBOX.Modules.Suppliers.Application.Errors;
using HAMBOX.Modules.Suppliers.Application.Services;
using HAMBOX.Modules.Suppliers.Domain.Suppliers;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Suppliers.Application.Features.Suppliers;

// ─── Queries ───────────────────────────────────────────────────

public sealed record GetSuppliersQuery(string? Search, string? Status, int Page, int PageSize)
    : IRequest<Result<PagedResult<SupplierListItemDto>>>;

internal sealed class GetSuppliersQueryHandler(ISuppliersDbContext dbContext)
    : IRequestHandler<GetSuppliersQuery, Result<PagedResult<SupplierListItemDto>>>
{
    public async Task<Result<PagedResult<SupplierListItemDto>>> Handle(GetSuppliersQuery request, CancellationToken cancellationToken)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 100 ? 20 : request.PageSize;

        var query = dbContext.Suppliers.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(s => s.Name.Contains(search) || s.Code.Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(request.Status) && Enum.TryParse<SupplierStatus>(request.Status, true, out var status))
        {
            query = query.Where(s => s.Status == status);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(s => s.Priority)
            .ThenBy(s => s.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => SupplierMapper.ToListItem(s))
            .ToListAsync(cancellationToken);

        return Result.Success(new PagedResult<SupplierListItemDto>(items, page, pageSize, totalCount));
    }
}

public sealed record GetSupplierByIdQuery(Guid Id) : IRequest<Result<SupplierDetailDto>>;

internal sealed class GetSupplierByIdQueryHandler(ISuppliersDbContext dbContext)
    : IRequestHandler<GetSupplierByIdQuery, Result<SupplierDetailDto>>
{
    public async Task<Result<SupplierDetailDto>> Handle(GetSupplierByIdQuery request, CancellationToken cancellationToken)
    {
        var supplier = await dbContext.Suppliers.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);

        return supplier is null
            ? Result.Failure<SupplierDetailDto>(SupplierErrors.NotFound)
            : Result.Success(SupplierMapper.ToDetail(supplier));
    }
}

public sealed record GetSupplierProviderTypesQuery : IRequest<Result<IReadOnlyCollection<string>>>;

internal sealed class GetSupplierProviderTypesQueryHandler(ISupplierProviderRegistry registry)
    : IRequestHandler<GetSupplierProviderTypesQuery, Result<IReadOnlyCollection<string>>>
{
    public Task<Result<IReadOnlyCollection<string>>> Handle(GetSupplierProviderTypesQuery request, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success(registry.GetRegisteredProviderTypes()));
}

public sealed record GetSupplierMappingsQuery(Guid SupplierId) : IRequest<Result<IReadOnlyList<SupplierMappingDto>>>;

internal sealed class GetSupplierMappingsQueryHandler(ISuppliersDbContext dbContext)
    : IRequestHandler<GetSupplierMappingsQuery, Result<IReadOnlyList<SupplierMappingDto>>>
{
    public async Task<Result<IReadOnlyList<SupplierMappingDto>>> Handle(GetSupplierMappingsQuery request, CancellationToken cancellationToken)
    {
        var exists = await dbContext.Suppliers.AsNoTracking().AnyAsync(s => s.Id == request.SupplierId, cancellationToken);
        if (!exists)
        {
            return Result.Failure<IReadOnlyList<SupplierMappingDto>>(SupplierErrors.NotFound);
        }

        var mappings = await dbContext.SupplierProductMappings.AsNoTracking()
            .Where(m => m.SupplierId == request.SupplierId)
            .OrderBy(m => m.Priority)
            .Select(m => SupplierMapper.ToMappingDto(m))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<SupplierMappingDto>>(mappings);
    }
}

// ─── Commands: Supplier ────────────────────────────────────────

public sealed record CreateSupplierCommand(CreateSupplierRequest Request) : IRequest<Result<Guid>>;

internal sealed class CreateSupplierCommandHandler(ISuppliersDbContext dbContext, ICurrentUserService currentUser)
    : IRequestHandler<CreateSupplierCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateSupplierCommand request, CancellationToken cancellationToken)
    {
        var r = request.Request;

        var codeExists = await dbContext.Suppliers.AsNoTracking()
            .AnyAsync(s => s.Code == r.Code.Trim().ToUpper(), cancellationToken);
        if (codeExists)
        {
            return Result.Failure<Guid>(SupplierErrors.CodeAlreadyExists);
        }

        if (!Enum.TryParse<SupplierAuthenticationType>(r.AuthenticationType, true, out var authType))
        {
            authType = SupplierAuthenticationType.None;
        }

        var supplier = Supplier.Create(r.Name, r.Code, r.ProviderType, authType, r.BaseUrl, r.Priority);
        supplier.UpdateDetails(
            r.Name, r.ProviderType, authType, r.BaseUrl,
            r.SupportsInventorySync, r.SupportsPriceSync, r.SupportsReservations, r.SupportsOrderStatus, r.SupportsWebhooks);

        dbContext.Suppliers.Add(supplier);
        SupplierAuditWriter.Record(dbContext, supplier.Id, SupplierAuditAction.Created, currentUser.UserId);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(supplier.Id);
    }
}

public sealed record UpdateSupplierCommand(Guid Id, UpdateSupplierRequest Request) : IRequest<Result>;

internal sealed class UpdateSupplierCommandHandler(ISuppliersDbContext dbContext, ICurrentUserService currentUser)
    : IRequestHandler<UpdateSupplierCommand, Result>
{
    public async Task<Result> Handle(UpdateSupplierCommand request, CancellationToken cancellationToken)
    {
        var supplier = await dbContext.Suppliers.FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);
        if (supplier is null)
        {
            return Result.Failure(SupplierErrors.NotFound);
        }

        var r = request.Request;
        if (!Enum.TryParse<SupplierAuthenticationType>(r.AuthenticationType, true, out var authType))
        {
            authType = SupplierAuthenticationType.None;
        }

        supplier.UpdateDetails(
            r.Name, r.ProviderType, authType, r.BaseUrl,
            r.SupportsInventorySync, r.SupportsPriceSync, r.SupportsReservations, r.SupportsOrderStatus, r.SupportsWebhooks);

        SupplierAuditWriter.Record(dbContext, supplier.Id, SupplierAuditAction.Updated, currentUser.UserId);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed record UpdateSupplierCredentialsCommand(Guid Id, UpdateSupplierCredentialsRequest Request) : IRequest<Result>;

internal sealed class UpdateSupplierCredentialsCommandHandler(ISuppliersDbContext dbContext, ICurrentUserService currentUser)
    : IRequestHandler<UpdateSupplierCredentialsCommand, Result>
{
    public async Task<Result> Handle(UpdateSupplierCredentialsCommand request, CancellationToken cancellationToken)
    {
        var supplier = await dbContext.Suppliers.FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);
        if (supplier is null)
        {
            return Result.Failure(SupplierErrors.NotFound);
        }

        var r = request.Request;
        supplier.UpdateCredentials(r.ApiKey, r.ApiSecret, r.Username, r.Password, r.BearerToken, r.OAuthSettingsJson);

        SupplierAuditWriter.Record(dbContext, supplier.Id, SupplierAuditAction.CredentialsUpdated, currentUser.UserId);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed record UpdateSupplierSettingsCommand(Guid Id, UpdateSupplierSettingsRequest Request) : IRequest<Result>;

internal sealed class UpdateSupplierSettingsCommandHandler(ISuppliersDbContext dbContext, ICurrentUserService currentUser)
    : IRequestHandler<UpdateSupplierSettingsCommand, Result>
{
    public async Task<Result> Handle(UpdateSupplierSettingsCommand request, CancellationToken cancellationToken)
    {
        var supplier = await dbContext.Suppliers.FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);
        if (supplier is null)
        {
            return Result.Failure(SupplierErrors.NotFound);
        }

        supplier.UpdateSettings(request.Request.SettingsJson);

        SupplierAuditWriter.Record(dbContext, supplier.Id, SupplierAuditAction.Updated, currentUser.UserId, "{\"field\":\"settings\"}");
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed record UpdateSupplierPriorityCommand(Guid Id, int Priority) : IRequest<Result>;

internal sealed class UpdateSupplierPriorityCommandHandler(ISuppliersDbContext dbContext, ICurrentUserService currentUser)
    : IRequestHandler<UpdateSupplierPriorityCommand, Result>
{
    public async Task<Result> Handle(UpdateSupplierPriorityCommand request, CancellationToken cancellationToken)
    {
        var supplier = await dbContext.Suppliers.FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);
        if (supplier is null)
        {
            return Result.Failure(SupplierErrors.NotFound);
        }

        supplier.UpdatePriority(request.Priority);

        SupplierAuditWriter.Record(dbContext, supplier.Id, SupplierAuditAction.PriorityChanged, currentUser.UserId, $"{{\"priority\":{request.Priority}}}");
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed record EnableSupplierCommand(Guid Id) : IRequest<Result>;

internal sealed class EnableSupplierCommandHandler(ISuppliersDbContext dbContext, ICurrentUserService currentUser)
    : IRequestHandler<EnableSupplierCommand, Result>
{
    public async Task<Result> Handle(EnableSupplierCommand request, CancellationToken cancellationToken)
    {
        var supplier = await dbContext.Suppliers.FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);
        if (supplier is null)
        {
            return Result.Failure(SupplierErrors.NotFound);
        }

        supplier.Enable();
        SupplierAuditWriter.Record(dbContext, supplier.Id, SupplierAuditAction.Enabled, currentUser.UserId);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed record DisableSupplierCommand(Guid Id) : IRequest<Result>;

internal sealed class DisableSupplierCommandHandler(ISuppliersDbContext dbContext, ICurrentUserService currentUser)
    : IRequestHandler<DisableSupplierCommand, Result>
{
    public async Task<Result> Handle(DisableSupplierCommand request, CancellationToken cancellationToken)
    {
        var supplier = await dbContext.Suppliers.FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);
        if (supplier is null)
        {
            return Result.Failure(SupplierErrors.NotFound);
        }

        supplier.Disable();
        SupplierAuditWriter.Record(dbContext, supplier.Id, SupplierAuditAction.Disabled, currentUser.UserId);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed record DeleteSupplierCommand(Guid Id) : IRequest<Result>;

internal sealed class DeleteSupplierCommandHandler(ISuppliersDbContext dbContext, ICurrentUserService currentUser)
    : IRequestHandler<DeleteSupplierCommand, Result>
{
    public async Task<Result> Handle(DeleteSupplierCommand request, CancellationToken cancellationToken)
    {
        var supplier = await dbContext.Suppliers.FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);
        if (supplier is null)
        {
            return Result.Failure(SupplierErrors.NotFound);
        }

        supplier.Delete();
        SupplierAuditWriter.Record(dbContext, supplier.Id, SupplierAuditAction.Deleted, currentUser.UserId);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed record TestSupplierConnectionCommand(Guid Id) : IRequest<Result<SupplierTestConnectionResultDto>>;

internal sealed class TestSupplierConnectionCommandHandler(
    ISuppliersDbContext dbContext,
    ISupplierProviderRegistry registry,
    ICurrentUserService currentUser)
    : IRequestHandler<TestSupplierConnectionCommand, Result<SupplierTestConnectionResultDto>>
{
    public async Task<Result<SupplierTestConnectionResultDto>> Handle(TestSupplierConnectionCommand request, CancellationToken cancellationToken)
    {
        var supplier = await dbContext.Suppliers.AsNoTracking().FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);
        if (supplier is null)
        {
            return Result.Failure<SupplierTestConnectionResultDto>(SupplierErrors.NotFound);
        }

        var providerResult = registry.Resolve(supplier.ProviderType);
        if (providerResult.IsFailure)
        {
            return Result.Failure<SupplierTestConnectionResultDto>(providerResult.Error);
        }

        var context = new SupplierProviderContext(
            supplier.Id,
            supplier.Code,
            supplier.BaseUrl,
            new SupplierProviderCredentials(
                supplier.ApiKey, supplier.ApiSecret, supplier.Username, supplier.Password,
                supplier.BearerToken, supplier.OAuthSettingsJson),
            supplier.SettingsJson);

        var testResult = await providerResult.Value.TestConnectionAsync(context, cancellationToken);

        SupplierAuditWriter.Record(
            dbContext, supplier.Id, SupplierAuditAction.ConnectionTested, currentUser.UserId,
            $"{{\"success\":{testResult.IsSuccess.ToString().ToLowerInvariant()}}}");
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new SupplierTestConnectionResultDto(testResult.IsSuccess, testResult.Message));
    }
}

// ─── Commands: Product Mapping ─────────────────────────────────

public sealed record CreateSupplierMappingCommand(Guid SupplierId, CreateSupplierMappingRequest Request) : IRequest<Result<Guid>>;

internal sealed class CreateSupplierMappingCommandHandler(ISuppliersDbContext dbContext, ICurrentUserService currentUser)
    : IRequestHandler<CreateSupplierMappingCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateSupplierMappingCommand request, CancellationToken cancellationToken)
    {
        var supplierExists = await dbContext.Suppliers.AsNoTracking().AnyAsync(s => s.Id == request.SupplierId, cancellationToken);
        if (!supplierExists)
        {
            return Result.Failure<Guid>(SupplierErrors.NotFound);
        }

        var r = request.Request;
        var mapping = SupplierProductMapping.Create(
            request.SupplierId, r.InternalProductId, r.ExternalProductId, r.ExternalSku, r.ExternalName, r.BuyingPrice, r.Currency, r.Priority);

        dbContext.SupplierProductMappings.Add(mapping);
        SupplierAuditWriter.Record(dbContext, request.SupplierId, SupplierAuditAction.MappingCreated, currentUser.UserId);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(mapping.Id);
    }
}

public sealed record UpdateSupplierMappingCommand(Guid SupplierId, Guid MappingId, UpdateSupplierMappingRequest Request) : IRequest<Result>;

internal sealed class UpdateSupplierMappingCommandHandler(ISuppliersDbContext dbContext, ICurrentUserService currentUser)
    : IRequestHandler<UpdateSupplierMappingCommand, Result>
{
    public async Task<Result> Handle(UpdateSupplierMappingCommand request, CancellationToken cancellationToken)
    {
        var mapping = await dbContext.SupplierProductMappings
            .FirstOrDefaultAsync(m => m.Id == request.MappingId && m.SupplierId == request.SupplierId, cancellationToken);
        if (mapping is null)
        {
            return Result.Failure(SupplierErrors.MappingNotFound);
        }

        var r = request.Request;
        if (!Enum.TryParse<SupplierMappingStatus>(r.Status, true, out var status))
        {
            status = SupplierMappingStatus.Active;
        }

        mapping.Update(r.ExternalProductId, r.ExternalSku, r.ExternalName, r.BuyingPrice, r.Currency, r.Priority, status);

        SupplierAuditWriter.Record(dbContext, request.SupplierId, SupplierAuditAction.MappingUpdated, currentUser.UserId);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed record DeleteSupplierMappingCommand(Guid SupplierId, Guid MappingId) : IRequest<Result>;

internal sealed class DeleteSupplierMappingCommandHandler(ISuppliersDbContext dbContext, ICurrentUserService currentUser)
    : IRequestHandler<DeleteSupplierMappingCommand, Result>
{
    public async Task<Result> Handle(DeleteSupplierMappingCommand request, CancellationToken cancellationToken)
    {
        var mapping = await dbContext.SupplierProductMappings
            .FirstOrDefaultAsync(m => m.Id == request.MappingId && m.SupplierId == request.SupplierId, cancellationToken);
        if (mapping is null)
        {
            return Result.Failure(SupplierErrors.MappingNotFound);
        }

        dbContext.SupplierProductMappings.Remove(mapping);
        SupplierAuditWriter.Record(dbContext, request.SupplierId, SupplierAuditAction.MappingDeleted, currentUser.UserId);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
