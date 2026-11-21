using System.Runtime.InteropServices;
using Auralis.Graphics.Abstractions;
using Auralis.Graphics.Commands;
using Auralis.Graphics.Devices;
using Auralis.Graphics.Pipelines;
using Auralis.Graphics.Resources;
using Auralis.Graphics.Synchronization;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ARB;
using Silk.NET.OpenGL.Extensions.EXT;
using Silk.NET.OpenGL.Extensions.NV;

namespace Auralis.Graphics.Backends.OpenGL;

internal enum OpenGLCommandKind : byte
{
    MemoryBarrier,
    CopyBuffer,
    CopyTexture,
    SetRenderTargets,
    SetViewport,
    SetScissor,
    ClearColor,
    ClearColorUInt,
    ClearColorSInt,
    ClearDepthStencil,
    SetRenderPipeline,
    SetComputePipeline,
    SetBindingSet,
    SetVertexBuffer,
    SetIndexBuffer,
    Draw,
    DrawIndexed,
    DrawIndirect,
    DrawIndexedIndirect,
    DrawMeshTasks,
    DrawMeshTasksIndirect,
    Dispatch,
    DispatchIndirect
}

internal struct OpenGLRecordedCommand
{
    internal OpenGLCommandKind Kind;
    internal MemoryBarrierMask BarrierMask;
    internal IGraphicsBuffer? SourceBuffer;
    internal IGraphicsBuffer? DestinationBuffer;
    internal IGraphicsTexture? SourceTexture;
    internal IGraphicsTexture? DestinationTexture;
    internal ulong SourceOffset;
    internal ulong DestinationOffset;
    internal ulong SizeInBytes;
    internal int RenderTargetViewOffset;
    internal int RenderTargetColorCount;
    internal IGraphicsTextureView? RenderTargetDepthStencil;
    internal GraphicsViewport Viewport;
    internal GraphicsScissorRect Scissor;
    internal uint AttachmentIndex;
    internal GraphicsClearColor ClearColor;
    internal GraphicsClearColorUInt ClearColorUInt;
    internal GraphicsClearColorSInt ClearColorSInt;
    internal float ClearDepth;
    internal uint ClearStencil;
    internal bool ClearDepthEnabled;
    internal bool ClearStencilEnabled;
    internal OpenGLGraphicsRenderPipeline? RenderPipeline;
    internal OpenGLGraphicsComputePipeline? ComputePipeline;
    internal OpenGLGraphicsBindingLayout? BindingLayout;
    internal int BindingValueOffset;
    internal int BindingValueCount;
    internal uint SetIndex;
    internal uint VertexBinding;
    internal IGraphicsBuffer? VertexBuffer;
    internal IGraphicsBuffer? IndexBuffer;
    internal ulong BufferOffset;
    internal GraphicsIndexFormat IndexFormat;
    internal uint VertexCount;
    internal uint InstanceCount;
    internal uint FirstVertex;
    internal uint FirstInstance;
    internal uint IndexCount;
    internal uint FirstIndex;
    internal int BaseVertex;
    internal uint DrawCount;
    internal uint Stride;
    internal uint GroupCountX;
    internal uint GroupCountY;
    internal uint GroupCountZ;
}

internal readonly record struct OpenGLVertexBufferBinding(
    IGraphicsBuffer Buffer,
    ulong Offset,
    uint Stride,
    GraphicsVertexInputRate InputRate);

/// <summary>
/// CPU-side deferred command recorder. Recording does not make the GL context current and never calls GL.
/// Commands retain their referenced resources until the list is reset or disposed.
/// </summary>
internal sealed class OpenGLGraphicsCommandList : GraphicsDeviceObject, IGraphicsCommandList
{
    private readonly OpenGLGraphicsCommandQueue _queue;
    private readonly GraphicsCommandListLifecycle _lifecycle;
    private readonly List<OpenGLRecordedCommand> _commands = new(64);
    private readonly List<IGraphicsDeviceObject> _barrierResourceReferences = new(16);
    private readonly List<IGraphicsTextureView> _renderTargetViews = new(8);
    private readonly List<OpenGLBindingValue> _bindingSnapshotValues = new(32);
    private readonly OpenGLResourceRetention _resourceRetention = new();
    private int _disposedCleanup;
    private uint _recordedColorTargetCount;
    private int _recordedTargetViewOffset;
    private GraphicsRenderTargetExtent _recordedTargetExtent;
    private bool _hasRecordedTargets;
    private bool _recordedHasDepthStencil;
    private GraphicsFormat _renderTargetsDepthFormat;
    private OpenGLGraphicsRenderPipeline? _recordedPipeline;
    private OpenGLGraphicsComputePipeline? _recordedComputePipeline;
    private OpenGLGraphicsPipelineLayout? _recordedPipelineLayout;
    private readonly Dictionary<uint, OpenGLVertexBufferBinding> _recordedVertexBuffers = new();
    private readonly HashSet<uint> _recordedBoundSets = [];
    private bool _hasRecordedViewport;
    private IGraphicsBuffer? _recordedIndexBuffer;
    private GraphicsIndexFormat _recordedIndexFormat;
    private ulong _recordedIndexOffset;

    internal OpenGLGraphicsCommandList(
        OpenGLGraphicsCommandQueue queue,
        GraphicsCommandListUsage usage) : base(queue.Device)
    {
        _queue = queue;
        Usage = usage;
        _lifecycle = new GraphicsCommandListLifecycle(queue.QueueType, usage);
    }

    public GraphicsQueueType QueueType => _queue.QueueType;
    public GraphicsCommandListUsage Usage { get; }
    public GraphicsCommandListState State
    {
        get
        {
            _queue.PollPending();
            return _lifecycle.State;
        }
    }

    internal ReadOnlySpan<OpenGLRecordedCommand> Commands => CollectionsMarshal.AsSpan(_commands);
    internal GraphicsCommandListLifecycle Lifecycle => _lifecycle;

    public void Begin()
    {
        ThrowIfDisposed();
        _lifecycle.Begin();
    }

    public void End()
    {
        ThrowIfDisposed();
        _lifecycle.End();
    }

    public void Reset()
    {
        ThrowIfDisposed();
        _lifecycle.Reset();
        ClearRecordedState();
    }

    private void ClearRecordedState()
    {
        _commands.Clear();
        _barrierResourceReferences.Clear();
        _renderTargetViews.Clear();
        _bindingSnapshotValues.Clear();
        _recordedColorTargetCount = 0;
        _recordedTargetViewOffset = 0;
        _recordedTargetExtent = default;
        _hasRecordedTargets = false;
        _recordedHasDepthStencil = false;
        _renderTargetsDepthFormat = GraphicsFormat.Unknown;
        _recordedPipeline = null;
        _recordedComputePipeline = null;
        _recordedPipelineLayout = null;
        _recordedVertexBuffers.Clear();
        _recordedBoundSets.Clear();
        _hasRecordedViewport = false;
        _recordedIndexBuffer = null;
        _recordedIndexOffset = 0;
        _resourceRetention.ReleaseAll();
    }

    public void MemoryBarrier(ReadOnlySpan<GraphicsResourceBarrier> barriers)
    {
        EnsureRecording();
        if (barriers.IsEmpty)
            return;

        MemoryBarrierMask mask = 0;
        for (int i = 0; i < barriers.Length; i++)
        {
            GraphicsResourceBarrier barrier = barriers[i];
            barrier.Validate();
            EnsureOwned(barrier.Resource);
            MemoryBarrierMask barrierMask = OpenGLMemoryBarrierTranslator.ToMask(barrier);
            mask |= barrierMask;
            if (barrierMask != 0)
                _barrierResourceReferences.Add(barrier.Resource);
        }

        if (mask != 0)
        {
            _commands.Add(new OpenGLRecordedCommand
            {
                Kind = OpenGLCommandKind.MemoryBarrier,
                BarrierMask = mask
            });
        }
    }

