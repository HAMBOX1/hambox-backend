using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Contracts.Account;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Account.Notifications.GetNotifications;

public sealed record GetNotificationsQuery(
    int PageNumber,
    int PageSize,
    bool IncludeArchived = false,
    bool? IsRead = null,
    string? Category = null,
    string? SearchTerm = null) : IRequest<Result<PagedResult<UserNotificationDto>>>;

internal sealed class GetNotificationsQueryHandler
    : IRequestHandler<GetNotificationsQuery, Result<PagedResult<UserNotificationDto>>>
{
    private readonly ICommerceDbContext _commerceDbContext;
    private readonly ICurrentUserService _currentUserService;

    public GetNotificationsQueryHandler(
        ICommerceDbContext commerceDbContext,
        ICurrentUserService currentUserService)
    {
        _commerceDbContext = commerceDbContext;
        _currentUserService = currentUserService;
    }

    public async Task<Result<PagedResult<UserNotificationDto>>> Handle(
        GetNotificationsQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.UserId is null)
        {
            return Result.Failure<PagedResult<UserNotificationDto>>(CommerceErrors.AuthenticationRequired);
        }

        var pageNumber = Math.Max(1, request.PageNumber);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var query = _commerceDbContext.UserNotifications
            .Where(n => n.UserId == _currentUserService.UserId);

        if (!request.IncludeArchived)
        {
            query = query.Where(n => !n.IsArchived);
        }

        if (request.IsRead.HasValue)
        {
            query = query.Where(n => n.IsRead == request.IsRead.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Category))
        {
            query = query.Where(n => n.Category == request.Category);
        }

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim();
            query = query.Where(n => n.Title.Contains(term) || n.Body.Contains(term));
        }

        query = query.OrderByDescending(n => n.CreatedOnUtc);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var dtos = items.Select(AccountMapper.ToUserNotificationDto).ToList();

        return Result.Success(new PagedResult<UserNotificationDto>(dtos, pageNumber, pageSize, totalCount));
    }
}
