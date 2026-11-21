using Auralis.Graphics.Abstractions;
using Auralis.Graphics.Devices;

namespace Auralis.Graphics.Backends.OpenGLES;

/// <summary>Validates GLES compute dimensions using limits queried from the current device/program.</summary>
internal static class OpenGLESComputeWorkGroupValidation
{
    internal static void ValidateDispatch(
        uint groupCountX, uint groupCountY, uint groupCountZ,
        uint maxGroupCountX, uint maxGroupCountY, uint maxGroupCountZ)
    {
        // GLES defines a zero count in any dimension as a valid no-op.
        if (groupCountX == 0 || groupCountY == 0 || groupCountZ == 0)
            return;

        if (maxGroupCountX == 0 || maxGroupCountY == 0 || maxGroupCountZ == 0)
            throw new GraphicsValidationException(
                "The GLES device did not report valid compute dispatch-count limits.");

        if (groupCountX > maxGroupCountX || groupCountY > maxGroupCountY || groupCountZ > maxGroupCountZ)
            throw new GraphicsValidationException(
                $"OpenGL ES compute dispatch ({groupCountX}, {groupCountY}, {groupCountZ}) exceeds " +
                $"the device work-group count limits ({maxGroupCountX}, {maxGroupCountY}, {maxGroupCountZ}).");
    }

    internal static void ValidateLocalSize(
        int localSizeX, int localSizeY, int localSizeZ, in GraphicsDeviceLimits limits)
    {
        if (localSizeX <= 0 || localSizeY <= 0 || localSizeZ <= 0)
            throw new GraphicsValidationException(
                "The linked GLES compute program returned an invalid local work-group size.");

        uint x = (uint)localSizeX, y = (uint)localSizeY, z = (uint)localSizeZ;
        if (limits.MaxComputeWorkGroupSizeX == 0 || limits.MaxComputeWorkGroupSizeY == 0 ||
            limits.MaxComputeWorkGroupSizeZ == 0 || limits.MaxComputeWorkGroupInvocations == 0)
            throw new GraphicsValidationException(
                "The GLES device did not report complete local work-group size and invocation limits.");

        if (x > limits.MaxComputeWorkGroupSizeX || y > limits.MaxComputeWorkGroupSizeY ||
            z > limits.MaxComputeWorkGroupSizeZ)
            throw new GraphicsValidationException(
                $"Compute shader local size ({x}, {y}, {z}) exceeds the GLES per-axis limits " +
                $"({limits.MaxComputeWorkGroupSizeX}, {limits.MaxComputeWorkGroupSizeY}, {limits.MaxComputeWorkGroupSizeZ}).");

        ulong product = x;
        if (product > ulong.MaxValue / y)
            throw new GraphicsValidationException("Compute shader local work-group size overflows the invocation-count range.");
        product *= y;
        if (product > ulong.MaxValue / z)
            throw new GraphicsValidationException("Compute shader local work-group size overflows the invocation-count range.");
        product *= z;
        if (product > limits.MaxComputeWorkGroupInvocations)
            throw new GraphicsValidationException(
                $"Compute shader local work-group size contains {product} invocations; " +
                $"the GLES device limit is {limits.MaxComputeWorkGroupInvocations}.");
    }
}
