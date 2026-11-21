using Auralis.Graphics.Abstractions;
using Auralis.Graphics.Pipelines;
using Auralis.Graphics.Resources;

namespace Auralis.Graphics.Devices;

/// <summary>Implementation-reported hardware limits. Zero means that a value is not reported.</summary>
public readonly record struct GraphicsDeviceLimits(
    ulong MaxBufferSizeInBytes,
    uint MaxTextureDimension1D,
    uint MaxTextureDimension2D,
    uint MaxTextureDimension3D,
    uint MaxTextureArrayLayers,
    uint MaxVertexInputAttributes,
    uint MaxVertexInputBindings,
    uint MaxBoundDescriptorSets,
    uint MaxComputeWorkGroupInvocations,
    uint MaxComputeWorkGroupSizeX,
    uint MaxComputeWorkGroupSizeY,
    uint MaxComputeWorkGroupSizeZ,
    float MaxSamplerAnisotropy,
    uint MaxTessellationPatchSize = 0,
    uint MaxColorAttachments = 0,
    uint MaxColorDrawBuffers = 0,
    uint MaxMeshWorkGroupCountX = 0,
    uint MaxMeshWorkGroupCountY = 0,
    uint MaxMeshWorkGroupCountZ = 0,
    uint MaxMeshWorkGroupTotalCount = 0,
    uint MaxTaskWorkGroupCountX = 0,
    uint MaxTaskWorkGroupCountY = 0,
    uint MaxTaskWorkGroupCountZ = 0,
    uint MaxTaskWorkGroupTotalCount = 0);

/// <summary>Describes the operations supported for a particular texture format.</summary>
public readonly record struct GraphicsFormatCapabilities(
    GraphicsFormat Format,
    GraphicsFormatSupport Support,
    GraphicsSampleCountFlags SampleCounts)
{
    public bool Supports(GraphicsFormatSupport support) =>
        support != GraphicsFormatSupport.None && (Support & support) == support;

    public bool SupportsSampleCount(uint sampleCount)
    {
        GraphicsSampleCountFlags flag = sampleCount switch
        {
            1 => GraphicsSampleCountFlags.One,
            2 => GraphicsSampleCountFlags.Two,
            4 => GraphicsSampleCountFlags.Four,
            8 => GraphicsSampleCountFlags.Eight,
            16 => GraphicsSampleCountFlags.Sixteen,
            32 => GraphicsSampleCountFlags.ThirtyTwo,
            64 => GraphicsSampleCountFlags.SixtyFour,
            _ => GraphicsSampleCountFlags.None
        };

        return flag != GraphicsSampleCountFlags.None && (SampleCounts & flag) != 0;
    }
}

/// <summary>
/// Immutable, backend-reported feature and limit snapshot. No capability is inferred from the backend name.
/// </summary>
public sealed class GraphicsDeviceCapabilities
{
    private readonly GraphicsFormatCapabilities[] _formats;

    public GraphicsFeatureSet Features { get; }

    public GraphicsQueueCapabilities QueueCapabilities { get; }

    public GraphicsDeviceLimits Limits { get; }

    public ReadOnlySpan<GraphicsFormatCapabilities> Formats => _formats;

