using Auralis.Graphics.Abstractions;

namespace Auralis.Graphics.Backends.OpenGL;

/// <summary>Validates optional pipeline state against actually loaded OpenGL extension wrappers.</summary>
internal static class OpenGLPipelineFeatureValidation
{
    internal static void ValidateDepthBiasClamp(float clamp, bool supported)
    {
        if (!float.IsFinite(clamp))
            throw new ArgumentOutOfRangeException(nameof(clamp));
        if (clamp != 0 && !supported)
            throw new GraphicsFeatureNotSupportedException(
                GraphicsFeature.DepthBiasClamp,
                GraphicsFeature.DepthBiasClamp);
    }

    internal static void ValidateDepthBounds(bool enabled, bool supported)
    {
        if (enabled && !supported)
            throw new GraphicsFeatureNotSupportedException(
                GraphicsFeature.DepthBoundsTest,
                GraphicsFeature.DepthBoundsTest);
    }
}
