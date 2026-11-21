using Auralis.Graphics.Resources;
using Silk.NET.OpenGL;

namespace Auralis.Graphics.Backends.OpenGL;

/// <summary>Pure mapping between shared texture descriptions and desktop OpenGL texture targets.</summary>
internal static class OpenGLTextureTargetMapping
{
    internal static TextureTarget ForTexture(in GraphicsTextureDescription description)
    {
        if (description.SampleCount > 1)
            return description.ArrayLayers > 1
                ? TextureTarget.Texture2DMultisampleArray
                : TextureTarget.Texture2DMultisample;

        return description.Dimension switch
        {
            GraphicsTextureDimension.Texture1D =>
                description.ArrayLayers > 1 ? TextureTarget.Texture1DArray : TextureTarget.Texture1D,
            GraphicsTextureDimension.Texture2D =>
                description.ArrayLayers > 1 ? TextureTarget.Texture2DArray : TextureTarget.Texture2D,
            GraphicsTextureDimension.Texture3D => TextureTarget.Texture3D,
            GraphicsTextureDimension.Cube =>
                description.ArrayLayers > 6 ? TextureTarget.TextureCubeMapArray : TextureTarget.TextureCubeMap,
            _ => throw new ArgumentOutOfRangeException(nameof(description))
        };
    }

    internal static TextureTarget ForView(GraphicsTextureViewType viewType) => viewType switch
    {
        GraphicsTextureViewType.Texture1D => TextureTarget.Texture1D,
        GraphicsTextureViewType.Texture1DArray => TextureTarget.Texture1DArray,
        GraphicsTextureViewType.Texture2D => TextureTarget.Texture2D,
        GraphicsTextureViewType.Texture2DArray => TextureTarget.Texture2DArray,
        GraphicsTextureViewType.Texture2DMultisample => TextureTarget.Texture2DMultisample,
        GraphicsTextureViewType.Texture2DMultisampleArray => TextureTarget.Texture2DMultisampleArray,
        GraphicsTextureViewType.Texture3D => TextureTarget.Texture3D,
        GraphicsTextureViewType.Cube => TextureTarget.TextureCubeMap,
        GraphicsTextureViewType.CubeArray => TextureTarget.TextureCubeMapArray,
        _ => throw new ArgumentOutOfRangeException(nameof(viewType))
    };
}
