namespace HAMBOX.Infrastructure.Services;

/// <summary>Shared no-op <see cref="IAsyncDisposable"/> singleton for "subscription" APIs that have
/// nothing to actually subscribe to or clean up.</summary>
internal sealed class EmptyAsyncDisposable : IAsyncDisposable
{
    public static readonly EmptyAsyncDisposable Instance = new();

    private EmptyAsyncDisposable()
    {
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
