using Auralis.Graphics.Abstractions;
using Auralis.Graphics.Backends.OpenGL;
using Auralis.Graphics.Devices;

namespace Auralis.Graphics.OpenGL.TestSupport;

/// <summary>Owns one real GLFW OpenGL context and one Auralis device for a test/benchmark process.</summary>
public sealed class GlfwOpenGLHardwareSession : IDisposable
{
    private GlfwOpenGLHardwareSession(
        GlfwOpenGLContextProvider contextProvider,
        IGraphicsDevice device)
    {
        ContextProvider = contextProvider;
        Device = device;
    }

    public GlfwOpenGLContextProvider ContextProvider { get; }
    public IGraphicsDevice Device { get; }

    public static async ValueTask<GlfwOpenGLHardwareSession> CreateAsync(
        CancellationToken cancellationToken = default)
    {
        GlfwOpenGLContextProvider contextProvider = GlfwOpenGLContextProvider.Create();
        try
        {
            GraphicsBackendProviderRegistry registry = new();
            registry.Register(new OpenGLGraphicsBackendProvider(contextProvider));

            IGraphicsDevice device = await GraphicsDeviceFactory.CreateAsync(
                registry,
                new GraphicsDeviceOptions
                {
                    Backend = GraphicsBackend.OpenGL,
                    RequiredQueues =
                        GraphicsQueueCapabilities.Graphics |
                        GraphicsQueueCapabilities.Compute |
                        GraphicsQueueCapabilities.Copy
                },
                cancellationToken).ConfigureAwait(false);

            return new GlfwOpenGLHardwareSession(contextProvider, device);
        }
        catch
        {
            contextProvider.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        // The provider transfers context ownership to the device. Device disposal releases all
        // Auralis GL resources before destroying the GLFW context and native library.
        Device.Dispose();
    }
}
