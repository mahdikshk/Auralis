namespace Auralis.Graphics.Backends.OpenGLES;

/// <summary>
/// Owns one successful MakeCurrent/ReleaseCurrent pair. A failed activation never releases a context
/// that was not successfully made current.
/// </summary>
internal sealed class OpenGLESContextLease : IDisposable
{
    private readonly IOpenGLESContextProvider _provider;
    private int _disposed;

    private OpenGLESContextLease(IOpenGLESContextProvider provider) => _provider = provider;

    internal static OpenGLESContextLease Acquire(IOpenGLESContextProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        OpenGLESContextLease lease = new(provider);
        provider.MakeCurrent();
        return lease;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
            _provider.ReleaseCurrent();
    }
}
