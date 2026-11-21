using Auralis.Graphics.Resources;
using Silk.NET.OpenGL;

namespace Auralis.Graphics.Backends.OpenGL;

/// <summary>
/// Maps backend-neutral CPU access intent to immutable buffer-storage map permissions.
/// MemoryPreference remains a placement hint because OpenGL does not expose D3D12/Vulkan heaps.
/// </summary>
internal static class OpenGLBufferStorageMapping
{
    internal static BufferStorageMask GetStorageFlags(GraphicsBufferDescription description)
    {
        BufferStorageMask flags = 0;
        if (description.MemoryAccess is GraphicsMemoryAccess.GpuToCpu or GraphicsMemoryAccess.CpuReadWrite)
            flags |= BufferStorageMask.MapReadBit;
        if (description.MemoryAccess is GraphicsMemoryAccess.CpuToGpu or GraphicsMemoryAccess.CpuReadWrite)
            flags |= BufferStorageMask.MapWriteBit;
        return flags;
    }
}
