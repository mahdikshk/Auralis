using Auralis.Graphics.Abstractions;
using Auralis.Graphics.Resources;
using Silk.NET.OpenGLES;

namespace Auralis.Graphics.Backends.OpenGLES;

/// <summary>Pure GLES texture compatibility validation, independent of a live driver/context.</summary>
internal static class OpenGLESTextureCompatibility
{
    internal static TextureTarget ValidateAndGetTarget(
        in GraphicsTextureDescription description,
        OpenGLESVersion version = default,
        bool supportsCubeMapArray = false)
    {
        description.Validate();

        if ((description.CreationOptions & GraphicsResourceCreationOptions.AllowAliasing) != 0)
            throw new GraphicsValidationException("OpenGL ES does not expose resource aliasing through this backend.");

        if (description.SampleCount > 1)
        {
            if (!version.IsAtLeast(3, 1))
                throw new GraphicsValidationException(
                    "Multisample texture storage requires OpenGL ES 3.1 or newer.");
            if (description.Dimension != GraphicsTextureDimension.Texture2D ||
                description.ArrayLayers != 1 ||
                description.MipLevels != 1)
            {
                throw new GraphicsValidationException(
                    "GLES core multisample texture storage currently supports only single-layer, single-mip 2D textures.");
            }
            return TextureTarget.Texture2DMultisample;
        }
        if (description.Dimension == GraphicsTextureDimension.Texture1D)
            throw new GraphicsValidationException("OpenGL ES has no 1D texture target.");
        if (description.Dimension == GraphicsTextureDimension.Cube &&
            (description.ArrayLayers < 6 || description.ArrayLayers % 6 != 0))
            throw new GraphicsValidationException("Cube-map textures require a multiple of six layers.");
        if (description.Dimension == GraphicsTextureDimension.Cube &&
            description.ArrayLayers > 6 &&
            !supportsCubeMapArray &&
            !version.IsAtLeast(3, 2))
            throw new GraphicsValidationException(
                "Cube-map arrays require OpenGL ES 3.2 or GL_OES_texture_cube_map_array.");
        _ = OpenGLESGraphicsTexture.GetInternalFormat(description.Format);
        return OpenGLESGraphicsTexture.GetTextureTarget(description);
    }

    internal static void ValidateCopy(
        in GraphicsTextureDescription source,
        in GraphicsTextureDescription destination,
        bool supportsImageCopy = false)
    {
        source.Validate();
        destination.Validate();

        if ((source.Usage & GraphicsTextureUsage.CopySource) == 0 ||
            (destination.Usage & GraphicsTextureUsage.CopyDestination) == 0)
        {
            throw new GraphicsValidationException(
                "Texture copies require CopySource usage on the source and CopyDestination usage on the destination.");
        }

        if (source.Dimension != destination.Dimension ||
            source.Width != destination.Width ||
            source.Height != destination.Height ||
            source.Depth != destination.Depth ||
            source.ArrayLayers != destination.ArrayLayers ||
            source.MipLevels != destination.MipLevels ||
            source.SampleCount != destination.SampleCount ||
            source.Format != destination.Format)
        {
            throw new GraphicsValidationException(
                "A full-resource GLES texture copy requires matching dimensions, extents, depth/layers, mip counts, sample counts and formats.");
        }

        if (supportsImageCopy)
            return;

        // The blit fallback has its own stricter requirements. In particular, it really does
        // require framebuffer attachment usage and cannot copy arbitrary subresources.
        if ((source.Usage & GraphicsTextureUsage.ColorAttachment) == 0 ||
            (destination.Usage & GraphicsTextureUsage.ColorAttachment) == 0)
        {
            throw new GraphicsValidationException(
                "The GLES framebuffer-blit copy fallback requires ColorAttachment usage on both textures.");
        }

        if (source.Dimension != GraphicsTextureDimension.Texture2D ||
            source.SampleCount != 1 ||
            source.ArrayLayers != 1 ||
            source.MipLevels != 1)
        {
            throw new GraphicsValidationException(
                "The GLES framebuffer-blit copy fallback supports only single-sample, single-layer, single-mip 2D textures.");
        }

        if (source.Format is not (GraphicsFormat.RGBA8UNorm or GraphicsFormat.RGBA8UNormSrgb))
            throw new GraphicsValidationException(
                $"The GLES framebuffer-blit copy fallback does not support format {source.Format}.");
    }
}
