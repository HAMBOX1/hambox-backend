using System.Net;
using System.Net.Sockets;
using HAMBOX.Modules.Catalog.Application.Contracts;
using HAMBOX.Modules.Catalog.Application.Errors;
using HAMBOX.Modules.Catalog.Application.Features.Products.Images.UploadProductImage;
using HAMBOX.SharedKernel.Results;
using MediatR;

namespace HAMBOX.Modules.Catalog.Application.Features.Products.Images.ImportProductImageFromUrl;

/// <summary>
/// Downloads a remote image (e.g. a supplier's catalog logo) and stores it as a product image through
/// the exact same path as a manual upload, so size/type limits and the per-product image cap still apply.
/// </summary>
public sealed record ImportProductImageFromUrlCommand(Guid ProductId, string Url) : IRequest<Result<ProductImageDto>>;

internal sealed class ImportProductImageFromUrlCommandHandler(ISender sender)
    : IRequestHandler<ImportProductImageFromUrlCommand, Result<ProductImageDto>>
{
    private const long MaxDownloadBytes = 10 * 1024 * 1024;

    // One shared client: the ConnectCallback rejects non-public addresses on every connection
    // (including redirect hops), so an admin-supplied URL can never reach internal services.
    private static readonly HttpClient Client = new(new SocketsHttpHandler
    {
        AllowAutoRedirect = true,
        MaxAutomaticRedirections = 3,
        ConnectTimeout = TimeSpan.FromSeconds(8),
        ConnectCallback = ConnectToPublicHostAsync,
    })
    {
        Timeout = TimeSpan.FromSeconds(20),
    };

    public async Task<Result<ProductImageDto>> Handle(
        ImportProductImageFromUrlCommand request,
        CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            return Result.Failure<ProductImageDto>(CatalogErrors.InvalidProductImage);
        }

        try
        {
            using var response = await Client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode
                || response.Content.Headers.ContentLength is > MaxDownloadBytes)
            {
                return Result.Failure<ProductImageDto>(CatalogErrors.InvalidProductImage);
            }

            var contentType = response.Content.Headers.ContentType?.MediaType;
            if (string.IsNullOrWhiteSpace(contentType) || !contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                return Result.Failure<ProductImageDto>(CatalogErrors.InvalidProductImage);
            }

            await using var network = await response.Content.ReadAsStreamAsync(cancellationToken);
            var buffer = new MemoryStream();
            var chunk = new byte[81920];
            int read;
            while ((read = await network.ReadAsync(chunk, cancellationToken)) > 0)
            {
                if (buffer.Length + read > MaxDownloadBytes)
                {
                    return Result.Failure<ProductImageDto>(CatalogErrors.InvalidProductImage);
                }

                buffer.Write(chunk, 0, read);
            }

            buffer.Position = 0;
            var fileName = Path.GetFileName(uri.AbsolutePath);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                fileName = "supplier-image";
            }

            return await sender.Send(
                new UploadProductImageCommand(request.ProductId, buffer, fileName, contentType, buffer.Length),
                cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException or SocketException)
        {
            return Result.Failure<ProductImageDto>(CatalogErrors.InvalidProductImage);
        }
    }

    private static async ValueTask<Stream> ConnectToPublicHostAsync(
        SocketsHttpConnectionContext context,
        CancellationToken cancellationToken)
    {
        var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, cancellationToken);
        var address = addresses.FirstOrDefault(IsPublicAddress)
            ?? throw new HttpRequestException("Host does not resolve to a public address.");

        var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), cancellationToken);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    private static bool IsPublicAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any))
        {
            return false;
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return !(address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast
                || (address.GetAddressBytes()[0] & 0xFE) == 0xFC);
        }

        var b = address.GetAddressBytes();
        return !(b[0] == 10
            || b[0] == 127
            || b[0] == 0
            || (b[0] == 100 && b[1] is >= 64 and <= 127)
            || (b[0] == 169 && b[1] == 254)
            || (b[0] == 172 && b[1] is >= 16 and <= 31)
            || (b[0] == 192 && b[1] == 168)
            || (b[0] == 192 && b[1] == 0 && b[2] == 0)
            || b[0] >= 224);
    }
}
