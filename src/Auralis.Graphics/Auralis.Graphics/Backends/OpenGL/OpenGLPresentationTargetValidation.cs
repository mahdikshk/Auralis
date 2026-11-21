using Auralis.Graphics.Abstractions;
using Auralis.Graphics.Presentation;

namespace Auralis.Graphics.Backends.OpenGL;

/// <summary>Validates that a common presentation snapshot is the drawable belonging to this GL context.</summary>
internal static class OpenGLPresentationTargetValidation
{
    internal static void ValidateTarget(
        IOpenGLContextProvider provider,
        in GraphicsPresentationTargetSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(provider);
        GraphicsPresentationTargetSnapshot copy = snapshot;
        copy.Validate();

        if (!provider.SupportsPresentation)
            throw new GraphicsBackendUnavailableException(
                GraphicsBackend.OpenGL,
                "The injected OpenGL context provider has no presentation drawable.");

        if (!provider.IsPresentationTargetCompatible(copy))
            throw new GraphicsBackendUnavailableException(
                GraphicsBackend.OpenGL,
                "The target's native handles do not identify the drawable associated with the injected OpenGL context.");
    }

    internal static void ValidatePresentMode(
        IOpenGLContextProvider provider,
        GraphicsPresentMode presentMode)
    {
        ArgumentNullException.ThrowIfNull(provider);
        if (!Enum.IsDefined(presentMode))
            throw new ArgumentOutOfRangeException(nameof(presentMode));

        if (!provider.SupportsPresentMode(presentMode))
            throw new GraphicsValidationException(
                $"The OpenGL context provider cannot honor present mode {presentMode}.");
    }
}
