using Auralis.Graphics.Abstractions;

namespace Auralis.Graphics.Backends.OpenGL;

/// <summary>Validates compute dispatch group counts against the active context's indexed limits.</summary>
internal static class OpenGLComputeDispatchValidation
{
    internal static void Validate(
        uint groupCountX,
        uint groupCountY,
        uint groupCountZ,
        uint maxGroupCountX,
        uint maxGroupCountY,
        uint maxGroupCountZ)
    {
        // OpenGL defines a zero count in any dimension as a valid no-op.
        if (groupCountX == 0 || groupCountY == 0 || groupCountZ == 0)
            return;

        if (maxGroupCountX == 0 || maxGroupCountY == 0 || maxGroupCountZ == 0)
            throw new GraphicsValidationException(
                "The OpenGL device did not report valid compute dispatch-count limits.");

        if (groupCountX > maxGroupCountX ||
            groupCountY > maxGroupCountY ||
            groupCountZ > maxGroupCountZ)
        {
            throw new GraphicsValidationException(
                $"Compute dispatch ({groupCountX}, {groupCountY}, {groupCountZ}) exceeds the OpenGL work-group count limits " +
                $"({maxGroupCountX}, {maxGroupCountY}, {maxGroupCountZ}).");
        }
    }
}
