using Auralis.Graphics.Resources;
using Auralis.Graphics.Synchronization;
using Silk.NET.OpenGL;

namespace Auralis.Graphics.Backends.OpenGL;

/// <summary>
/// Translates common destination access intents to the GL visibility bits required by subsequent
/// consumers. OpenGL has no native texture-layout transition; layouts are validated semantic hints only.
/// </summary>
internal static class OpenGLMemoryBarrierTranslator
{
    internal static MemoryBarrierMask ToMask(in GraphicsResourceBarrier barrier)
    {
        GraphicsResourceAccess access = barrier.DestinationAccess;
        if (access == GraphicsResourceAccess.None)
            return 0;

        if ((access & (GraphicsResourceAccess.MemoryRead | GraphicsResourceAccess.MemoryWrite)) != 0)
            return MemoryBarrierMask.AllBarrierBits;

        MemoryBarrierMask mask = 0;
        IGraphicsResource resource = barrier.Resource;
        bool isBuffer = resource is IGraphicsBuffer;
        GraphicsTextureUsage textureUsage = resource switch
        {
            IGraphicsTexture texture => texture.Description.Usage,
            IGraphicsTextureView view => view.Texture.Description.Usage,
            _ => GraphicsTextureUsage.None
        };

        if ((access & GraphicsResourceAccess.IndirectRead) != 0)
            mask |= MemoryBarrierMask.CommandBarrierBit;
        if ((access & GraphicsResourceAccess.IndexRead) != 0)
            mask |= MemoryBarrierMask.ElementArrayBarrierBit;
        if ((access & GraphicsResourceAccess.VertexRead) != 0)
            mask |= MemoryBarrierMask.VertexAttribArrayBarrierBit;
        if ((access & GraphicsResourceAccess.UniformRead) != 0)
            mask |= MemoryBarrierMask.UniformBarrierBit;

        if ((access & GraphicsResourceAccess.ShaderRead) != 0)
        {
            if (isBuffer)
                mask |= MemoryBarrierMask.ShaderStorageBarrierBit;
            if ((textureUsage & GraphicsTextureUsage.Sampled) != 0)
                mask |= MemoryBarrierMask.TextureFetchBarrierBit;
            if ((textureUsage & GraphicsTextureUsage.Storage) != 0)
                mask |= MemoryBarrierMask.ShaderImageAccessBarrierBit;
        }

        if ((access & GraphicsResourceAccess.ShaderWrite) != 0)
        {
            mask |= isBuffer
                ? MemoryBarrierMask.ShaderStorageBarrierBit
                : MemoryBarrierMask.ShaderImageAccessBarrierBit;
        }

        if ((access & (GraphicsResourceAccess.ColorAttachmentRead |
                       GraphicsResourceAccess.ColorAttachmentWrite |
                       GraphicsResourceAccess.DepthStencilRead |
                       GraphicsResourceAccess.DepthStencilWrite)) != 0)
        {
            mask |= MemoryBarrierMask.FramebufferBarrierBit;
        }

        if ((access & (GraphicsResourceAccess.TransferRead | GraphicsResourceAccess.TransferWrite)) != 0)
        {
            mask |= isBuffer
                ? MemoryBarrierMask.BufferUpdateBarrierBit
                : MemoryBarrierMask.TextureUpdateBarrierBit;
        }

        // CPU mappings in this backend use staging memory and explicit BufferSubData/GetBufferSubData.
        // They are not persistent GL mappings, so ClientMappedBufferBarrierBit is not applicable.

        return mask;
    }
}
