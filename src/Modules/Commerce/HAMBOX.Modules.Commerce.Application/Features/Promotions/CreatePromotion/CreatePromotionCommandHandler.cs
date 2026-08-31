using HAMBOX.Application.Abstractions;
using HAMBOX.Application.PlatformSettings;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Contracts.Promotions;
using HAMBOX.Modules.Commerce.Application.Services;
using HAMBOX.Modules.Commerce.Domain.Promotions;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Commerce.Application.Features.Promotions.CreatePromotion;

public sealed record CreatePromotionCommand(CreatePromotionRequest Request)
    : IRequest<Result<PromotionDetailDto>>;

internal sealed class CreatePromotionCommandHandler : IRequestHandler<CreatePromotionCommand, Result<PromotionDetailDto>>
{
    private readonly ICommerceDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IPlatformSettingsProvider _platformSettings;

    public CreatePromotionCommandHandler(
        ICommerceDbContext dbContext,
        ICurrentUserService currentUserService,
        IPlatformSettingsProvider platformSettings)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _platformSettings = platformSettings;
    }

    public async Task<Result<PromotionDetailDto>> Handle(CreatePromotionCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;
        var type = PromotionMapper.ParseType(request.Type);
        var discountType = PromotionMapper.ParseDiscountType(request.DiscountType);

        var promotion = Promotion.Create(
            request.Name,
            request.Description,
            type,
            discountType,
            request.DiscountValue,
            request.StartDateUtc,
            request.EndDateUtc);

        promotion.ReplaceConditions(PromotionMapper.ParseConditions(request.Conditions));
        promotion.ReplaceTargets(PromotionMapper.ParseTargets(request.Targets));

        if (!string.IsNullOrWhiteSpace(request.InitialCouponCode) && type == PromotionType.CouponCode)
        {
            var promotionsSettings = await _platformSettings.GetAsync<PromotionsSettingsPayload>(
                PlatformSettingsCategoryKeys.Promotions, cancellationToken);
            promotion.AddCouponCode(
                request.InitialCouponCode,
                isSingleUse: false,
                maxUses: promotionsSettings.DefaultCouponUsageLimit,
                perUserMaxUses: promotionsSettings.DefaultPerUserLimit,
                expiresOnUtc: null);
        }

        _dbContext.Promotions.Add(promotion);
        PromotionAuditWriter.Write(
            _dbContext,
            promotion.Id,
            null,
            PromotionAuditAction.Created,
            _currentUserService.UserId,
            $"Promotion '{promotion.Name}' created.");

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success(PromotionMapper.ToDetail(promotion));
    }
}
