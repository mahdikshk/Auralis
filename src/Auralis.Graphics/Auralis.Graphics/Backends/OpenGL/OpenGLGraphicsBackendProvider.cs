using Auralis.Graphics.Abstractions;
using Auralis.Graphics.Devices;

namespace Auralis.Graphics.Backends.OpenGL;

/// <summary>
/// Explicit provider for a host-created desktop OpenGL 4.5 Core Profile context.
/// No windowing or native context-creation package is required.
/// </summary>
public sealed class OpenGLGraphicsBackendProvider : IGraphicsBackendProvider
{
    private readonly IOpenGLContextProvider _contextProvider;

    public OpenGLGraphicsBackendProvider(IOpenGLContextProvider contextProvider)
    {
        _contextProvider = contextProvider ?? throw new ArgumentNullException(nameof(contextProvider));
        ArgumentNullException.ThrowIfNull(contextProvider.ContextIdentity);
        if (!Enum.IsDefined(contextProvider.Ownership))
            throw new ArgumentOutOfRangeException(nameof(contextProvider), "Context ownership is invalid.");
    }

    public GraphicsBackend Backend => GraphicsBackend.OpenGL;

    public bool IsAvailable => _contextProvider.IsAvailable;

    public string? UnavailableReason => IsAvailable
        ? null
        : _contextProvider.UnavailableReason ?? "The injected OpenGL context provider is unavailable.";

    public ValueTask<ReadOnlyMemory<GraphicsAdapterInfo>> EnumerateAdaptersAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsAvailable)
            throw new GraphicsBackendUnavailableException(Backend, UnavailableReason);

        // A context provider supplies one already-selected context, not a system-wide adapter
        // enumeration facility. Return no fabricated physical-adapter entries.
        return ValueTask.FromResult<ReadOnlyMemory<GraphicsAdapterInfo>>(ReadOnlyMemory<GraphicsAdapterInfo>.Empty);
    }

    public ValueTask<IGraphicsDevice> CreateDeviceAsync(
        GraphicsDeviceOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsAvailable)
            throw new GraphicsBackendUnavailableException(Backend, UnavailableReason);
        if (options.Adapter is { } requested && !requested.IsDefault)
            throw new GraphicsBackendUnavailableException(Backend,
                "The injected context provider cannot select among physical adapters.");
        if (options.DebugOptions != GraphicsDebugOptions.None)
            throw new NotSupportedException(
                "OpenGL debug-context options must be configured by the host before supplying the context.");

        IGraphicsDevice device = OpenGLGraphicsDevice.Create(_contextProvider, options, cancellationToken);
        return ValueTask.FromResult(device);
    }
}
