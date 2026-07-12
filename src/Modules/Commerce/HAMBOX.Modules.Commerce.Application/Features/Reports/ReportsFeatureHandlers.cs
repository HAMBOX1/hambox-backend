using System.Text.Json;
using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Contracts.Reports;
using HAMBOX.Modules.Commerce.Domain.Operations;
using HAMBOX.Modules.Commerce.Domain.Reports;
using HAMBOX.SharedKernel.Errors;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Reports;

public sealed record GetReportTypesQuery : IRequest<Result<IReadOnlyList<ReportTypeInfoDto>>>;

public sealed record GetReportLibraryQuery(string? Search, string? ReportType)
    : IRequest<Result<IReadOnlyList<ReportDefinitionDto>>>;

public sealed record CreateReportDefinitionCommand(
    string Name,
    string ReportType,
    string? FiltersJson,
    string FormatDefault) : IRequest<Result<ReportDefinitionDto>>;

public sealed record UpdateReportDefinitionCommand(
    Guid Id,
    string Name,
    string? FiltersJson,
    string FormatDefault) : IRequest<Result<ReportDefinitionDto>>;

public sealed record DeleteReportDefinitionCommand(Guid Id) : IRequest<Result>;

public sealed record AddReportFavoriteCommand(Guid DefinitionId) : IRequest<Result>;

public sealed record RemoveReportFavoriteCommand(Guid DefinitionId) : IRequest<Result>;

public sealed record GetReportFavoritesQuery : IRequest<Result<IReadOnlyList<ReportDefinitionDto>>>;

public sealed record GetReportDownloadsQuery : IRequest<Result<IReadOnlyList<ReportDownloadDto>>>;

public sealed record GenerateReportCommand(
    string ReportType,
    string Format,
    string? Preset,
    DateTimeOffset? From,
    DateTimeOffset? To,
    string? Status,
    Guid? CategoryId,
    Guid? MembershipPlanId,
    Guid? PromotionId,
    string? Country,
    string? Currency) : IRequest<Result<ReportDocumentResult>>;

public sealed record GetScheduledReportsQuery : IRequest<Result<IReadOnlyList<ScheduledReportDto>>>;

public sealed record CreateScheduledReportCommand(
    string ReportType,
    string? FiltersJson,
    string Format,
    string Frequency,
    IReadOnlyList<string> EmailRecipients,
    bool IsEnabled,
    Guid? ReportDefinitionId) : IRequest<Result<ScheduledReportDto>>;

public sealed record UpdateScheduledReportCommand(
    Guid Id,
    string? FiltersJson,
    string Format,
    string Frequency,
    IReadOnlyList<string> EmailRecipients,
    bool IsEnabled) : IRequest<Result<ScheduledReportDto>>;

public sealed record DeleteScheduledReportCommand(Guid Id) : IRequest<Result>;

public sealed record RunScheduledReportCommand(Guid Id) : IRequest<Result<Guid>>;

public sealed record GetScheduledReportExecutionsQuery(Guid Id)
    : IRequest<Result<IReadOnlyList<ScheduledReportExecutionDto>>>;

internal sealed class GetReportTypesQueryHandler(IReportCatalog catalog)
    : IRequestHandler<GetReportTypesQuery, Result<IReadOnlyList<ReportTypeInfoDto>>>
{
    public Task<Result<IReadOnlyList<ReportTypeInfoDto>>> Handle(
        GetReportTypesQuery request,
        CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success(catalog.GetTypes()));
}

internal sealed class GetReportLibraryQueryHandler(
    ICommerceDbContext db,
    ICurrentUserService currentUser)
    : IRequestHandler<GetReportLibraryQuery, Result<IReadOnlyList<ReportDefinitionDto>>>
{
    public async Task<Result<IReadOnlyList<ReportDefinitionDto>>> Handle(
        GetReportLibraryQuery request,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? string.Empty;
        var query = db.ReportDefinitions.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.ReportType))
        {
            query = query.Where(x => x.ReportType == request.ReportType);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(x => x.Name.Contains(term) || x.ReportType.Contains(term));
        }

        var definitions = await query
            .OrderByDescending(x => x.IsSystem)
            .ThenByDescending(x => x.CreatedOnUtc)
            .ToListAsync(cancellationToken);

        var favoriteIds = await db.ReportFavorites.AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => x.ReportDefinitionId)
            .ToListAsync(cancellationToken);
        var favoriteSet = favoriteIds.ToHashSet();

        var dtos = definitions.Select(x => new ReportDefinitionDto(
            x.Id,
            x.Name,
            x.ReportType,
            x.FiltersJson,
            x.FormatDefault,
            x.IsSystem,
            x.CreatedByUserId,
            x.CreatedOnUtc,
            x.ModifiedOnUtc,
            favoriteSet.Contains(x.Id))).ToList();

        return Result.Success<IReadOnlyList<ReportDefinitionDto>>(dtos);
    }
}

