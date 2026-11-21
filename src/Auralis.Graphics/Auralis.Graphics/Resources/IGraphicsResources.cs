using Auralis.Graphics.Abstractions;

namespace Auralis.Graphics.Resources;

/// <summary>Base interface for GPU resources owned by a graphics device.</summary>
public interface IGraphicsResource : IGraphicsDeviceObject
{
    string? DebugName { get; }
}

public interface IGraphicsBuffer : IGraphicsResource
{
    GraphicsBufferDescription Description { get; }

    /// <summary>
    /// Maps a CPU-visible buffer range. The returned memory is valid only until Unmap is called.
    /// <see cref="GraphicsMapMode.Read"/> and <see cref="GraphicsMapMode.ReadWrite"/> preserve and
    /// expose existing contents. <see cref="GraphicsMapMode.Write"/> is discard/write-only: previous
    /// contents of the mapped range are undefined, and callers must initialize every byte they rely
    /// on. Use ReadWrite or another read-preserving strategy for partial updates that need old bytes.
    /// A <see cref="MemoryHandle"/> pin must be released before Unmap or resource disposal can finish.
    /// Previously acquired Span values must not be used after Unmap or disposal. Mappings are transient,
    /// not persistent allocations. Mapping GPU-only memory is an error; backends perform required cache
    /// maintenance.
    /// </summary>
    Memory<byte> Map(GraphicsMapMode mode, ulong offset = 0, ulong size = ulong.MaxValue);

    void Unmap();
}

public interface IGraphicsTexture : IGraphicsResource
{
    GraphicsTextureDescription Description { get; }

    IGraphicsTextureView CreateView(in GraphicsTextureViewDescription description);
}

public interface IGraphicsTextureView : IGraphicsResource
{
    IGraphicsTexture Texture { get; }

    GraphicsTextureViewDescription Description { get; }
}

public interface IGraphicsSampler : IGraphicsResource
{
    GraphicsSamplerDescription Description { get; }
}

public interface IGraphicsShader : IGraphicsResource
{
    GraphicsShaderDescription Description { get; }
}
