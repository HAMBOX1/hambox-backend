using HAMBOX.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Abstractions;
using HAMBOX.Modules.Commerce.Application.Errors;
using HAMBOX.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HAMBOX.Modules.Commerce.Application.Features.Account.Library;

/// <summary>
/// Streams the picture of a purchased picture code to its owner. It sits behind the same gate as showing a text code:
/// the customer must own the item AND have just revealed it (which already ran the e-mailed-code policy), so the
/// picture can never be fetched around the reveal step.
/// </summary>
public sealed record GetCustomerLibraryImageQuery(Guid LibraryItemId) : IRequest<Result<LibraryImageDto>>;

public sealed record LibraryImageDto(Stream Content, string ContentType);

internal sealed class GetCustomerLibraryImageQueryHandler(
    ICommerceDbContext commerceDb,
    ICurrentUserService currentUser,
    IFileStorage fileStorage) : IRequestHandler<GetCustomerLibraryImageQuery, Result<LibraryImageDto>>
{
    /// <summary>How long after revealing the picture can still be fetched (a page can re-request it while it is open).</summary>
    private static readonly TimeSpan RevealWindow = TimeSpan.FromMinutes(15);

    public async Task<Result<LibraryImageDto>> Handle(GetCustomerLibraryImageQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is null)
        {
            return Result.Failure<LibraryImageDto>(CommerceErrors.AuthenticationRequired);
        }

        var userId = currentUser.UserId;

        var key = await (
            from license in commerceDb.OrderLicenseKeys.AsNoTracking()
            join order in commerceDb.Orders.AsNoTracking() on license.OrderId equals order.Id
            where license.Id == request.LibraryItemId && order.UserId == userId
            select license)
            .FirstOrDefaultAsync(cancellationToken);

        var storageKey = ImageCodes.ToStorageKey(key?.LicenseKey);
        if (key is null || storageKey is null || !ImageCodes.IsPrivateCodeImageKey(storageKey))
        {
            return Result.Failure<LibraryImageDto>(CommerceErrors.OrderLicenseKeyNotFound);
        }

        var marker = key.Id.ToString();
        var since = DateTimeOffset.UtcNow - RevealWindow;
        var revealed = await commerceDb.OrderAuditEntries.AsNoTracking().AnyAsync(
            e => e.OrderId == key.OrderId
                 && e.EventType == LicenseKeyRevealPolicy.EventRevealed
                 && e.ActorUserId == userId
                 && e.Description.Contains(marker)
                 && e.OccurredOnUtc > since,
            cancellationToken);
        if (!revealed)
        {
            return Result.Failure<LibraryImageDto>(CommerceErrors.RevealOtpRequired);
        }

        try
        {
            var stream = await fileStorage.OpenReadAsync(storageKey, cancellationToken);
            return Result.Success(new LibraryImageDto(stream, ContentTypeFor(storageKey)));
        }
        catch (FileNotFoundException)
        {
            return Result.Failure<LibraryImageDto>(CommerceErrors.OrderLicenseKeyNotFound);
        }
    }

    private static string ContentTypeFor(string storageKey) => Path.GetExtension(storageKey).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".webp" => "image/webp",
        ".gif" => "image/gif",
        _ => "image/jpeg",
    };
}