    public GraphicsDeviceCapabilities(
        GraphicsFeatureSet features,
        GraphicsQueueCapabilities queueCapabilities,
        GraphicsDeviceLimits limits,
        ReadOnlySpan<GraphicsFormatCapabilities> formats)
    {
        const GraphicsFeature allFeatures =
            GraphicsFeature.ComputeShaders | GraphicsFeature.IndirectDrawing |
            GraphicsFeature.MultiDrawIndirect | GraphicsFeature.DescriptorIndexing |
            GraphicsFeature.SeparateComputeQueue | GraphicsFeature.SeparateTransferQueue |
            GraphicsFeature.TimestampQueries | GraphicsFeature.SamplerAnisotropy |
            GraphicsFeature.TessellationShaders | GraphicsFeature.GeometryShaders |
            GraphicsFeature.MeshShaders | GraphicsFeature.TaskShaders | GraphicsFeature.RayTracing |
            GraphicsFeature.VariableRateShading | GraphicsFeature.ConservativeRasterization |
            GraphicsFeature.TimelineSynchronization | GraphicsFeature.SparseResources |
            GraphicsFeature.MultiViewport | GraphicsFeature.DepthBoundsTest |
            GraphicsFeature.LogicOperations | GraphicsFeature.WideLines |
            GraphicsFeature.DepthClamp | GraphicsFeature.FillModeNonSolid |
            GraphicsFeature.IndependentBlend | GraphicsFeature.DepthBiasClamp |
            GraphicsFeature.SpirVShaders | GraphicsFeature.BaseVertexDrawing |
            GraphicsFeature.BaseInstanceDrawing | GraphicsFeature.TextureViews;
        const GraphicsQueueCapabilities allQueues =
            GraphicsQueueCapabilities.Graphics | GraphicsQueueCapabilities.Compute |
            GraphicsQueueCapabilities.Copy | GraphicsQueueCapabilities.Present;
        const GraphicsFormatSupport allFormatSupport =
            GraphicsFormatSupport.Sampled | GraphicsFormatSupport.Storage |
            GraphicsFormatSupport.ColorAttachment | GraphicsFormatSupport.DepthStencilAttachment |
            GraphicsFormatSupport.Blend | GraphicsFormatSupport.VertexBuffer |
            GraphicsFormatSupport.CopySource | GraphicsFormatSupport.CopyDestination |
            GraphicsFormatSupport.LinearFiltering;
        const GraphicsSampleCountFlags allSampleCounts =
            GraphicsSampleCountFlags.One | GraphicsSampleCountFlags.Two |
            GraphicsSampleCountFlags.Four | GraphicsSampleCountFlags.Eight |
            GraphicsSampleCountFlags.Sixteen | GraphicsSampleCountFlags.ThirtyTwo |
            GraphicsSampleCountFlags.SixtyFour;

        if ((features.Enabled & ~allFeatures) != 0)
            throw new ArgumentOutOfRangeException(nameof(features));
        if ((queueCapabilities & ~allQueues) != 0)
            throw new ArgumentOutOfRangeException(nameof(queueCapabilities));
        if (limits.MaxSamplerAnisotropy != 0 &&
            (!float.IsFinite(limits.MaxSamplerAnisotropy) || limits.MaxSamplerAnisotropy < 1))
            throw new ArgumentOutOfRangeException(nameof(limits));

        Features = features;
        QueueCapabilities = queueCapabilities;
        Limits = limits;
        _formats = formats.ToArray();

        for (int i = 0; i < _formats.Length; i++)
        {
            if (_formats[i].Format == GraphicsFormat.Unknown || !Enum.IsDefined(_formats[i].Format))
                throw new ArgumentException("Format capability entries must identify a concrete format.", nameof(formats));
            if ((_formats[i].Support & ~allFormatSupport) != 0 ||
                (_formats[i].SampleCounts & ~allSampleCounts) != 0)
                throw new ArgumentException("A format capability entry contains unknown flags.", nameof(formats));

            for (int j = 0; j < i; j++)
            {
                if (_formats[j].Format == _formats[i].Format)
                    throw new ArgumentException($"Format '{_formats[i].Format}' is listed more than once.", nameof(formats));
            }
        }
    }

    public bool TryGetFormatCapabilities(
        GraphicsFormat format,
        out GraphicsFormatCapabilities capabilities)
    {
        for (int i = 0; i < _formats.Length; i++)
        {
            if (_formats[i].Format == format)
            {
                capabilities = _formats[i];
                return true;
            }
        }

        capabilities = default;
        return false;
    }

    public bool SupportsFormat(GraphicsFormat format, GraphicsFormatSupport support) =>
        TryGetFormatCapabilities(format, out GraphicsFormatCapabilities capabilities) &&
        capabilities.Supports(support);

    public bool SupportsSampleCount(GraphicsFormat format, uint sampleCount) =>
        TryGetFormatCapabilities(format, out GraphicsFormatCapabilities capabilities) &&
        capabilities.SupportsSampleCount(sampleCount);

    /// <summary>Validates a buffer descriptor against structural rules and the limits reported by this device.</summary>
    public void ValidateBufferDescription(in GraphicsBufferDescription description)
    {
        description.Validate();

        if (Limits.MaxBufferSizeInBytes != 0 &&
            description.SizeInBytes > Limits.MaxBufferSizeInBytes)
            throw new GraphicsException(
                $"Buffer size {description.SizeInBytes} exceeds the device limit of {Limits.MaxBufferSizeInBytes} bytes.");
    }

    /// <summary>Validates texture structure, implementation limits, and reported format/usage/sample support.</summary>
    public void ValidateTextureDescription(in GraphicsTextureDescription description)
    {
        description.Validate();

        ValidateTextureDimensions(description);

        if (!TryGetFormatCapabilities(description.Format, out GraphicsFormatCapabilities format))
            throw new GraphicsException($"The device did not report support information for format {description.Format}.");

        GraphicsFormatSupport required = GetRequiredFormatSupport(description.Usage);
        if (required != GraphicsFormatSupport.None && !format.Supports(required))
            throw new GraphicsException($"Format {description.Format} does not support all requested texture usages: {required}.");
        if (!format.SupportsSampleCount(description.SampleCount))
            throw new GraphicsException($"Format {description.Format} does not support {description.SampleCount} sample(s).");
    }

