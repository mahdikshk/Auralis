using Auralis.Graphics.Abstractions;

namespace Auralis.Graphics.Devices;

/// <summary>Debugging and validation facilities requested during device creation.</summary>
[Flags]
public enum GraphicsDebugOptions
{
    None = 0,
    EnableValidation = 1 << 0,
    EnableDebugLayer = 1 << 1,
    EnableGpuBasedValidation = 1 << 2
}

/// <summary>Options for creating a device. No presentation surface or window is required.</summary>
public sealed record GraphicsDeviceOptions
{
    /// <summary>When null, registered available providers are tried in registration order.</summary>
    public GraphicsBackend? Backend { get; init; }

    /// <summary>Optional opaque adapter identifier returned by the selected provider.</summary>
    public GraphicsAdapterId? Adapter { get; init; }

    /// <summary>Optional features that the resulting device must support.</summary>
    public GraphicsFeature RequiredFeatures { get; init; }

    /// <summary>Queue types that the resulting device must expose.</summary>
    public GraphicsQueueCapabilities RequiredQueues { get; init; } = GraphicsQueueCapabilities.Graphics;

    public GraphicsDebugOptions DebugOptions { get; init; }

    public string? ApplicationName { get; init; }

    public void Validate()
    {
        const GraphicsFeature allFeatures =
            GraphicsFeature.ComputeShaders | GraphicsFeature.IndirectDrawing |
            GraphicsFeature.MultiDrawIndirect | GraphicsFeature.DescriptorIndexing |
            GraphicsFeature.SeparateComputeQueue | GraphicsFeature.SeparateTransferQueue |
            GraphicsFeature.TimestampQueries | GraphicsFeature.SamplerAnisotropy |
            GraphicsFeature.TessellationShaders | GraphicsFeature.GeometryShaders |
            GraphicsFeature.MeshShaders | GraphicsFeature.RayTracing |
            GraphicsFeature.VariableRateShading | GraphicsFeature.ConservativeRasterization |
            GraphicsFeature.TimelineSynchronization | GraphicsFeature.SparseResources |
            GraphicsFeature.MultiViewport | GraphicsFeature.DepthBoundsTest |
            GraphicsFeature.LogicOperations | GraphicsFeature.WideLines |
            GraphicsFeature.DepthClamp | GraphicsFeature.FillModeNonSolid |
            GraphicsFeature.TaskShaders | GraphicsFeature.IndependentBlend |
            GraphicsFeature.DepthBiasClamp | GraphicsFeature.SpirVShaders |
            GraphicsFeature.BaseVertexDrawing | GraphicsFeature.BaseInstanceDrawing |
            GraphicsFeature.TextureViews;

        const GraphicsQueueCapabilities allQueues =
            GraphicsQueueCapabilities.Graphics | GraphicsQueueCapabilities.Compute |
            GraphicsQueueCapabilities.Copy | GraphicsQueueCapabilities.Present;

        const GraphicsDebugOptions allDebugOptions =
            GraphicsDebugOptions.EnableValidation | GraphicsDebugOptions.EnableDebugLayer |
            GraphicsDebugOptions.EnableGpuBasedValidation;

        if (Backend is { } backend && !Enum.IsDefined(backend))
            throw new ArgumentOutOfRangeException(nameof(Backend));

        if (Adapter is not null && Backend is null)
            throw new ArgumentException("An adapter identifier is backend-specific; specify Backend when selecting an adapter.", nameof(Adapter));

        if ((RequiredFeatures & ~allFeatures) != 0)
            throw new ArgumentOutOfRangeException(nameof(RequiredFeatures));

        if ((RequiredQueues & ~allQueues) != 0)
            throw new ArgumentOutOfRangeException(nameof(RequiredQueues));

        if ((DebugOptions & ~allDebugOptions) != 0)
            throw new ArgumentOutOfRangeException(nameof(DebugOptions));
    }
}
