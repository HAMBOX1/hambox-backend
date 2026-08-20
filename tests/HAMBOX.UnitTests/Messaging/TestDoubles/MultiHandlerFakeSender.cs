using HAMBOX.Modules.Catalog.Application.Features.Categories.GetCategoryTree;
using HAMBOX.Modules.Catalog.Application.Features.Products.GetProductById;
using HAMBOX.Modules.Catalog.Application.Features.Products.GetProducts;
using HAMBOX.Modules.Catalog.Application.Features.Storefront.GetProductConfiguration;
using HAMBOX.Modules.Commerce.Application.Features.Cart.AddCartItem;
using HAMBOX.Modules.Commerce.Application.Features.Cart.GetCart;
using MediatR;

namespace HAMBOX.UnitTests.Messaging.TestDoubles;

/// <summary>
/// Routes exactly the request types the WhatsApp bot engine's Browse/Search/Cart flow sends through
/// <see cref="ISender"/> straight to real handler instances — no MediatR DI pipeline, mirroring
/// <c>DispatchingFakeSender</c>'s approach but for the fixed handful of request types this scenario
/// needs (see the Catalog/Commerce audit — these are the exact existing queries/commands being reused,
/// not new ones).
/// </summary>
internal sealed class MultiHandlerFakeSender(
    GetCategoryTreeQueryHandler categoryTreeHandler,
    GetProductsQueryHandler productsHandler,
    GetProductByIdQueryHandler productByIdHandler,
    GetStorefrontProductConfigurationsQueryHandler storefrontConfigHandler,
    AddCartItemCommandHandler addCartItemHandler,
    GetCartQueryHandler getCartHandler) : ISender
{
    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default) => request switch
    {
        GetCategoryTreeQuery q => (Task<TResponse>)(object)categoryTreeHandler.Handle(q, cancellationToken),
        GetProductsQuery q => (Task<TResponse>)(object)productsHandler.Handle(q, cancellationToken),
        GetProductByIdQuery q => (Task<TResponse>)(object)productByIdHandler.Handle(q, cancellationToken),
        GetStorefrontProductConfigurationsQuery q => (Task<TResponse>)(object)storefrontConfigHandler.Handle(q, cancellationToken),
        AddCartItemCommand q => (Task<TResponse>)(object)addCartItemHandler.Handle(q, cancellationToken),
        GetCartQuery q => (Task<TResponse>)(object)getCartHandler.Handle(q, cancellationToken),
        _ => throw new NotSupportedException($"This fake only dispatches a fixed set of request types. Got {request.GetType().Name}."),
    };

    public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest =>
        throw new NotSupportedException("Not needed by these tests.");

    public Task<object?> Send(object request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Not needed by these tests.");

    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Not needed by these tests.");

    public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Not needed by these tests.");
}
