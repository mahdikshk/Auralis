namespace Auralis.Graphics.Resources;

/// <summary>Cross-backend texture and vertex formats. The enum does not imply device support.</summary>
public enum GraphicsFormat
{
    Unknown,
    R8UNorm,
    R8SNorm,
    R8UInt,
    R8SInt,
    RG8UNorm,
    RG8SNorm,
    RG8UInt,
    RG8SInt,
    RGBA8UNorm,
    RGBA8UNormSrgb,
    RGBA8SNorm,
    RGBA8UInt,
    RGBA8SInt,
    BGRA8UNorm,
    BGRA8UNormSrgb,
    R16Float,
    R16UInt,
    R16SInt,
    RG16Float,
    RG16UInt,
    RG16SInt,
    RGBA16Float,
    RGBA16UInt,
    RGBA16SInt,
    R32Float,
    R32UInt,
    R32SInt,
    RG32Float,
    RG32UInt,
    RG32SInt,
    RGB32Float,
    RGBA32Float,
    RGBA32UInt,
    RGBA32SInt,
    D16UNorm,
    D24UNormS8UInt,
    D32Float,
    D32FloatS8UInt,
    BC1UNorm,
    BC1UNormSrgb,
    BC3UNorm,
    BC3UNormSrgb,
    BC5UNorm,
    BC7UNorm,
    BC7UNormSrgb,
    ASTC4x4UNorm,
    ASTC4x4UNormSrgb
}

/// <summary>Operations that a device may support for a particular format.</summary>
[Flags]
public enum GraphicsFormatSupport
{
    None = 0,
    Sampled = 1 << 0,
    Storage = 1 << 1,
    ColorAttachment = 1 << 2,
    DepthStencilAttachment = 1 << 3,
    Blend = 1 << 4,
    VertexBuffer = 1 << 5,
    CopySource = 1 << 6,
    CopyDestination = 1 << 7,
    LinearFiltering = 1 << 8
}

/// <summary>Supported sample counts, represented as flags so a format can advertise multiple counts.</summary>
[Flags]
public enum GraphicsSampleCountFlags
{
    None = 0,
    One = 1 << 0,
    Two = 1 << 1,
    Four = 1 << 2,
    Eight = 1 << 3,
    Sixteen = 1 << 4,
    ThirtyTwo = 1 << 5,
    SixtyFour = 1 << 6
}

/// <summary>Backend-neutral resource creation hints that do not prescribe a native allocation model.</summary>
[Flags]
public enum GraphicsResourceCreationOptions
{
    None = 0,
    DedicatedAllocation = 1 << 0,
    AllowAliasing = 1 << 1,
    /// <summary>Allows views to reinterpret the texture using an explicitly compatible format.</summary>
    MutableFormat = 1 << 2
}

/// <summary>Texture resource dimensionality.</summary>
public enum GraphicsTextureDimension
{
    Texture1D,
    Texture2D,
    Texture3D,
    Cube
}

/// <summary>Intended uses of a texture. Actual format/usage support is device-dependent.</summary>
[Flags]
public enum GraphicsTextureUsage
{
    None = 0,
    Sampled = 1 << 0,
    Storage = 1 << 1,
    ColorAttachment = 1 << 2,
    DepthStencilAttachment = 1 << 3,
    CopySource = 1 << 4,
    CopyDestination = 1 << 5,
    /// <summary>Intended for presentation through a compatible presentation surface.</summary>
    Present = 1 << 6
}

/// <summary>How a buffer is intended to be accessed by the CPU and GPU.</summary>
public enum GraphicsMemoryAccess
{
    GpuOnly,
    CpuToGpu,
    GpuToCpu,
    CpuReadWrite
}

/// <summary>Memory placement preference. This is a hint, not a promise about physical heap topology.</summary>
public enum GraphicsMemoryPreference
{
    Default,
    DeviceLocal,
    HostVisible,
    HostCached
}

/// <summary>CPU access mode for a mapped buffer range.</summary>
public enum GraphicsMapMode
{
    /// <summary>Reads existing contents without modifying them.</summary>
    Read,

    /// <summary>
    /// Discard/write-only mapping. Previous contents of the mapped range are undefined.
    /// Initialize every byte that will be relied on; use ReadWrite when preserving old bytes.
    /// </summary>
    Write,

    /// <summary>Reads and writes the range while preserving its existing contents.</summary>
    ReadWrite
}

/// <summary>Intended buffer operations.</summary>
[Flags]
public enum GraphicsBufferUsage
{
    None = 0,
    Vertex = 1 << 0,
    Index = 1 << 1,
    Constant = 1 << 2,
    Storage = 1 << 3,
    IndirectArguments = 1 << 4,
    CopySource = 1 << 5,
    CopyDestination = 1 << 6
}