    /// <summary>
    /// Validates a texture view against its parent texture, mutable-format compatibility rules,
    /// device limits, and the capabilities reported for the effective view format.
    /// </summary>
    public void ValidateTextureViewDescription(
        in GraphicsTextureDescription texture,
        in GraphicsTextureViewDescription view)
    {
        ValidateTextureDescription(texture);
        view.Validate(texture);

        GraphicsFormat viewFormat = view.Format == GraphicsFormat.Unknown ? texture.Format : view.Format;
        if (!TryGetFormatCapabilities(viewFormat, out GraphicsFormatCapabilities format))
            throw new GraphicsException($"The device did not report support information for texture-view format {viewFormat}.");

        GraphicsFormatSupport required = GetRequiredFormatSupport(texture.Usage);
        if (required != GraphicsFormatSupport.None && !format.Supports(required))
            throw new GraphicsException(
                $"Texture-view format {viewFormat} does not support the parent texture usages: {required}.");
        if (!format.SupportsSampleCount(texture.SampleCount))
            throw new GraphicsException(
                $"Texture-view format {viewFormat} does not support {texture.SampleCount} sample(s).");
    }

    /// <summary>Validates vertex input counts and every attribute format against this device's limits.</summary>
    public void ValidateVertexInputLayout(GraphicsVertexInputLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        layout.Validate();

        if (Limits.MaxVertexInputBindings != 0 &&
            (uint)layout.Buffers.Length > Limits.MaxVertexInputBindings)
            throw new GraphicsException(
                $"Vertex input uses {layout.Buffers.Length} bindings; the device limit is {Limits.MaxVertexInputBindings}.");

        if (Limits.MaxVertexInputAttributes != 0 &&
            (uint)layout.Attributes.Length > Limits.MaxVertexInputAttributes)
            throw new GraphicsException(
                $"Vertex input uses {layout.Attributes.Length} attributes; the device limit is {Limits.MaxVertexInputAttributes}.");

        ReadOnlySpan<GraphicsVertexAttribute> attributes = layout.Attributes;
        for (int i = 0; i < attributes.Length; i++)
        {
            GraphicsFormat format = attributes[i].Format;
            if (!SupportsFormat(format, GraphicsFormatSupport.VertexBuffer))
                throw new GraphicsException(
                    $"The device does not support {format} as a vertex-buffer attribute format.");
        }
    }

    private void ValidateTextureDimensions(in GraphicsTextureDescription description)
    {
        switch (description.Dimension)
        {
            case GraphicsTextureDimension.Texture1D:
                ValidateDimension(description.Width, Limits.MaxTextureDimension1D, "1D texture width");
                break;
            case GraphicsTextureDimension.Texture2D:
            case GraphicsTextureDimension.Cube:
                ValidateDimension(description.Width, Limits.MaxTextureDimension2D, "2D texture width");
                ValidateDimension(description.Height, Limits.MaxTextureDimension2D, "2D texture height");
                break;
            case GraphicsTextureDimension.Texture3D:
                ValidateDimension(description.Width, Limits.MaxTextureDimension3D, "3D texture width");
                ValidateDimension(description.Height, Limits.MaxTextureDimension3D, "3D texture height");
                ValidateDimension(description.Depth, Limits.MaxTextureDimension3D, "3D texture depth");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(description));
        }

        ValidateDimension(description.ArrayLayers, Limits.MaxTextureArrayLayers, "texture array layers");
    }

    private static void ValidateDimension(uint requested, uint limit, string name)
    {
        if (limit != 0 && requested > limit)
            throw new GraphicsException($"Requested {name} ({requested}) exceeds the device limit ({limit}).");
    }

    private static GraphicsFormatSupport GetRequiredFormatSupport(GraphicsTextureUsage usage)
    {
        GraphicsFormatSupport required = GraphicsFormatSupport.None;
        if ((usage & GraphicsTextureUsage.Sampled) != 0) required |= GraphicsFormatSupport.Sampled;
        if ((usage & GraphicsTextureUsage.Storage) != 0) required |= GraphicsFormatSupport.Storage;
        if ((usage & GraphicsTextureUsage.ColorAttachment) != 0) required |= GraphicsFormatSupport.ColorAttachment;
        if ((usage & GraphicsTextureUsage.DepthStencilAttachment) != 0) required |= GraphicsFormatSupport.DepthStencilAttachment;
        if ((usage & GraphicsTextureUsage.CopySource) != 0) required |= GraphicsFormatSupport.CopySource;
        if ((usage & GraphicsTextureUsage.CopyDestination) != 0) required |= GraphicsFormatSupport.CopyDestination;
        // Presentation compatibility is surface/queue-specific, not a per-format capability.
        return required;
    }

    public void ValidateSamplerDescription(in GraphicsSamplerDescription description)
    {
        description.Validate();

        if (description.MaxAnisotropy > 1)
        {
            if (!Features.Supports(GraphicsFeature.SamplerAnisotropy))
                throw new GraphicsException("Sampler anisotropy is not supported by this device.");
            if (Limits.MaxSamplerAnisotropy > 0 && description.MaxAnisotropy > Limits.MaxSamplerAnisotropy)
                throw new GraphicsException($"Requested anisotropy exceeds the device limit of {Limits.MaxSamplerAnisotropy}.");
        }
    }
}
