using Auralis.Graphics.Abstractions;
using Auralis.Graphics.Devices;

namespace Auralis.Graphics.Backends.OpenGL;

/// <summary>Rejects device options the injected-context OpenGL backend cannot satisfy.</summary>
internal static class OpenGLDeviceOptionsValidation
{
    internal static void Validate(GraphicsDeviceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        if (options.Adapter is not null)
            throw new GraphicsBackendUnavailableException(
                GraphicsBackend.OpenGL,
                "The OpenGL backend consumes an already-selected context and cannot enumerate or select a physical adapter ID.");

        GraphicsDebugOptions unsupported = options.DebugOptions &
            GraphicsDebugOptions.EnableGpuBasedValidation;
        if (unsupported != GraphicsDebugOptions.None)
            throw new GraphicsBackendUnavailableException(
                GraphicsBackend.OpenGL,
                $"The requested OpenGL debug option is not implemented: {unsupported}. " +
                "GL_KHR_debug output is available when EnableDebugLayer is requested; GPU-based validation is not implemented.");
    }
}
