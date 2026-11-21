namespace Auralis.Graphics.Abstractions;

/// <summary>Identifies a graphics API backend supported by the common graphics layer.</summary>
public enum GraphicsBackend
{
    Direct3D12,
    Vulkan,
    Metal,
    OpenGL,
    OpenGLES
}

/// <summary>Broad hardware class reported by a graphics adapter.</summary>
public enum GraphicsAdapterType
{
    Unknown,
    Integrated,
    Discrete,
    Virtual,
    Cpu
}

/// <summary>Opaque adapter identifier. Its interpretation is private to the backend that produced it.</summary>
public readonly record struct GraphicsAdapterId(ulong High, ulong Low)
{
    public bool IsDefault => High == 0 && Low == 0;
}

/// <summary>Describes an adapter discovered by a backend.</summary>
public readonly record struct GraphicsAdapterInfo(
    GraphicsAdapterId Id,
    string Name,
    GraphicsAdapterType Type,
    uint VendorId,
    uint DeviceId,
    ulong DedicatedMemoryBytes);

/// <summary>Describes the selected device and the driver/API versions reported by its backend.</summary>
public readonly record struct GraphicsDeviceInfo(
    string Name,
    string DriverName,
    string DriverVersion,
    string ApiVersion,
    uint VendorId,
    uint DeviceId,
    bool IsSoftware);

/// <summary>Optional device features. These flags describe semantics, not native API feature bits.</summary>
[Flags]
public enum GraphicsFeature : ulong
{
    None = 0,
    ComputeShaders = 1UL << 0,
    IndirectDrawing = 1UL << 1,
    MultiDrawIndirect = 1UL << 2,
    DescriptorIndexing = 1UL << 3,
    SeparateComputeQueue = 1UL << 4,
    SeparateTransferQueue = 1UL << 5,
    TimestampQueries = 1UL << 6,
    SamplerAnisotropy = 1UL << 7,
    TessellationShaders = 1UL << 8,
    GeometryShaders = 1UL << 9,
    MeshShaders = 1UL << 10,
    RayTracing = 1UL << 11,
    VariableRateShading = 1UL << 12,
    ConservativeRasterization = 1UL << 13,
    TimelineSynchronization = 1UL << 14,
    SparseResources = 1UL << 15,
    MultiViewport = 1UL << 16,
    DepthBoundsTest = 1UL << 17,
    LogicOperations = 1UL << 18,
    WideLines = 1UL << 19,
    DepthClamp = 1UL << 20,
    FillModeNonSolid = 1UL << 21,
    TaskShaders = 1UL << 22,
    IndependentBlend = 1UL << 23,
    DepthBiasClamp = 1UL << 24,
    SpirVShaders = 1UL << 25,
    BaseVertexDrawing = 1UL << 26,
    BaseInstanceDrawing = 1UL << 27,
    TextureViews = 1UL << 28
}

/// <summary>A strongly typed set of optional graphics features reported by a device.</summary>
public readonly record struct GraphicsFeatureSet(GraphicsFeature Enabled)
{
    public bool Supports(GraphicsFeature feature) =>
        feature != GraphicsFeature.None && (Enabled & feature) == feature;

    public bool ContainsAll(GraphicsFeature features) => (Enabled & features) == features;

    public GraphicsFeature MissingFrom(GraphicsFeature requested) => requested & ~Enabled;
}

/// <summary>Logical role used to request a command-capable queue.</summary>
public enum GraphicsQueueType
{
    Graphics,
    Compute,
    Copy
}

/// <summary>
/// Coarse device-level queue capabilities. Present indicates that the device may expose presentation
/// queues, but it never proves compatibility with a particular native surface. Use
/// IGraphicsPresentationSurface.SupportsPresentation for the exact queue/surface pair.
/// </summary>
[Flags]
public enum GraphicsQueueCapabilities
{
    None = 0,
    Graphics = 1 << 0,
    Compute = 1 << 1,
    Copy = 1 << 2,
    /// <summary>At least one queue may support presentation; surface compatibility must be queried separately.</summary>
    Present = 1 << 3
}
