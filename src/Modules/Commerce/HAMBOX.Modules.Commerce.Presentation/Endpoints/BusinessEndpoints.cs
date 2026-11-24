using Asp.Versioning.Builder;
using HAMBOX.Modules.Commerce.Application.Features.Business;
using HAMBOX.Modules.Commerce.Domain.Business;
using HAMBOX.Modules.Identity.Application.Authorization;
using HAMBOX.Modules.Identity.Presentation.Extensions;
using HAMBOX.SharedKernel.Errors;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace HAMBOX.Modules.Commerce.Presentation.Endpoints;

/// <summary>ELITE Business (B2B): the applicant's own view, and the admin side (approval, wholesale prices, quotes, orders).</summary>
internal static class BusinessEndpoints
{
    public static void MapBusinessEndpoints(this IEndpointRouteBuilder app, ApiVersionSet apiVersionSet)
    {
        var mine = app.MapGroup("api/v{version:apiVersion}/account/business")
            .WithApiVersionSet(apiVersionSet)
            .WithTags("Business")
            .HasApiVersion(1)
            .RequireAuthorization()
            .RequireCustomerContext();

        mine.MapGet("", async Task<IResult> (ISender sender) => MapResult(await sender.Send(new GetMyBusinessQuery())))
            .WithName("GetMyBusiness");

        mine.MapPost("", async Task<IResult> (BusinessApplicationInput input, ISender sender) =>
                MapResult(await sender.Send(new ApplyBusinessCommand(input))))
            .WithName("ApplyForBusiness");

        mine.MapPost("quotes/{id:guid}/respond", async Task<IResult> (Guid id, RespondRequest body, ISender sender) =>
                MapResult(await sender.Send(new RespondToQuoteCommand(id, body.Accept))))
            .WithName("RespondToBusinessQuote");

        var admin = app.MapGroup("api/v{version:apiVersion}/admin/business")
            .WithApiVersionSet(apiVersionSet)
            .WithTags("Admin Business")
            .HasApiVersion(1)
            .RequireAuthorization();

        admin.MapGet("accounts", async Task<IResult> ([FromQuery] string? status, ISender sender) =>
                MapResult(await sender.Send(new GetBusinessAccountsQuery(status))))
            .WithName("GetBusinessAccounts")
            .RequirePermission(PermissionConstants.B2B.View);

        admin.MapPost("accounts/{id:guid}/decision", async Task<IResult> (Guid id, DecisionRequest body, ISender sender) =>
                MapResult(await sender.Send(new DecideBusinessAccountCommand(id, body.Status, body.Reason))))
            .WithName("DecideBusinessAccount")
            .RequirePermission(PermissionConstants.B2B.Approve);

        admin.MapPut("accounts/{id:guid}", async Task<IResult> (Guid id, ConfigureRequest body, ISender sender) =>
                MapResult(await sender.Send(new ConfigureBusinessAccountCommand(id, body.SalesRepUserId, body.StarsEligible))))
            .WithName("ConfigureBusinessAccount")
            .RequirePermission(PermissionConstants.B2B.Manage);

        admin.MapGet("prices", async Task<IResult> ([FromQuery] Guid? accountId, ISender sender) =>
                MapResult(await sender.Send(new GetBusinessPricesQuery(accountId))))
            .WithName("GetBusinessPrices")
            .RequirePermission(PermissionConstants.B2B.View);

        admin.MapPost("prices", async Task<IResult> (BusinessPriceInput input, ISender sender) =>
                MapResult(await sender.Send(new SaveBusinessPriceCommand(null, input))))
            .WithName("CreateBusinessPrice")
            .RequirePermission(PermissionConstants.B2B.Manage);

        admin.MapPut("prices/{id:guid}", async Task<IResult> (Guid id, BusinessPriceInput input, ISender sender) =>
                MapResult(await sender.Send(new SaveBusinessPriceCommand(id, input))))
            .WithName("UpdateBusinessPrice")
            .RequirePermission(PermissionConstants.B2B.Manage);

        admin.MapDelete("prices/{id:guid}", async Task<IResult> (Guid id, ISender sender) =>
            {
                var result = await sender.Send(new DeleteBusinessPriceCommand(id));
                return result.IsSuccess ? Results.NoContent() : Problem(result.Error);
            })
            .WithName("DeleteBusinessPrice")
            .RequirePermission(PermissionConstants.B2B.Manage);

        admin.MapGet("quotes", async Task<IResult> ([FromQuery] string? status, ISender sender) =>
                MapResult(await sender.Send(new GetBusinessQuotesQuery(status))))
            .WithName("GetBusinessQuotes")
            .RequirePermission(PermissionConstants.B2B.View);

        admin.MapPost("quotes", async Task<IResult> (CreateQuoteRequest body, ISender sender) =>
                MapResult(await sender.Send(new CreateBusinessQuoteCommand(body.AccountId, body.ValidDays <= 0 ? 14 : body.ValidDays, body.Terms, body.Lines ?? [], body.SendNow))))
            .WithName("CreateBusinessQuote")
            .RequirePermission(PermissionConstants.B2B.Manage);

        admin.MapPost("quotes/{id:guid}/send", async Task<IResult> (Guid id, ISender sender) =>
            {
                var result = await sender.Send(new SendBusinessQuoteCommand(id));
                return result.IsSuccess ? Results.NoContent() : Problem(result.Error);
            })
            .WithName("SendBusinessQuote")
            .RequirePermission(PermissionConstants.B2B.Manage);

        admin.MapPost("quotes/{id:guid}/order", async Task<IResult> (Guid id, OrderRequest body, ISender sender) =>
                MapResult(await sender.Send(new CreateOrderFromQuoteCommand(id, body.PaymentConfirmed, body.PaymentReference))))
            .WithName("CreateBusinessOrder")
            .RequirePermission(PermissionConstants.B2B.Manage);

        admin.MapPost("orders/{orderId:guid}/confirm-payment", async Task<IResult> (Guid orderId, ConfirmRequest body, ISender sender) =>
                MapResult(await sender.Send(new ConfirmBusinessPaymentCommand(orderId, body.Reference))))
            .WithName("ConfirmBusinessPayment")
            .RequirePermission(PermissionConstants.B2B.Manage);
    }

    private static IResult MapResult<T>(Result<T> result) => result.IsSuccess ? Results.Ok(result.Value) : Problem(result.Error);

    private static IResult Problem(Error error) =>
        Results.BadRequest(new ProblemDetails { Title = "Bad Request", Detail = error.Description, Type = error.Code, Status = StatusCodes.Status400BadRequest });

    internal sealed record RespondRequest(bool Accept);

    internal sealed record DecisionRequest(BusinessAccountStatus Status, string? Reason);

    internal sealed record ConfigureRequest(string? SalesRepUserId, bool StarsEligible);

    internal sealed record CreateQuoteRequest(Guid AccountId, int ValidDays, string? Terms, IReadOnlyList<QuoteLineInput>? Lines, bool SendNow);

    internal sealed record OrderRequest(bool PaymentConfirmed, string? PaymentReference);

    internal sealed record ConfirmRequest(string Reference);
}