internal sealed class CreateReportDefinitionCommandHandler(
    ICommerceDbContext db,
    IReportCatalog catalog,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateReportDefinitionCommand, Result<ReportDefinitionDto>>
{
    public async Task<Result<ReportDefinitionDto>> Handle(
        CreateReportDefinitionCommand request,
        CancellationToken cancellationToken)
    {
        if (catalog.GetType(request.ReportType) is null)
        {
            return Result.Failure<ReportDefinitionDto>(
                new Error("Reports.UnknownType", $"Unknown report type '{request.ReportType}'."));
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Result.Failure<ReportDefinitionDto>(
                new Error("Reports.NameRequired", "Report name is required."));
        }

        var entity = ReportDefinition.Create(
            request.Name,
            request.ReportType,
            request.FiltersJson,
            request.FormatDefault,
            currentUser.UserId);

        db.ReportDefinitions.Add(entity);
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success(new ReportDefinitionDto(
            entity.Id,
            entity.Name,
            entity.ReportType,
            entity.FiltersJson,
            entity.FormatDefault,
            entity.IsSystem,
            entity.CreatedByUserId,
            entity.CreatedOnUtc,
            entity.ModifiedOnUtc,
            false));
    }
}

internal sealed class UpdateReportDefinitionCommandHandler(
    ICommerceDbContext db,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdateReportDefinitionCommand, Result<ReportDefinitionDto>>
{
    public async Task<Result<ReportDefinitionDto>> Handle(
        UpdateReportDefinitionCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await db.ReportDefinitions.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
        if (entity is null)
        {
            return Result.Failure<ReportDefinitionDto>(
                new Error("Reports.NotFound", "Report definition not found."));
        }

        if (entity.IsSystem)
        {
            return Result.Failure<ReportDefinitionDto>(
                new Error("Reports.SystemLocked", "System report definitions cannot be edited."));
        }

        if (!string.Equals(entity.CreatedByUserId, currentUser.UserId, StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(entity.CreatedByUserId))
        {
            // Owners can edit their own; admins with View can still update if they created it.
        }

        entity.Update(request.Name, request.FiltersJson, request.FormatDefault);
        await db.SaveChangesAsync(cancellationToken);

        var isFavorite = await db.ReportFavorites.AsNoTracking()
            .AnyAsync(x => x.UserId == currentUser.UserId && x.ReportDefinitionId == entity.Id, cancellationToken);

        return Result.Success(new ReportDefinitionDto(
            entity.Id,
            entity.Name,
            entity.ReportType,
            entity.FiltersJson,
            entity.FormatDefault,
            entity.IsSystem,
            entity.CreatedByUserId,
            entity.CreatedOnUtc,
            entity.ModifiedOnUtc,
            isFavorite));
    }
}

internal sealed class DeleteReportDefinitionCommandHandler(ICommerceDbContext db)
    : IRequestHandler<DeleteReportDefinitionCommand, Result>
{
    public async Task<Result> Handle(DeleteReportDefinitionCommand request, CancellationToken cancellationToken)
    {
        var entity = await db.ReportDefinitions.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
        if (entity is null)
        {
            return Result.Failure(new Error("Reports.NotFound", "Report definition not found."));
        }

        if (entity.IsSystem)
        {
            return Result.Failure(new Error("Reports.SystemLocked", "System report definitions cannot be deleted."));
        }

        var favorites = await db.ReportFavorites
            .Where(x => x.ReportDefinitionId == entity.Id)
            .ToListAsync(cancellationToken);
        db.ReportFavorites.RemoveRange(favorites);
        db.ReportDefinitions.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

internal sealed class AddReportFavoriteCommandHandler(
    ICommerceDbContext db,
    ICurrentUserService currentUser)
    : IRequestHandler<AddReportFavoriteCommand, Result>
{
    public async Task<Result> Handle(AddReportFavoriteCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId;
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Result.Failure(new Error("Reports.Unauthorized", "User is required."));
        }

        var exists = await db.ReportDefinitions.AsNoTracking()
            .AnyAsync(x => x.Id == request.DefinitionId, cancellationToken);
        if (!exists)
        {
            return Result.Failure(new Error("Reports.NotFound", "Report definition not found."));
        }

        var already = await db.ReportFavorites.AsNoTracking()
            .AnyAsync(x => x.UserId == userId && x.ReportDefinitionId == request.DefinitionId, cancellationToken);
        if (!already)
        {
            db.ReportFavorites.Add(ReportFavorite.Create(userId, request.DefinitionId));
            await db.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }
}

internal sealed class RemoveReportFavoriteCommandHandler(
    ICommerceDbContext db,
    ICurrentUserService currentUser)
    : IRequestHandler<RemoveReportFavoriteCommand, Result>
{
    public async Task<Result> Handle(RemoveReportFavoriteCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? string.Empty;
        var favorites = await db.ReportFavorites
            .Where(x => x.UserId == userId && x.ReportDefinitionId == request.DefinitionId)
            .ToListAsync(cancellationToken);
        if (favorites.Count == 0)
        {
            return Result.Success();
        }

        db.ReportFavorites.RemoveRange(favorites);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

internal sealed class GetReportFavoritesQueryHandler(
    ICommerceDbContext db,
    ICurrentUserService currentUser)
    : IRequestHandler<GetReportFavoritesQuery, Result<IReadOnlyList<ReportDefinitionDto>>>
{
    public async Task<Result<IReadOnlyList<ReportDefinitionDto>>> Handle(
        GetReportFavoritesQuery request,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? string.Empty;
        var favoriteIds = await db.ReportFavorites.AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => x.ReportDefinitionId)
            .ToListAsync(cancellationToken);

        var definitions = await db.ReportDefinitions.AsNoTracking()
            .Where(x => favoriteIds.Contains(x.Id))
            .OrderByDescending(x => x.CreatedOnUtc)
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<ReportDefinitionDto>>(definitions.Select(x => new ReportDefinitionDto(
            x.Id,
            x.Name,
            x.ReportType,
            x.FiltersJson,
            x.FormatDefault,
            x.IsSystem,
            x.CreatedByUserId,
            x.CreatedOnUtc,
            x.ModifiedOnUtc,
            true)).ToList());
    }
}

internal sealed class GetReportDownloadsQueryHandler(
    ICommerceDbContext db,
    ICurrentUserService currentUser)
    : IRequestHandler<GetReportDownloadsQuery, Result<IReadOnlyList<ReportDownloadDto>>>
{
    public async Task<Result<IReadOnlyList<ReportDownloadDto>>> Handle(
        GetReportDownloadsQuery request,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? string.Empty;
        var query = db.ReportDownloads.AsNoTracking().AsQueryable();

        // Non-admin callers only see their own history; admins with Reports.View see all via same endpoint
        // when they have Operations-style elevated access — keep scoped to current user for privacy.
        query = query.Where(x => x.UserId == userId);

        var items = await query
            .OrderByDescending(x => x.CreatedOnUtc)
            .Take(200)
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<ReportDownloadDto>>(items.Select(x => new ReportDownloadDto(
            x.Id,
            x.UserId,
            x.ReportType,
            x.Format,
            x.FileName,
            x.FileSizeBytes,
            x.CreatedOnUtc,
            x.CorrelationId)).ToList());
    }
}

internal sealed class GenerateReportCommandHandler(
    IReportCatalog catalog,
    IReportBuilderService builder,
    IReportDocumentGenerator generator,
    ICommerceDbContext db,
    ICurrentUserService currentUser)
    : IRequestHandler<GenerateReportCommand, Result<ReportDocumentResult>>
{
    public async Task<Result<ReportDocumentResult>> Handle(
        GenerateReportCommand request,
        CancellationToken cancellationToken)
    {
        if (catalog.GetType(request.ReportType) is null)
        {
            return Result.Failure<ReportDocumentResult>(
                new Error("Reports.UnknownType", $"Unknown report type '{request.ReportType}'."));
        }

        var format = (request.Format ?? ReportFormats.Pdf).Trim().ToLowerInvariant();
        if (format is "excel")
        {
            format = ReportFormats.Xlsx;
        }

        if (!ReportFormats.All.Contains(format) && format != "excel")
        {
            return Result.Failure<ReportDocumentResult>(
                new Error("Reports.InvalidFormat", "Format must be pdf, xlsx, csv, or json."));
        }

        var filters = new ReportFilterRequest(
            request.Preset,
            request.From,
            request.To,
            request.Status,
            request.CategoryId,
            request.MembershipPlanId,
            request.PromotionId,
            request.Country,
            request.Currency);

        ReportModel model;
        try
        {
            model = await builder.BuildAsync(request.ReportType, filters, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<ReportDocumentResult>(new Error("Reports.BuildFailed", ex.Message));
        }

        var document = await generator.GenerateAsync(new ReportDocumentRequest(model, format), cancellationToken);
        var filtersJson = JsonSerializer.Serialize(filters);

        db.ReportDownloads.Add(ReportDownload.Create(
            currentUser.UserId ?? "unknown",
            request.ReportType,
            format,
            filtersJson,
            document.FileName,
            document.Content.LongLength));

        db.OperationalAuditLogs.Add(OperationalAuditLog.Create(
            "ReportGenerated",
            currentUser.UserId,
            currentUser.UserId,
            "Report",
            request.ReportType,
            $"format={format};file={document.FileName}"));

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(document);
    }
}

internal sealed class GetScheduledReportsQueryHandler(IScheduledReportService scheduledReports)
    : IRequestHandler<GetScheduledReportsQuery, Result<IReadOnlyList<ScheduledReportDto>>>
{
    public async Task<Result<IReadOnlyList<ScheduledReportDto>>> Handle(
        GetScheduledReportsQuery request,
        CancellationToken cancellationToken) =>
        Result.Success(await scheduledReports.ListAsync(cancellationToken));
}

internal sealed class CreateScheduledReportCommandHandler(
    IScheduledReportService scheduledReports,
    IReportCatalog catalog,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateScheduledReportCommand, Result<ScheduledReportDto>>
{
    public async Task<Result<ScheduledReportDto>> Handle(
        CreateScheduledReportCommand request,
        CancellationToken cancellationToken)
    {
        if (catalog.GetType(request.ReportType) is null)
        {
            return Result.Failure<ScheduledReportDto>(
                new Error("Reports.UnknownType", $"Unknown report type '{request.ReportType}'."));
        }

        var dto = await scheduledReports.CreateAsync(
            request.ReportType,
            request.FiltersJson,
            request.Format,
            request.Frequency,
            request.EmailRecipients ?? [],
            request.IsEnabled,
            currentUser.UserId,
            request.ReportDefinitionId,
            cancellationToken);

        return Result.Success(dto);
    }
}

internal sealed class UpdateScheduledReportCommandHandler(IScheduledReportService scheduledReports)
    : IRequestHandler<UpdateScheduledReportCommand, Result<ScheduledReportDto>>
{
    public async Task<Result<ScheduledReportDto>> Handle(
        UpdateScheduledReportCommand request,
        CancellationToken cancellationToken)
    {
        var dto = await scheduledReports.UpdateAsync(
            request.Id,
            request.FiltersJson,
            request.Format,
            request.Frequency,
            request.EmailRecipients ?? [],
            request.IsEnabled,
            cancellationToken);

        return dto is null
            ? Result.Failure<ScheduledReportDto>(new Error("Reports.ScheduleNotFound", "Scheduled report not found."))
            : Result.Success(dto);
    }
}

internal sealed class DeleteScheduledReportCommandHandler(IScheduledReportService scheduledReports)
    : IRequestHandler<DeleteScheduledReportCommand, Result>
{
    public async Task<Result> Handle(DeleteScheduledReportCommand request, CancellationToken cancellationToken)
    {
        var deleted = await scheduledReports.DeleteAsync(request.Id, cancellationToken);
        return deleted
            ? Result.Success()
            : Result.Failure(new Error("Reports.ScheduleNotFound", "Scheduled report not found."));
    }
}

internal sealed class RunScheduledReportCommandHandler(IScheduledReportService scheduledReports)
    : IRequestHandler<RunScheduledReportCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(RunScheduledReportCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var executionId = await scheduledReports.EnqueueManualRunAsync(request.Id, cancellationToken);
            return Result.Success(executionId);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<Guid>(new Error("Reports.ScheduleNotFound", ex.Message));
        }
    }
}

internal sealed class GetScheduledReportExecutionsQueryHandler(IScheduledReportService scheduledReports)
    : IRequestHandler<GetScheduledReportExecutionsQuery, Result<IReadOnlyList<ScheduledReportExecutionDto>>>
{
    public async Task<Result<IReadOnlyList<ScheduledReportExecutionDto>>> Handle(
        GetScheduledReportExecutionsQuery request,
        CancellationToken cancellationToken) =>
        Result.Success(await scheduledReports.GetExecutionsAsync(request.Id, cancellationToken));
}
