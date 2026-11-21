using Auralis.Graphics.Resources;
using Silk.NET.OpenGL;

namespace Auralis.Graphics.Backends.OpenGL;

/// <summary>Pure sampler-state translations. Unsupported enum values fail rather than silently defaulting.</summary>
internal static class OpenGLSamplerMapping
{
    internal static TextureMagFilter ToMagFilter(GraphicsFilter filter) => filter switch
    {
        GraphicsFilter.Nearest => TextureMagFilter.Nearest,
        GraphicsFilter.Linear => TextureMagFilter.Linear,
        _ => throw new ArgumentOutOfRangeException(nameof(filter))
    };

    internal static TextureMinFilter ToMinFilter(GraphicsFilter min, GraphicsFilter mip) =>
        (min, mip) switch
        {
            (GraphicsFilter.Nearest, GraphicsFilter.Nearest) => TextureMinFilter.NearestMipmapNearest,
            (GraphicsFilter.Linear, GraphicsFilter.Nearest) => TextureMinFilter.LinearMipmapNearest,
            (GraphicsFilter.Nearest, GraphicsFilter.Linear) => TextureMinFilter.NearestMipmapLinear,
            (GraphicsFilter.Linear, GraphicsFilter.Linear) => TextureMinFilter.LinearMipmapLinear,
            _ => throw new ArgumentOutOfRangeException(nameof(min))
        };

    internal static TextureWrapMode ToAddressMode(GraphicsAddressMode mode) => mode switch
    {
        GraphicsAddressMode.Repeat => TextureWrapMode.Repeat,
        GraphicsAddressMode.MirroredRepeat => TextureWrapMode.MirroredRepeat,
        GraphicsAddressMode.ClampToEdge => TextureWrapMode.ClampToEdge,
        GraphicsAddressMode.ClampToBorder => TextureWrapMode.ClampToBorder,
        GraphicsAddressMode.MirrorClampToEdge => (TextureWrapMode)GLEnum.MirrorClampToEdge,
        _ => throw new ArgumentOutOfRangeException(nameof(mode))
    };

    internal static GLEnum ToCompareFunction(GraphicsCompareOperation operation) => operation switch
    {
        GraphicsCompareOperation.Never => GLEnum.Never,
        GraphicsCompareOperation.Less => GLEnum.Less,
        GraphicsCompareOperation.Equal => GLEnum.Equal,
        GraphicsCompareOperation.LessEqual => GLEnum.Lequal,
        GraphicsCompareOperation.Greater => GLEnum.Greater,
        GraphicsCompareOperation.NotEqual => GLEnum.Notequal,
        GraphicsCompareOperation.GreaterEqual => GLEnum.Gequal,
        GraphicsCompareOperation.Always => GLEnum.Always,
        _ => throw new ArgumentOutOfRangeException(nameof(operation))
    };
}
