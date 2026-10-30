using Asp.Versioning.Builder;
using HAMBOX.Modules.Commerce.Application.Features.FloatingAds;
using HAMBOX.Modules.Identity.Application.Authorization;
using HAMBOX.Modules.Identity.Presentation.Extensions;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace HAMBOX.Modules.Commerce.Presentation.Endpoints;

internal static class FloatingAdEndpoints
{
    public static void MapFloatingAdEndpoints(this IEndpointRouteBuilder app, ApiVersionSet apiVersionSet)
    {
        // Storefront: the ads the current visitor should see. Open to guests; signed-in visitors are recognised
        // from their token when one is sent.
        app.MapGet("api/v{version:apiVersion}/floating-ads", async Task<IResult> (ISender sender) =>
                MapResult(await sender.Send(new GetActiveFloatingAdsQuery())))
            .WithApiVersionSet(apiVersionSet)
            .WithTags("FloatingAds")
            .HasApiVersion(1)
            .AllowAnonymous();

        var admin = app.MapGroup("api/v{version:apiVersion}/admin/floating-ads")
            .WithApiVersionSet(apiVersionSet)
            .WithTags("FloatingAds")
            .HasApiVersion(1)
            .RequireAuthorization();

        admin.MapGet("", async Task<IResult> (ISender sender) =>
                MapResult(await sender.Send(new GetFloatingAdsAdminQuery())))
            .RequirePermission(PermissionConstants.Promotions.View);

        admin.MapPost("", async Task<IResult> ([FromBody] FloatingAdInput input, ISender sender) =>
                MapResult(await sender.Send(new CreateFloatingAdCommand(input))))
            .RequirePermission(PermissionConstants.Promotions.Create);

        admin.MapPut("{id:guid}", async Task<IResult> (Guid id, [FromBody] FloatingAdInput input, ISender sender) =>
                MapResult(await sender.Send(new UpdateFloatingAdCommand(id, input))))
            .RequirePermission(PermissionConstants.Promotions.Edit);

        admin.MapPost("{id:guid}/activate", async Task<IResult> (Guid id, ISender sender) =>
                MapResult(await sender.Send(new SetFloatingAdActiveCommand(id, true))))
            .RequirePermission(PermissionConstants.Promotions.Publish);

        admin.MapPost("{id:guid}/deactivate", async Task<IResult> (Guid id, ISender sender) =>
                MapResult(await sender.Send(new SetFloatingAdActiveCommand(id, false))))
            .RequirePermission(PermissionConstants.Promotions.Publish);

        admin.MapDelete("{id:guid}", async Task<IResult> (Guid id, ISender sender) =>
                MapResult(await sender.Send(new DeleteFloatingAdCommand(id))))
            .RequirePermission(PermissionConstants.Promotions.Delete);
    }

    private static IResult MapResult(Result result) =>
        result.IsSuccess
            ? TypedResults.Ok()
            : TypedResults.BadRequest(new ProblemDetails { Title = "Bad Request", Detail = result.Error.Description, Type = result.Error.Code });

    private static IResult MapResult<T>(Result<T> result) =>
        result.IsSuccess
            ? TypedResults.Ok(result.Value)
            : TypedResults.BadRequest(new ProblemDetails { Title = "Bad Request", Detail = result.Error.Description, Type = result.Error.Code });
}
