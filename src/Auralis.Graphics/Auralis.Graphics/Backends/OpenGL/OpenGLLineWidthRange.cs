using Auralis.Graphics.Abstractions;

namespace Auralis.Graphics.Backends.OpenGL;

/// <summary>Driver-reported aliased line-width limits queried from GL_ALIASED_LINE_WIDTH_RANGE.</summary>
internal readonly record struct OpenGLLineWidthRange(float Minimum, float Maximum)
{
    internal static OpenGLLineWidthRange FromNative(float minimum, float maximum)
    {
        if (!float.IsFinite(minimum) || !float.IsFinite(maximum) ||
            minimum <= 0 || maximum < minimum)
            throw new GraphicsException(
                $"The OpenGL driver returned an invalid aliased line-width range [{minimum}, {maximum}].");

        return new OpenGLLineWidthRange(minimum, maximum);
    }

    internal bool SupportsWideLines => Minimum < 1f || Maximum > 1f;

    internal bool Contains(float width) =>
        float.IsFinite(width) && width >= Minimum && width <= Maximum;
}
