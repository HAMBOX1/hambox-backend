using HAMBOX.Application.PlatformSettings;
using HAMBOX.Modules.Identity.Application.Abstractions;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Identity.Application.Features.PlatformSettings;

public sealed record GetPlatformSettingsQuery : IRequest<Result<IReadOnlyList<PlatformSettingsCategoryDto>>>;

internal sealed class GetPlatformSettingsQueryHandler(IPlatformSettingsService settingsService)
    : IRequestHandler<GetPlatformSettingsQuery, Result<IReadOnlyList<PlatformSettingsCategoryDto>>>
{
    public async Task<Result<IReadOnlyList<PlatformSettingsCategoryDto>>> Handle(
        GetPlatformSettingsQuery request,
        CancellationToken cancellationToken) =>
        Result.Success(await settingsService.GetAllCategoriesAsync(cancellationToken));
}

public sealed record GetPlatformSettingsCategoryQuery(string CategoryKey)
    : IRequest<Result<PlatformSettingsCategoryDto>>;

internal sealed class GetPlatformSettingsCategoryQueryHandler(IPlatformSettingsService settingsService)
    : IRequestHandler<GetPlatformSettingsCategoryQuery, Result<PlatformSettingsCategoryDto>>
{
    public async Task<Result<PlatformSettingsCategoryDto>> Handle(
        GetPlatformSettingsCategoryQuery request,
        CancellationToken cancellationToken) =>
        Result.Success(await settingsService.GetCategoryAsync(request.CategoryKey, cancellationToken));
}

public sealed record UpdatePlatformSettingsCategoryCommand(
    string CategoryKey,
    string PayloadJson,
    string? ActorUserId,
    string? ActorDisplayName) : IRequest<Result<PlatformSettingsCategoryDto>>;

internal sealed class UpdatePlatformSettingsCategoryCommandHandler(IPlatformSettingsService settingsService)
    : IRequestHandler<UpdatePlatformSettingsCategoryCommand, Result<PlatformSettingsCategoryDto>>
{
    public async Task<Result<PlatformSettingsCategoryDto>> Handle(
        UpdatePlatformSettingsCategoryCommand request,
        CancellationToken cancellationToken) =>
        Result.Success(await settingsService.UpdateCategoryAsync(
            request.CategoryKey,
            request.PayloadJson,
            request.ActorUserId,
            request.ActorDisplayName,
            cancellationToken));
}

public sealed record RestorePlatformSettingsCategoryCommand(
    string CategoryKey,
    string? ActorUserId,
    string? ActorDisplayName) : IRequest<Result<PlatformSettingsCategoryDto>>;

internal sealed class RestorePlatformSettingsCategoryCommandHandler(IPlatformSettingsService settingsService)
    : IRequestHandler<RestorePlatformSettingsCategoryCommand, Result<PlatformSettingsCategoryDto>>
{
    public async Task<Result<PlatformSettingsCategoryDto>> Handle(
        RestorePlatformSettingsCategoryCommand request,
        CancellationToken cancellationToken) =>
        Result.Success(await settingsService.RestoreDefaultsAsync(
            request.CategoryKey,
            request.ActorUserId,
            request.ActorDisplayName,
            cancellationToken));
}

public sealed record GetPlatformSettingsAuditQuery(int Take = 50)
    : IRequest<Result<IReadOnlyList<PlatformSettingsAuditEntryDto>>>;

internal sealed class GetPlatformSettingsAuditQueryHandler(IPlatformSettingsService settingsService)
    : IRequestHandler<GetPlatformSettingsAuditQuery, Result<IReadOnlyList<PlatformSettingsAuditEntryDto>>>
{
    public async Task<Result<IReadOnlyList<PlatformSettingsAuditEntryDto>>> Handle(
        GetPlatformSettingsAuditQuery request,
        CancellationToken cancellationToken) =>
        Result.Success(await settingsService.GetAuditLogAsync(request.Take, cancellationToken));
}

public sealed record TestPlatformSettingsEmailCommand(string TestEmail)
    : IRequest<Result>;

internal sealed class TestPlatformSettingsEmailCommandHandler(IPlatformSettingsService settingsService)
    : IRequestHandler<TestPlatformSettingsEmailCommand, Result>
{
    public async Task<Result> Handle(TestPlatformSettingsEmailCommand request, CancellationToken cancellationToken)
    {
        await settingsService.SendTestEmailAsync(request.TestEmail, cancellationToken);
        return Result.Success();
    }
}

public sealed record GetPublicPlatformSettingsQuery : IRequest<Result<PublicPlatformSettingsDto>>;

internal sealed class GetPublicPlatformSettingsQueryHandler(IPlatformSettingsService settingsService)
    : IRequestHandler<GetPublicPlatformSettingsQuery, Result<PublicPlatformSettingsDto>>
{
    public async Task<Result<PublicPlatformSettingsDto>> Handle(
        GetPublicPlatformSettingsQuery request,
        CancellationToken cancellationToken) =>
        Result.Success(await settingsService.GetPublicSettingsAsync(cancellationToken));
}