    public void CopyBuffer(
        IGraphicsBuffer source,
        ulong sourceOffset,
        IGraphicsBuffer destination,
        ulong destinationOffset,
        ulong sizeInBytes)
    {
        EnsureRecording();
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);
        EnsureOwned(source);
        EnsureOwned(destination);

        if ((source.Description.Usage & GraphicsBufferUsage.CopySource) == 0)
            throw new GraphicsValidationException("The source buffer was not created for copy-source use.");
        if ((destination.Description.Usage & GraphicsBufferUsage.CopyDestination) == 0)
            throw new GraphicsValidationException("The destination buffer was not created for copy-destination use.");

        new GraphicsBufferRange(sourceOffset, sizeInBytes).Validate(source.Description);
        new GraphicsBufferRange(destinationOffset, sizeInBytes).Validate(destination.Description);
        if (sourceOffset > (ulong)nint.MaxValue || destinationOffset > (ulong)nint.MaxValue ||
            sizeInBytes > (ulong)nuint.MaxValue)
            throw new GraphicsValidationException("The buffer copy range exceeds the native addressable size.");

        _commands.Add(new OpenGLRecordedCommand
        {
            Kind = OpenGLCommandKind.CopyBuffer,
            SourceBuffer = source,
            DestinationBuffer = destination,
            SourceOffset = sourceOffset,
            DestinationOffset = destinationOffset,
            SizeInBytes = sizeInBytes
        });
    }

    public void CopyTexture(IGraphicsTexture source, IGraphicsTexture destination)
    {
        EnsureRecording();
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);
        GraphicsCommandValidation.ValidateTextureCopyNotAliased(source, destination);
        EnsureOwned(source);
        EnsureOwned(destination);

        GraphicsTextureDescription sourceDescription = source.Description;
        GraphicsTextureDescription destinationDescription = destination.Description;
        if ((sourceDescription.Usage & GraphicsTextureUsage.CopySource) == 0 ||
            (destinationDescription.Usage & GraphicsTextureUsage.CopyDestination) == 0)
            throw new GraphicsValidationException("Texture copies require CopySource and CopyDestination usage respectively.");
        if (sourceDescription.Dimension != destinationDescription.Dimension ||
            sourceDescription.Width != destinationDescription.Width ||
            sourceDescription.Height != destinationDescription.Height ||
            sourceDescription.Depth != destinationDescription.Depth ||
            sourceDescription.ArrayLayers != destinationDescription.ArrayLayers ||
            sourceDescription.MipLevels != destinationDescription.MipLevels ||
            sourceDescription.SampleCount != destinationDescription.SampleCount ||
            sourceDescription.Format != destinationDescription.Format)
            throw new GraphicsValidationException("The source and destination texture descriptions must match for a whole-texture copy.");

        if (sourceDescription.SampleCount != 1)
            throw new GraphicsValidationException("OpenGL image copies do not support multisample textures in this backend.");

        _commands.Add(new OpenGLRecordedCommand
        {
            Kind = OpenGLCommandKind.CopyTexture,
            SourceTexture = source,
            DestinationTexture = destination
        });
    }

    public void SetRenderTargets(ReadOnlySpan<IGraphicsTextureView> colorTargets, IGraphicsTextureView? depthStencilTarget = null)
    {
        EnsureRecording();
        EnsureGraphicsQueue();
        GraphicsRenderTargetExtent extent = GraphicsRenderTargetValidation.Validate(
            Device, colorTargets, depthStencilTarget);
        int viewOffset = _renderTargetViews.Count;
        for (int i = 0; i < colorTargets.Length; i++)
        {
            EnsureOwned(colorTargets[i]);
            _renderTargetViews.Add(colorTargets[i]);
        }

        if (depthStencilTarget is not null)
            EnsureOwned(depthStencilTarget);

        _commands.Add(new OpenGLRecordedCommand
        {
            Kind = OpenGLCommandKind.SetRenderTargets,
            RenderTargetViewOffset = viewOffset,
            RenderTargetColorCount = colorTargets.Length,
            RenderTargetDepthStencil = depthStencilTarget
        });
        _recordedColorTargetCount = (uint)colorTargets.Length;
        _recordedTargetViewOffset = viewOffset;
        _recordedTargetExtent = extent;
        _hasRecordedTargets = true;
        _recordedHasDepthStencil = depthStencilTarget is not null;
        _renderTargetsDepthFormat = depthStencilTarget is null
            ? GraphicsFormat.Unknown
            : depthStencilTarget.Description.Format == GraphicsFormat.Unknown
                ? depthStencilTarget.Texture.Description.Format
                : depthStencilTarget.Description.Format;
    }

    public void SetViewport(in GraphicsViewport viewport)
    {
        EnsureRecording();
        EnsureGraphicsQueue();
        GraphicsViewport value = viewport;
        value.Validate();
        _hasRecordedViewport = true;
        _commands.Add(new OpenGLRecordedCommand { Kind = OpenGLCommandKind.SetViewport, Viewport = value });
    }

    public void SetScissor(in GraphicsScissorRect scissor)
    {
        EnsureRecording();
        EnsureGraphicsQueue();
        GraphicsScissorRect value = scissor;
        value.Validate();
        _commands.Add(new OpenGLRecordedCommand { Kind = OpenGLCommandKind.SetScissor, Scissor = value });
    }

    public void ClearColor(uint attachmentIndex, in GraphicsClearColor color)
    {
        EnsureRecording();
        EnsureGraphicsQueue();
        GraphicsFormat format = GetColorTargetFormat(attachmentIndex);
        if (OpenGLFormatCapabilities.IsUnsignedInteger(format) || OpenGLFormatCapabilities.IsSignedInteger(format))
            throw new GraphicsValidationException("Floating-point color clears cannot target integer formats.");
        GraphicsClearColor value = color;
        value.Validate();
        _commands.Add(new OpenGLRecordedCommand
        {
            Kind = OpenGLCommandKind.ClearColor,
            AttachmentIndex = attachmentIndex,
            ClearColor = value
        });
    }

    public void ClearColorUInt(uint attachmentIndex, in GraphicsClearColorUInt color)
    {
        EnsureRecording();
        EnsureGraphicsQueue();
        GraphicsFormat format = GetColorTargetFormat(attachmentIndex);
        if (!OpenGLFormatCapabilities.IsUnsignedInteger(format))
            throw new GraphicsValidationException("Unsigned integer color clears require an unsigned integer attachment format.");
        _commands.Add(new OpenGLRecordedCommand
        {
            Kind = OpenGLCommandKind.ClearColorUInt,
            AttachmentIndex = attachmentIndex,
            ClearColorUInt = color
        });
    }

    public void ClearColorSInt(uint attachmentIndex, in GraphicsClearColorSInt color)
    {
        EnsureRecording();
        EnsureGraphicsQueue();
        GraphicsFormat format = GetColorTargetFormat(attachmentIndex);
        if (!OpenGLFormatCapabilities.IsSignedInteger(format))
            throw new GraphicsValidationException("Signed integer color clears require a signed integer attachment format.");
        _commands.Add(new OpenGLRecordedCommand
        {
            Kind = OpenGLCommandKind.ClearColorSInt,
            AttachmentIndex = attachmentIndex,
            ClearColorSInt = color
        });
    }

    public void ClearDepthStencil(float depth, uint stencil, bool clearDepth = true, bool clearStencil = false)
    {
        EnsureRecording();
        EnsureGraphicsQueue();
        if (!_recordedHasDepthStencil)
            throw new GraphicsValidationException("A depth/stencil attachment must be bound before it can be cleared.");
        if (!clearDepth && !clearStencil)
            throw new ArgumentException("At least one of clearDepth or clearStencil must be true.");
        if (clearDepth && (!float.IsFinite(depth) || depth < 0 || depth > 1))
            throw new ArgumentOutOfRangeException(nameof(depth));
        if (clearStencil)
        {
            GraphicsFormat depthFormat = _renderTargetsDepthFormat;
            if (depthFormat is not (GraphicsFormat.D24UNormS8UInt or GraphicsFormat.D32FloatS8UInt))
                throw new GraphicsValidationException("A stencil clear requires a depth/stencil attachment with stencil storage.");
        }
        _commands.Add(new OpenGLRecordedCommand
        {
            Kind = OpenGLCommandKind.ClearDepthStencil,
            ClearDepth = depth,
            ClearStencil = stencil,
            ClearDepthEnabled = clearDepth,
            ClearStencilEnabled = clearStencil
        });
    }
    public void SetRenderPipeline(IGraphicsRenderPipeline pipeline)
    {
        EnsureRecording();
        EnsureGraphicsQueue();
        ArgumentNullException.ThrowIfNull(pipeline);
        EnsureOwned(pipeline);
        if (pipeline is not OpenGLGraphicsRenderPipeline openGlPipeline)
            throw new GraphicsValidationException("The render pipeline belongs to an incompatible backend.");

        _recordedPipeline = openGlPipeline;
        _recordedComputePipeline = null;
        _recordedPipelineLayout = (OpenGLGraphicsPipelineLayout)openGlPipeline.Layout;
        _recordedVertexBuffers.Clear();
        _recordedBoundSets.Clear();
        _recordedIndexBuffer = null;
        _commands.Add(new OpenGLRecordedCommand
        {
            Kind = OpenGLCommandKind.SetRenderPipeline,
            RenderPipeline = openGlPipeline
        });
    }

    public void SetComputePipeline(IGraphicsComputePipeline pipeline)
    {
        EnsureRecording();
        EnsureComputeQueue();
        ArgumentNullException.ThrowIfNull(pipeline);
        EnsureOwned(pipeline);
        if (pipeline is not OpenGLGraphicsComputePipeline openGlPipeline)
            throw new GraphicsValidationException("The compute pipeline belongs to an incompatible backend.");

        _recordedComputePipeline = openGlPipeline;
        _recordedPipeline = null;
        _recordedPipelineLayout = (OpenGLGraphicsPipelineLayout)openGlPipeline.Layout;
        _recordedBoundSets.Clear();
        _commands.Add(new OpenGLRecordedCommand
        {
            Kind = OpenGLCommandKind.SetComputePipeline,
            ComputePipeline = openGlPipeline
        });
    }

    public void SetBindingSet(uint setIndex, IGraphicsBindingSet bindingSet)
    {
        EnsureRecording();
        ArgumentNullException.ThrowIfNull(bindingSet);
        EnsureOwned(bindingSet);
        if (_recordedPipelineLayout is null)
            throw new GraphicsValidationException("Set a graphics or compute pipeline before binding descriptor sets.");
        if (bindingSet is not OpenGLGraphicsBindingSet openGlSet)
            throw new GraphicsValidationException("The binding set belongs to an incompatible backend.");

        ReadOnlyMemory<IGraphicsBindingLayout> layouts = _recordedPipelineLayout.BindingLayouts;
        if (setIndex >= (uint)layouts.Length ||
            !ReferenceEquals(layouts.Span[(int)setIndex], openGlSet.Layout))
            throw new GraphicsValidationException($"Binding set {setIndex} does not match the active pipeline layout.");

        int snapshotOffset = openGlSet.SnapshotTo(_bindingSnapshotValues);
        for (int i = 0; i < openGlSet.ValueCount; i++)
        {
            OpenGLBindingValue value = _bindingSnapshotValues[snapshotOffset + i];
            if (value.Buffer is not null)
                EnsureOwned(value.Buffer);
            if (value.Texture is not null)
                EnsureOwned(value.Texture);
            if (value.Sampler is not null)
                EnsureOwned(value.Sampler);
        }

        _commands.Add(new OpenGLRecordedCommand
        {
            Kind = OpenGLCommandKind.SetBindingSet,
            SetIndex = setIndex,
            BindingLayout = openGlSet.OpenGLLayout,
            BindingValueOffset = snapshotOffset,
            BindingValueCount = openGlSet.ValueCount
        });
        _recordedBoundSets.Add(setIndex);
    }

    public void SetVertexBuffer(uint binding, IGraphicsBuffer buffer, ulong offset = 0)
    {
        EnsureRecording();
        EnsureGraphicsQueue();
        ArgumentNullException.ThrowIfNull(buffer);
        EnsureOwned(buffer);

        if (_recordedPipeline?.Description.VertexInput is not { } input)
            throw new GraphicsValidationException("The active pipeline has no vertex-input layout.");
        if ((buffer.Description.Usage & GraphicsBufferUsage.Vertex) == 0)
            throw new GraphicsValidationException("The buffer was not created with vertex usage.");
        if (offset > buffer.Description.SizeInBytes || offset > (ulong)nint.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(offset));

        bool found = false;
        ReadOnlySpan<GraphicsVertexBufferLayout> layouts = input.Buffers;
        GraphicsVertexBufferLayout bufferLayout = default;
        for (int i = 0; i < layouts.Length; i++)
        {
            if (layouts[i].Binding != binding)
                continue;
            found = true;
            bufferLayout = layouts[i];
            break;
        }

        if (!found)
            throw new GraphicsValidationException($"Vertex binding {binding} is not declared by the active pipeline.");

        _recordedVertexBuffers[binding] = new OpenGLVertexBufferBinding(
            buffer, offset, bufferLayout.Stride, bufferLayout.InputRate);
        _commands.Add(new OpenGLRecordedCommand
        {
            Kind = OpenGLCommandKind.SetVertexBuffer,
            VertexBinding = binding,
            VertexBuffer = buffer,
            BufferOffset = offset,
            Stride = bufferLayout.Stride
        });
    }

    public void SetIndexBuffer(IGraphicsBuffer buffer, GraphicsIndexFormat format, ulong offset = 0)
    {
        EnsureRecording();
        EnsureGraphicsQueue();
        ArgumentNullException.ThrowIfNull(buffer);
        EnsureOwned(buffer);
        if (!Enum.IsDefined(format))
            throw new ArgumentOutOfRangeException(nameof(format));
        if (_recordedPipeline?.IsMeshPipeline == true)
            throw new GraphicsValidationException("Mesh pipelines do not consume an index buffer.");
        if ((buffer.Description.Usage & GraphicsBufferUsage.Index) == 0)
            throw new GraphicsValidationException("The buffer was not created with index usage.");

        uint elementSize = format == GraphicsIndexFormat.UInt16 ? 2u : 4u;
        if (offset > buffer.Description.SizeInBytes || offset % elementSize != 0 ||
            offset > (ulong)nint.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(offset));

        _recordedIndexBuffer = buffer;
        _recordedIndexFormat = format;
        _recordedIndexOffset = offset;
        _commands.Add(new OpenGLRecordedCommand
        {
            Kind = OpenGLCommandKind.SetIndexBuffer,
            IndexBuffer = buffer,
            IndexFormat = format,
            BufferOffset = offset
        });
    }

    public void Draw(uint vertexCount, uint instanceCount = 1, uint firstVertex = 0, uint firstInstance = 0)
    {
        EnsureRecording();
        EnsureConventionalPipeline();
        EnsureDrawReady();
        ValidateVertexBindings(firstVertex, vertexCount, firstInstance, instanceCount);
        if (firstVertex > int.MaxValue)
            throw new GraphicsValidationException("The first vertex exceeds the native OpenGL integer range.");

        _commands.Add(new OpenGLRecordedCommand
        {
            Kind = OpenGLCommandKind.Draw,
            VertexCount = vertexCount,
            InstanceCount = instanceCount,
            FirstVertex = firstVertex,
            FirstInstance = firstInstance
        });
    }

    public void DrawIndexed(
        uint indexCount,
        uint instanceCount = 1,
        uint firstIndex = 0,
        int baseVertex = 0,
        uint firstInstance = 0)
    {
        EnsureRecording();
        EnsureConventionalPipeline();
        EnsureDrawReady();
        ValidateVertexBindings(0, 0, firstInstance, instanceCount);
        if (_recordedIndexBuffer is null)
            throw new GraphicsValidationException("An index buffer must be set before an indexed draw.");

        uint elementSize = _recordedIndexFormat == GraphicsIndexFormat.UInt16 ? 2u : 4u;
        ulong byteOffset = checked(_recordedIndexOffset + (ulong)firstIndex * elementSize);
        ulong byteCount = checked((ulong)indexCount * elementSize);
        if (byteOffset > _recordedIndexBuffer.Description.SizeInBytes ||
            byteCount > _recordedIndexBuffer.Description.SizeInBytes - byteOffset)
            throw new GraphicsValidationException("The indexed draw reads outside the bound index buffer.");

        _commands.Add(new OpenGLRecordedCommand
        {
            Kind = OpenGLCommandKind.DrawIndexed,
            IndexBuffer = _recordedIndexBuffer,
            IndexFormat = _recordedIndexFormat,
            BufferOffset = byteOffset,
            IndexCount = indexCount,
            InstanceCount = instanceCount,
            BaseVertex = baseVertex,
            FirstInstance = firstInstance
        });
    }

    public void DrawIndirect(IGraphicsBuffer arguments, ulong offset, uint drawCount = 1, uint stride = 0)
    {
        EnsureRecording();
        EnsureConventionalPipeline();
        EnsureDrawReady();
        ValidateVertexBindings();
        ValidateIndirectBuffer(arguments, offset, drawCount, ref stride, 16);
        if (drawCount > 1 && !Device.Capabilities.Features.Supports(GraphicsFeature.MultiDrawIndirect))
            throw new GraphicsFeatureNotSupportedException(GraphicsFeature.MultiDrawIndirect, GraphicsFeature.MultiDrawIndirect);

        _commands.Add(new OpenGLRecordedCommand
        {
            Kind = OpenGLCommandKind.DrawIndirect,
            SourceBuffer = arguments,
            BufferOffset = offset,
            DrawCount = drawCount,
            Stride = stride
        });
    }

    public void DrawIndexedIndirect(IGraphicsBuffer arguments, ulong offset, uint drawCount = 1, uint stride = 0)
    {
        EnsureRecording();
        EnsureConventionalPipeline();
        EnsureDrawReady();
        ValidateVertexBindings();
        if (_recordedIndexBuffer is null)
            throw new GraphicsValidationException("An index buffer must be set before an indexed indirect draw.");
        GraphicsCommandValidation.ValidateIndexedIndirectIndexBufferOffset(_recordedIndexOffset);
        ValidateIndirectBuffer(arguments, offset, drawCount, ref stride, 20);
        if (drawCount > 1 && !Device.Capabilities.Features.Supports(GraphicsFeature.MultiDrawIndirect))
            throw new GraphicsFeatureNotSupportedException(GraphicsFeature.MultiDrawIndirect, GraphicsFeature.MultiDrawIndirect);

        _commands.Add(new OpenGLRecordedCommand
        {
            Kind = OpenGLCommandKind.DrawIndexedIndirect,
            SourceBuffer = arguments,
            BufferOffset = offset,
            DrawCount = drawCount,
            Stride = stride,
            IndexBuffer = _recordedIndexBuffer,
            IndexFormat = _recordedIndexFormat
        });
    }

    public void DrawMeshTasks(uint groupCountX, uint groupCountY = 1, uint groupCountZ = 1)
    {
        EnsureRecording();
        EnsureMeshPipeline();
        EnsureDrawReady();

        OpenGLGraphicsDevice device = (OpenGLGraphicsDevice)Device;
        OpenGLGraphicsRenderPipeline pipeline = _recordedPipeline!;
        bool usesTaskStage = pipeline.IsTaskPipeline;
        GraphicsFeature requiredFeature = usesTaskStage
            ? GraphicsFeature.TaskShaders
            : GraphicsFeature.MeshShaders;
        if (pipeline.MeshShaderDialect == OpenGLMeshShaderDialect.Nv)
        {
            OpenGLMeshTaskValidation.Validate(
                groupCountX, groupCountY, groupCountZ,
                device.MaxNvDrawMeshTasksCount, 1, 1,
                device.MaxNvDrawMeshTasksCount,
                requiredFeature);
        }
        else
        {
            OpenGLMeshTaskValidation.Validate(
                groupCountX, groupCountY, groupCountZ,
                usesTaskStage ? device.MaxTaskWorkGroupCountX : device.MaxMeshWorkGroupCountX,
                usesTaskStage ? device.MaxTaskWorkGroupCountY : device.MaxMeshWorkGroupCountY,
                usesTaskStage ? device.MaxTaskWorkGroupCountZ : device.MaxMeshWorkGroupCountZ,
                usesTaskStage ? device.MaxTaskWorkGroupTotalCount : device.MaxMeshWorkGroupTotalCount,
                requiredFeature);
        }

        _commands.Add(new OpenGLRecordedCommand
        {
            Kind = OpenGLCommandKind.DrawMeshTasks,
            GroupCountX = groupCountX,
            GroupCountY = groupCountY,
            GroupCountZ = groupCountZ
        });
    }

    public void DrawMeshTasksIndirect(
        IGraphicsBuffer arguments,
        ulong offset,
        uint drawCount = 1,
        uint stride = 0)
    {
        EnsureRecording();
        EnsureMeshPipeline();
        EnsureDrawReady();
        OpenGLGraphicsDevice device = (OpenGLGraphicsDevice)Device;
        if (_recordedPipeline!.MeshShaderDialect == OpenGLMeshShaderDialect.Nv)
        {
            throw new GraphicsValidationException(
                "GL_NV_mesh_shader indirect commands use a two-field { count, first } layout, " +
                "while the common Auralis command uses three-dimensional { groupCountX, groupCountY, groupCountZ } arguments.");
        }
        ValidateIndirectBuffer(arguments, offset, drawCount, ref stride, 12);

        _commands.Add(new OpenGLRecordedCommand
        {
            Kind = OpenGLCommandKind.DrawMeshTasksIndirect,
            SourceBuffer = arguments,
            BufferOffset = offset,
            DrawCount = drawCount,
            Stride = stride
        });
    }

    private void ValidateIndirectBuffer(
        IGraphicsBuffer arguments,
        ulong offset,
        uint drawCount,
        ref uint stride,
        uint commandSize)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        EnsureOwned(arguments);
        if ((arguments.Description.Usage & GraphicsBufferUsage.IndirectArguments) == 0)
            throw new GraphicsValidationException("Indirect argument buffers require IndirectArguments usage.");
        if (offset % 4 != 0 || offset > (ulong)nint.MaxValue)
            throw new GraphicsValidationException("Indirect argument offset must be four-byte aligned and natively addressable.");
        if (stride == 0)
            stride = commandSize;
        if (stride < commandSize || stride % 4 != 0)
            throw new GraphicsValidationException("Indirect command stride must be at least the command size and four-byte aligned.");
        ulong byteCount = drawCount == 0 ? 0 : checked((ulong)(drawCount - 1) * stride + commandSize);
        if (offset > arguments.Description.SizeInBytes ||
            byteCount > arguments.Description.SizeInBytes - offset)
            throw new GraphicsValidationException("Indirect arguments exceed the buffer range.");
    }

    private void EnsureConventionalPipeline()
    {
        if (_recordedPipeline is null)
            throw new GraphicsValidationException("A render pipeline must be set before drawing.");
        if (_recordedPipeline.IsMeshPipeline)
            throw new GraphicsValidationException(
                "Conventional draw commands cannot be used with a mesh-shader pipeline; use DrawMeshTasks.");
    }

    private void EnsureMeshPipeline()
    {
        if (_recordedPipeline is null || !_recordedPipeline.IsMeshPipeline)
            throw new GraphicsValidationException("A mesh-shader pipeline must be set before drawing mesh tasks.");
        if (!Device.Capabilities.Features.Supports(GraphicsFeature.MeshShaders))
            throw new GraphicsFeatureNotSupportedException(
                GraphicsFeature.MeshShaders, GraphicsFeature.MeshShaders);
    }

    private void EnsureDrawReady()
    {
        if (_recordedPipeline is null)
            throw new GraphicsValidationException("A render pipeline must be set before a draw.");
        if (!_hasRecordedTargets)
            throw new GraphicsValidationException("Offscreen rendering requires an explicit render-target set.");
        if (!_hasRecordedViewport)
            throw new GraphicsValidationException("Set a viewport before issuing a draw.");
        ReadOnlyMemory<IGraphicsBindingLayout> layouts = _recordedPipelineLayout!.BindingLayouts;
        for (uint setIndex = 0; setIndex < (uint)layouts.Length; setIndex++)
        {
            if (!_recordedBoundSets.Contains(setIndex))
                throw new GraphicsValidationException($"Descriptor set {setIndex} has not been bound for the active pipeline.");
        }

        GraphicsRenderPipelineDescription pipeline = _recordedPipeline.Description;
        if (pipeline.SampleCount != _recordedTargetExtent.SampleCount)
            throw new GraphicsValidationException("The bound render targets do not match the pipeline sample count.");
        if (pipeline.ColorFormats.Length != _recordedColorTargetCount)
            throw new GraphicsValidationException("The bound render-target color attachment count does not match the pipeline.");
        for (int i = 0; i < pipeline.ColorFormats.Length; i++)
        {
            IGraphicsTextureView view = _renderTargetViews[_recordedTargetViewOffset + i];
            GraphicsFormat format = view.Description.Format == GraphicsFormat.Unknown
                ? view.Texture.Description.Format
                : view.Description.Format;
            if (pipeline.ColorFormats[i] != format)
                throw new GraphicsValidationException($"Color attachment {i} format {format} does not match pipeline format {pipeline.ColorFormats[i]}.");
        }

        if (pipeline.DepthStencilFormat != _renderTargetsDepthFormat)
            throw new GraphicsValidationException("The bound depth/stencil format does not match the render pipeline.");
    }

    private void ValidateVertexBindings(
        uint firstVertex = 0,
        uint vertexCount = 0,
        uint firstInstance = 0,
        uint instanceCount = 0)
    {
        if (_recordedPipeline?.Description.VertexInput is not { } input)
            return;

        ReadOnlySpan<GraphicsVertexBufferLayout> layouts = input.Buffers;
        ReadOnlySpan<GraphicsVertexAttribute> attributes = input.Attributes;
        for (int i = 0; i < layouts.Length; i++)
        {
            GraphicsVertexBufferLayout layout = layouts[i];
            if (!_recordedVertexBuffers.TryGetValue(layout.Binding, out OpenGLVertexBufferBinding binding))
                throw new GraphicsValidationException($"Vertex buffer binding {layout.Binding} has not been set.");

            uint firstElement = layout.InputRate == GraphicsVertexInputRate.PerInstance
                ? firstInstance
                : firstVertex;
            uint elementCount = layout.InputRate == GraphicsVertexInputRate.PerInstance
                ? instanceCount
                : vertexCount;
            if (elementCount == 0)
                continue;

            uint attributeEnd = 0;
            for (int attributeIndex = 0; attributeIndex < attributes.Length; attributeIndex++)
            {
                GraphicsVertexAttribute attribute = attributes[attributeIndex];
                if (attribute.Binding != layout.Binding)
                    continue;

                uint byteSize = OpenGLPipelineMapping.GetVertexFormatByteSize(attribute.Format);
                uint end = checked(attribute.Offset + byteSize);
                attributeEnd = Math.Max(attributeEnd, end);
            }

            if (attributeEnd == 0)
                continue;

            ulong lastElement = checked((ulong)firstElement + elementCount - 1);
            ulong requiredEnd = checked(
                binding.Offset +
                lastElement * binding.Stride +
                attributeEnd);

            if (requiredEnd > binding.Buffer.Description.SizeInBytes)
            {
                throw new GraphicsValidationException(
                    $"Vertex binding {layout.Binding} reads byte {requiredEnd} but the buffer size is {binding.Buffer.Description.SizeInBytes}.");
            }
        }
    }
    internal static bool TryCreateDispatchCommand(
        uint groupCountX,
        uint groupCountY,
        uint groupCountZ,
        uint maxGroupCountX,
        uint maxGroupCountY,
        uint maxGroupCountZ,
        out OpenGLRecordedCommand command)
    {
        OpenGLComputeDispatchValidation.Validate(
            groupCountX, groupCountY, groupCountZ,
            maxGroupCountX, maxGroupCountY, maxGroupCountZ);

        command = default;
        if (groupCountX == 0 || groupCountY == 0 || groupCountZ == 0)
            return false;

        command = new OpenGLRecordedCommand
        {
            Kind = OpenGLCommandKind.Dispatch,
            GroupCountX = groupCountX,
            GroupCountY = groupCountY,
            GroupCountZ = groupCountZ
        };
        return true;
    }

    public void Dispatch(uint groupCountX, uint groupCountY = 1, uint groupCountZ = 1)
    {
        EnsureRecording();
        EnsureComputeQueue();
        EnsureComputeReady();

        OpenGLGraphicsDevice device = (OpenGLGraphicsDevice)Device;
        if (TryCreateDispatchCommand(
            groupCountX, groupCountY, groupCountZ,
            device.MaxComputeWorkGroupCountX,
            device.MaxComputeWorkGroupCountY,
            device.MaxComputeWorkGroupCountZ,
            out OpenGLRecordedCommand command))
        {
            _commands.Add(command);
        }
    }

    public void DispatchIndirect(IGraphicsBuffer arguments, ulong offset)
    {
        EnsureRecording();
        EnsureComputeQueue();
        EnsureComputeReady();
        ArgumentNullException.ThrowIfNull(arguments);
        EnsureOwned(arguments);
        if ((arguments.Description.Usage & GraphicsBufferUsage.IndirectArguments) == 0)
            throw new GraphicsValidationException("Indirect dispatch requires IndirectArguments buffer usage.");
        if ((offset & 3) != 0 || offset > (ulong)nint.MaxValue)
            throw new GraphicsValidationException("Indirect dispatch offset must be four-byte aligned and natively addressable.");
        if (offset > arguments.Description.SizeInBytes ||
            12UL > arguments.Description.SizeInBytes - offset)
            throw new GraphicsValidationException("Indirect dispatch arguments exceed the buffer range.");

        _commands.Add(new OpenGLRecordedCommand
        {
            Kind = OpenGLCommandKind.DispatchIndirect,
            SourceBuffer = arguments,
            BufferOffset = offset
        });
    }

    private void EnsureComputeReady()
    {
        if (_recordedComputePipeline is null)
            throw new GraphicsValidationException("A compute pipeline must be set before dispatch.");
        ReadOnlyMemory<IGraphicsBindingLayout> layouts = _recordedComputePipeline.Layout.BindingLayouts;
        for (uint setIndex = 0; setIndex < (uint)layouts.Length; setIndex++)
        {
            if (!_recordedBoundSets.Contains(setIndex))
                throw new GraphicsValidationException($"Compute descriptor set {setIndex} has not been bound.");
        }
    }

    private void EnsureComputeQueue()
    {
        if (_queue.QueueType != GraphicsQueueType.Compute)
            throw new GraphicsValidationException("Compute commands require a compute-capable logical queue.");
    }

    internal unsafe void Execute(
        GL gl,
        ArbDirectStateAccess dsa,
        ArbCopyImage copyImage,
        ArbViewportArray viewportApi,
        ArbBaseInstance baseInstanceApi,
        ArbDrawIndirect drawIndirectApi,
        ArbMultiDrawIndirect multiDrawIndirectApi,
        ArbShaderImageLoadStore imageLoadStoreApi,
        ArbDrawBuffersBlend drawBuffersBlendApi,
        ArbComputeShader computeShaderApi,
        ExtMeshShader? meshShaderApi,
        NVMeshShader? nvMeshShaderApi)
    {
        ReadOnlySpan<OpenGLRecordedCommand> commands = Commands;
        uint framebuffer = 0;
        OpenGLGraphicsRenderPipeline? activePipeline = null;
        OpenGLGraphicsComputePipeline? activeComputePipeline = null;
        OpenGLGraphicsPipelineLayout? activePipelineLayout = null;
        // Dynamic scissor state is command-list-local in the common API. Do not inherit an
        // enable bit from a previously submitted list on this state-owning context.
        gl.Disable(EnableCap.ScissorTest);
        try
        {
            for (int i = 0; i < commands.Length; i++)
            {
                ref readonly OpenGLRecordedCommand command = ref commands[i];
                switch (command.Kind)
                {
                case OpenGLCommandKind.MemoryBarrier:
                    if (command.BarrierMask != 0)
                        gl.MemoryBarrier(command.BarrierMask);
                    break;
                case OpenGLCommandKind.CopyBuffer:
                    dsa.CopyNamedBufferSubData(
                        ((OpenGLGraphicsBuffer)command.SourceBuffer!).Handle,
                        ((OpenGLGraphicsBuffer)command.DestinationBuffer!).Handle,
                        checked((nint)command.SourceOffset),
                        checked((nint)command.DestinationOffset),
                        checked((nuint)command.SizeInBytes));
                    break;
                    case OpenGLCommandKind.CopyTexture:
                        ExecuteTextureCopy(copyImage, command.SourceTexture!, command.DestinationTexture!);
                        break;
                    case OpenGLCommandKind.SetRenderTargets:
                        if (framebuffer != 0)
                            gl.DeleteFramebuffer(framebuffer);
                        framebuffer = CreateFramebuffer(
                            gl,
                            dsa,
                            CollectionsMarshal.AsSpan(_renderTargetViews).Slice(
                                command.RenderTargetViewOffset,
                                command.RenderTargetColorCount),
                            command.RenderTargetDepthStencil);
                        gl.BindFramebuffer(FramebufferTarget.DrawFramebuffer, framebuffer);
                        break;
                    case OpenGLCommandKind.SetViewport:
                        GraphicsViewport viewport = command.Viewport;
                        viewportApi.ViewportIndexed(0, viewport.X, viewport.Y, viewport.Width, viewport.Height);
                        viewportApi.DepthRangeIndexed(0, viewport.MinDepth, viewport.MaxDepth);
                        break;
                    case OpenGLCommandKind.SetScissor:
                        GraphicsScissorRect scissor = command.Scissor;
                        gl.Enable(EnableCap.ScissorTest);
                        viewportApi.ScissorIndexed(0, scissor.X, scissor.Y, scissor.Width, scissor.Height);
                        break;
                    case OpenGLCommandKind.ClearColor:
                        EnsureFramebuffer(framebuffer);
                        ClearColor(dsa, framebuffer, command.AttachmentIndex, command.ClearColor);
                        break;
                    case OpenGLCommandKind.ClearColorUInt:
                        EnsureFramebuffer(framebuffer);
                        ClearColorUInt(dsa, framebuffer, command.AttachmentIndex, command.ClearColorUInt);
                        break;
                    case OpenGLCommandKind.ClearColorSInt:
                        EnsureFramebuffer(framebuffer);
                        ClearColorSInt(dsa, framebuffer, command.AttachmentIndex, command.ClearColorSInt);
                        break;
                    case OpenGLCommandKind.ClearDepthStencil:
                        EnsureFramebuffer(framebuffer);
                        ClearDepthStencil(dsa, framebuffer, command);
                        break;
                    case OpenGLCommandKind.SetRenderPipeline:
                        activePipeline = command.RenderPipeline!;
                        activeComputePipeline = null;
                        activePipelineLayout = (OpenGLGraphicsPipelineLayout)activePipeline.Layout;
                        activePipeline.Apply(
                            gl,
                            drawBuffersBlendApi,
                            ((OpenGLGraphicsDevice)Device).DepthBoundsApi,
                            ((OpenGLGraphicsDevice)Device).PolygonOffsetClampApi);
                        break;
                    case OpenGLCommandKind.SetComputePipeline:
                        activeComputePipeline = command.ComputePipeline!;
                        activePipeline = null;
                        activePipelineLayout = (OpenGLGraphicsPipelineLayout)activeComputePipeline.Layout;
                        gl.UseProgram(activeComputePipeline.Program);
                        break;
                    case OpenGLCommandKind.SetBindingSet:
                        if (activePipelineLayout is null)
                            throw new GraphicsValidationException("A graphics or compute pipeline must be active before descriptor sets are bound.");
                        OpenGLBindingSetBinder.Bind(
                            gl,
                            dsa,
                            imageLoadStoreApi,
                            command.BindingLayout!,
                            CollectionsMarshal.AsSpan(_bindingSnapshotValues).Slice(
                                command.BindingValueOffset,
                                command.BindingValueCount),
                            activePipelineLayout,
                            command.SetIndex);
                        break;
                    case OpenGLCommandKind.SetVertexBuffer:
                        if (activePipeline is null)
                            throw new GraphicsValidationException("A render pipeline must be active before vertex buffers are bound.");
                        dsa.VertexArrayVertexBuffer(
                            activePipeline.VertexArray,
                            command.VertexBinding,
                            ((OpenGLGraphicsBuffer)command.VertexBuffer!).Handle,
                            checked((nint)command.BufferOffset),
                            command.Stride);
                        break;
                    case OpenGLCommandKind.SetIndexBuffer:
                        if (activePipeline is null)
                            throw new GraphicsValidationException("A render pipeline must be active before an index buffer is bound.");
                        dsa.VertexArrayElementBuffer(
                            activePipeline.VertexArray,
                            ((OpenGLGraphicsBuffer)command.IndexBuffer!).Handle);
                        break;
                    case OpenGLCommandKind.Draw:
                        if (activePipeline is null)
                            throw new GraphicsValidationException("A render pipeline must be active before drawing.");
                        baseInstanceApi.DrawArraysInstancedBaseInstance(
                            activePipeline.Topology,
                            checked((int)command.FirstVertex),
                            command.VertexCount,
                            command.InstanceCount,
                            command.FirstInstance);
                        break;
                    case OpenGLCommandKind.DrawIndexed:
                        if (activePipeline is null)
                            throw new GraphicsValidationException("A render pipeline must be active before indexed drawing.");
                        baseInstanceApi.DrawElementsInstancedBaseVertexBaseInstance(
                            activePipeline.Topology,
                            command.IndexCount,
                            command.IndexFormat == GraphicsIndexFormat.UInt16
                                ? DrawElementsType.UnsignedShort
                                : DrawElementsType.UnsignedInt,
                            (void*)command.BufferOffset,
                            command.InstanceCount,
                            command.BaseVertex,
                            command.FirstInstance);
                        break;
                    case OpenGLCommandKind.DrawIndirect:
                        if (activePipeline is null)
                            throw new GraphicsValidationException("A render pipeline must be active before indirect drawing.");
                        gl.BindBuffer(
                            BufferTargetARB.DrawIndirectBuffer,
                            ((OpenGLGraphicsBuffer)command.SourceBuffer!).Handle);
                        if (command.DrawCount == 1)
                        {
                            drawIndirectApi.DrawArraysIndirect(
                                activePipeline.Topology,
                                (void*)command.BufferOffset);
                        }
                        else
                        {
                            multiDrawIndirectApi.MultiDrawArraysIndirect(
                                activePipeline.Topology,
                                (void*)command.BufferOffset,
                                command.DrawCount,
                                command.Stride);
                        }
                        break;
                    case OpenGLCommandKind.DrawIndexedIndirect:
                        if (activePipeline is null)
                            throw new GraphicsValidationException("A render pipeline must be active before indexed indirect drawing.");
                        gl.BindBuffer(
                            BufferTargetARB.DrawIndirectBuffer,
                            ((OpenGLGraphicsBuffer)command.SourceBuffer!).Handle);
                        DrawElementsType indexType = command.IndexFormat == GraphicsIndexFormat.UInt16
                            ? DrawElementsType.UnsignedShort
                            : DrawElementsType.UnsignedInt;
                        if (command.DrawCount == 1)
                        {
                            drawIndirectApi.DrawElementsIndirect(
                                activePipeline.Topology,
                                indexType,
                                (void*)command.BufferOffset);
                        }
                        else
                        {
                            multiDrawIndirectApi.MultiDrawElementsIndirect(
                                activePipeline.Topology,
                                indexType,
                                (void*)command.BufferOffset,
                                command.DrawCount,
                                command.Stride);
                        }
                        break;
                    case OpenGLCommandKind.DrawMeshTasks:
                        if (activePipeline is null || !activePipeline.IsMeshPipeline)
                            throw new GraphicsValidationException("A supported mesh-shader pipeline must be active before mesh-task drawing.");
                        if (activePipeline.MeshShaderDialect == OpenGLMeshShaderDialect.Ext)
                        {
                            (meshShaderApi ?? throw new GraphicsFeatureNotSupportedException(
                                GraphicsFeature.MeshShaders, GraphicsFeature.MeshShaders)).DrawMeshTask(
                                    command.GroupCountX,
                                    command.GroupCountY,
                                    command.GroupCountZ);
                        }
                        else if (activePipeline.MeshShaderDialect == OpenGLMeshShaderDialect.Nv)
                        {
                            if (command.GroupCountY != 1 || command.GroupCountZ != 1)
                                throw new GraphicsValidationException(
                                    "GL_NV_mesh_shader supports one-dimensional mesh-task dispatch only.");
                            (nvMeshShaderApi ?? throw new GraphicsFeatureNotSupportedException(
                                GraphicsFeature.MeshShaders, GraphicsFeature.MeshShaders)).DrawMeshTask(
                                    0,
                                    command.GroupCountX);
                        }
                        else
                        {
                            throw new GraphicsValidationException("The active mesh pipeline has no validated extension dialect.");
                        }
                        break;
                    case OpenGLCommandKind.DrawMeshTasksIndirect:
                        if (activePipeline is null || !activePipeline.IsMeshPipeline ||
                            activePipeline.MeshShaderDialect != OpenGLMeshShaderDialect.Ext ||
                            meshShaderApi is null)
                            throw new GraphicsValidationException(
                                "Indirect mesh-task drawing requires an EXT mesh-shader pipeline and its three-dimensional indirect command path.");
                        gl.BindBuffer(
                            BufferTargetARB.DrawIndirectBuffer,
                            ((OpenGLGraphicsBuffer)command.SourceBuffer!).Handle);
                        if (command.DrawCount == 1)
                        {
                            meshShaderApi.DrawMeshTasksIndirect(checked((nint)command.BufferOffset));
                        }
                        else
                        {
                            meshShaderApi.MultiDrawMeshTasksIndirect(
                                checked((nint)command.BufferOffset),
                                command.DrawCount,
                                command.Stride);
                        }
                        break;
                    case OpenGLCommandKind.Dispatch:
                        if (activeComputePipeline is null)
                            throw new GraphicsValidationException("A compute pipeline must be active before dispatch.");
                        computeShaderApi.DispatchCompute(
                            command.GroupCountX,
                            command.GroupCountY,
                            command.GroupCountZ);
                        break;
                    case OpenGLCommandKind.DispatchIndirect:
                        if (activeComputePipeline is null)
                            throw new GraphicsValidationException("A compute pipeline must be active before indirect dispatch.");
                        gl.BindBuffer(
                            BufferTargetARB.DispatchIndirectBuffer,
                            ((OpenGLGraphicsBuffer)command.SourceBuffer!).Handle);
                        computeShaderApi.DispatchComputeIndirect(checked((nint)command.BufferOffset));
                        break;
                    default:
                        throw new GraphicsException($"Unknown recorded OpenGL command kind: {command.Kind}.");
                }
            }
        }
        finally
        {
            // This backend requires exclusive context-state ownership. Leave transient state at a
            // documented baseline rather than restoring arbitrary host bindings.
            gl.BindBuffer(BufferTargetARB.DispatchIndirectBuffer, 0);
            gl.BindBuffer(BufferTargetARB.DrawIndirectBuffer, 0);
            gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            if (framebuffer != 0)
                gl.DeleteFramebuffer(framebuffer);
            gl.Disable(EnableCap.ScissorTest);
        }
    }

    private static uint CreateFramebuffer(
        GL gl,
        ArbDirectStateAccess dsa,
        ReadOnlySpan<IGraphicsTextureView> colorTargets,
        IGraphicsTextureView? depthStencilTarget)
    {
        dsa.CreateFramebuffers(1, out uint framebuffer);
        if (framebuffer == 0)
            throw new GraphicsException("OpenGL failed to create a render-target framebuffer.");

        try
        {
            for (int i = 0; i < colorTargets.Length; i++)
            {
                OpenGLGraphicsTextureView view = colorTargets[i] as OpenGLGraphicsTextureView ??
                    throw new GraphicsValidationException("Render attachment view belongs to an incompatible backend.");
                FramebufferAttachment attachment =
                    (FramebufferAttachment)((uint)FramebufferAttachment.ColorAttachment0 + (uint)i);
                dsa.NamedFramebufferTexture(framebuffer, attachment, view.Handle, 0);
            }

            if (depthStencilTarget is not null)
            {
                OpenGLGraphicsTextureView view = depthStencilTarget as OpenGLGraphicsTextureView ??
                    throw new GraphicsValidationException("Depth/stencil attachment view belongs to an incompatible backend.");
                GraphicsFormat format = view.Description.Format == GraphicsFormat.Unknown
                    ? view.Texture.Description.Format
                    : view.Description.Format;
                FramebufferAttachment attachment = format is GraphicsFormat.D24UNormS8UInt or GraphicsFormat.D32FloatS8UInt
                    ? FramebufferAttachment.DepthStencilAttachment
                    : FramebufferAttachment.DepthAttachment;
                dsa.NamedFramebufferTexture(framebuffer, attachment, view.Handle, 0);
            }

            if (colorTargets.IsEmpty)
            {
                dsa.NamedFramebufferDrawBuffer(framebuffer, ColorBuffer.None);
                dsa.NamedFramebufferReadBuffer(framebuffer, ColorBuffer.None);
            }
            else
            {
                Span<ColorBuffer> drawBuffers = colorTargets.Length <= 16
                ? stackalloc ColorBuffer[colorTargets.Length]
                : new ColorBuffer[colorTargets.Length];
                for (int i = 0; i < drawBuffers.Length; i++)
                    drawBuffers[i] = (ColorBuffer)((uint)ColorBuffer.ColorAttachment0 + (uint)i);
                dsa.NamedFramebufferDrawBuffers(framebuffer, drawBuffers);
                dsa.NamedFramebufferReadBuffer(framebuffer, ColorBuffer.ColorAttachment0);
            }

            ARB status = dsa.CheckNamedFramebufferStatus(framebuffer, FramebufferTarget.Framebuffer);
            if (status != ARB.FramebufferComplete)
                throw new GraphicsException($"The requested OpenGL framebuffer is incomplete: {status}.");

            return framebuffer;
        }
        catch
        {
            gl.DeleteFramebuffer(framebuffer);
            throw;
        }
    }

    private static void EnsureFramebuffer(uint framebuffer)
    {
        if (framebuffer == 0)
            throw new GraphicsValidationException("A render target must be selected before a clear command.");
    }

    private static void ClearColor(
        ArbDirectStateAccess dsa, uint framebuffer, uint attachment, in GraphicsClearColor color)
    {
        Span<float> values = stackalloc float[4] { color.Red, color.Green, color.Blue, color.Alpha };
        dsa.ClearNamedFramebuffer(framebuffer, BufferKind.Color, checked((int)attachment), values);
    }

    private static void ClearColorUInt(
        ArbDirectStateAccess dsa, uint framebuffer, uint attachment, in GraphicsClearColorUInt color)
    {
        Span<uint> values = stackalloc uint[4] { color.Red, color.Green, color.Blue, color.Alpha };
        dsa.ClearNamedFramebuffer(framebuffer, BufferKind.Color, checked((int)attachment), values);
    }

    private static void ClearColorSInt(
        ArbDirectStateAccess dsa, uint framebuffer, uint attachment, in GraphicsClearColorSInt color)
    {
        Span<int> values = stackalloc int[4] { color.Red, color.Green, color.Blue, color.Alpha };
        dsa.ClearNamedFramebuffer(framebuffer, BufferKind.Color, checked((int)attachment), values);
    }

    private static void ClearDepthStencil(
        ArbDirectStateAccess dsa, uint framebuffer, in OpenGLRecordedCommand command)
    {
        if (command.ClearDepthEnabled)
        {
            Span<float> depth = stackalloc float[1] { command.ClearDepth };
            dsa.ClearNamedFramebuffer(framebuffer, BufferKind.Depth, 0, depth);
        }

        if (command.ClearStencilEnabled)
        {
            Span<int> stencil = stackalloc int[1] { unchecked((int)command.ClearStencil) };
            dsa.ClearNamedFramebuffer(framebuffer, BufferKind.Stencil, 0, stencil);
        }
    }

    private GraphicsFormat GetColorTargetFormat(uint attachmentIndex)
    {
        if (attachmentIndex >= _recordedColorTargetCount)
            throw new GraphicsValidationException("The requested color attachment is not bound.");

        IGraphicsTextureView view = _renderTargetViews[
            _recordedTargetViewOffset + checked((int)attachmentIndex)];
        return view.Description.Format == GraphicsFormat.Unknown
            ? view.Texture.Description.Format
            : view.Description.Format;
    }

    private void EnsureGraphicsQueue()
    {
        if (_queue.QueueType != GraphicsQueueType.Graphics)
            throw new GraphicsValidationException("Render-target, viewport, scissor, and clear commands require the graphics queue.");
    }

    private static void ExecuteTextureCopy(
        ArbCopyImage copyImage,
        IGraphicsTexture source,
        IGraphicsTexture destination)
    {
        OpenGLGraphicsTexture src = (OpenGLGraphicsTexture)source;
        OpenGLGraphicsTexture dst = (OpenGLGraphicsTexture)destination;
        GraphicsTextureDescription description = source.Description;
        TextureTarget target = OpenGLTextureTargetMapping.ForTexture(description);
        uint depth = description.Dimension switch
        {
            GraphicsTextureDimension.Texture3D => description.Depth,
            GraphicsTextureDimension.Texture2D or GraphicsTextureDimension.Cube =>
                Math.Max(1u, description.ArrayLayers),
            _ => 1
        };

        for (uint mip = 0; mip < description.MipLevels; mip++)
        {
            uint width = Math.Max(1u, description.Width >> checked((int)mip));
            uint height = description.Dimension == GraphicsTextureDimension.Texture1D
                ? Math.Max(1u, description.ArrayLayers)
                : Math.Max(1u, description.Height >> checked((int)mip));
            uint mipDepth = description.Dimension == GraphicsTextureDimension.Texture3D
                ? Math.Max(1u, depth >> checked((int)mip))
                : depth;

            copyImage.CopyImageSubData(
                src.Handle, (CopyImageSubDataTarget)target, checked((int)mip), 0, 0, 0,
                dst.Handle, (CopyImageSubDataTarget)target, checked((int)mip), 0, 0, 0,
                width, height, mipDepth);
        }
    }

    internal void MarkCompletedAfterGpu() => _lifecycle.TryMarkCompleted();

    internal void ReleaseCompletedResourcesIfDisposed()
    {
        if (!IsDisposed || Interlocked.Exchange(ref _disposedCleanup, 1) != 0)
            return;

        if (_lifecycle.State != GraphicsCommandListState.Disposed)
            _lifecycle.MarkDisposed();
        ClearRecordedState();
    }

    internal void Complete()
    {
        _lifecycle.TryMarkCompleted();
        ReleaseCompletedResourcesIfDisposed();
    }

    internal void MarkSubmitted()
    {
        ThrowIfDisposed();
        _lifecycle.MarkSubmitted();
    }

    private void EnsureRecording()
    {
        ThrowIfDisposed();
        // Recording is deliberately CPU-only. Do not call the public State getter here:
        // that getter polls GLsync completion and makes the native context current.
        _lifecycle.EnsureRecording();
    }

    private void EnsureOwned(IGraphicsDeviceObject resource)
    {
        if (resource.IsDisposed || resource.Device.IsDisposed)
            throw new ObjectDisposedException(resource.GetType().Name);
        if (!ReferenceEquals(resource.Device, Device))
            throw new GraphicsValidationException("Command resources must belong to the command list's device.");

        if (resource is IOpenGLNativeResource nativeResource)
            _resourceRetention.Retain(nativeResource);
    }

    private static NotSupportedException Unsupported(string operation) =>
        new($"{operation} is not implemented by the current OpenGL command execution increment.");

    protected override void DisposeCore()
    {
        try
        {
            _queue.WaitForCompletion(this);
        }
        catch (ObjectDisposedException) when (Device.IsDisposed)
        {
            // Device shutdown may acquire the context gate after WaitForCompletion's initial
            // disposed check but before it activates the context. The shutdown barrier below waits
            // for its Finish/drain pass before allowing this list to release any references.
        }

        // Device shutdown drains queued submissions while the context is current. An exceptional
        // submission can have reached Pending before its completion record was enqueued; wait for
        // shutdown to finish and close that unqueued transition here if the queue did not.
        if (Device.IsDisposed)
        {
            ((OpenGLGraphicsDevice)Device).WaitForShutdownCompletion();
            if (_lifecycle.State == GraphicsCommandListState.Pending)
                _lifecycle.MarkCompleted();
        }

        // Disposing a recording abandons it; it must never become executable after Dispose.
        if (_lifecycle.State == GraphicsCommandListState.Recording)
            _lifecycle.End();
        ReleaseCompletedResourcesIfDisposed();
    }
}
