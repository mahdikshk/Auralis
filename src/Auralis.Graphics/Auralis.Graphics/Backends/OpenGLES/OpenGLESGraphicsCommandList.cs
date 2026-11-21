using System.Runtime.InteropServices;
using Auralis.Graphics.Abstractions;
using Auralis.Graphics.Commands;
using Auralis.Graphics.Backends.OpenGL;
using Auralis.Graphics.Devices;
using Auralis.Graphics.Pipelines;
using Auralis.Graphics.Resources;
using Auralis.Graphics.Synchronization;
using Silk.NET.OpenGLES;
using Silk.NET.OpenGLES.Extensions.EXT;

namespace Auralis.Graphics.Backends.OpenGLES;

internal enum OpenGLESCmdKind : byte
{
    Barrier,
    CopyBuffer,
    CopyTexture,
    RenderTargets,
    Viewport,
    Scissor,
    ClearColor,
    ClearColorUInt,
    ClearColorSInt,
    ClearDepthStencil,
    RenderPipeline,
    ComputePipeline,
    BindingSet,
    VertexBuffer,
    IndexBuffer,
    Draw,
    DrawIndexed,
    DrawIndirect,
    DrawIndexedIndirect,
    DrawMeshTasks,
    DrawMeshTasksIndirect,
    Dispatch,
    DispatchIndirect
}

internal struct OpenGLESCmd
{
    internal OpenGLESCmdKind Kind;
    internal IGraphicsBuffer? Buffer0;
    internal IGraphicsBuffer? Buffer1;
    internal IGraphicsTexture? Texture0;
    internal IGraphicsTexture? Texture1;
    internal IGraphicsTextureView? DepthTarget;
    internal int ViewOffset;
    internal int ColorCount;
    internal ulong Offset0;
    internal ulong Offset1;
    internal ulong Size;
    internal GraphicsViewport Viewport;
    internal GraphicsScissorRect Scissor;
    internal GraphicsClearColor ClearColor;
    internal GraphicsClearColorUInt ClearColorUInt;
    internal GraphicsClearColorSInt ClearColorSInt;
    internal float ClearDepth;
    internal uint ClearStencil;
    internal bool ClearDepthEnabled;
    internal bool ClearStencilEnabled;
    internal uint AttachmentIndex;
    internal OpenGLESGraphicsRenderPipeline? RenderPipeline;
    internal OpenGLESGraphicsComputePipeline? ComputePipeline;
    internal uint SetIndex;
    internal OpenGLESGraphicsBindingLayout? BindingLayout;
    internal OpenGLESGraphicsPipelineLayout? PipelineLayout;
    internal int BindingValueOffset;
    internal int BindingValueCount;
    internal uint VertexBinding;
    internal uint IndexFormat;
    internal uint VertexCount;
    internal uint InstanceCount;
    internal uint FirstVertex;
    internal uint FirstInstance;
    internal uint IndexCount;
    internal int BaseVertex;
    internal uint DrawCount;
    internal uint Stride;
    internal uint GroupsX;
    internal uint GroupsY;
    internal uint GroupsZ;
}

internal sealed unsafe class OpenGLESGraphicsCommandList : GraphicsDeviceObject, IGraphicsCommandList
{
    private static readonly TextureTarget[] s_textureTargets =
    [
        TextureTarget.Texture2D,
        TextureTarget.Texture2DArray,
        TextureTarget.Texture3D,
        TextureTarget.TextureCubeMap,
        TextureTarget.TextureCubeMapArray,
        TextureTarget.Texture2DMultisample
    ];

    private readonly OpenGLESGraphicsCommandQueue _queue;
    private readonly GraphicsCommandListLifecycle _lifecycle;
    private readonly List<OpenGLESCmd> _commands = new(48);
    private readonly List<IGraphicsTextureView> _renderTargetViews = new(4);
    private readonly List<OpenGLESBindingValue> _bindingValues = new(24);
    private readonly HashSet<IOpenGLESNativeResource> _retained = new(ReferenceEqualityComparer.Instance);
    private int _disposedCleanup;
    private OpenGLESGraphicsRenderPipeline? _renderPipeline;
    private OpenGLESGraphicsComputePipeline? _computePipeline;
    private OpenGLESGraphicsPipelineLayout? _pipelineLayout;
    private readonly HashSet<uint> _boundSets = [];
    private IGraphicsBuffer? _indexBuffer;
    private GraphicsIndexFormat _indexFormat;
    private ulong _indexOffset;
    private bool _hasTargets;
    private bool _hasViewport;
    private bool _hasDepthTarget;
    private GraphicsFormat _depthFormat;
    private uint _colorCount;
    private uint _targetSampleCount;
    private int _targetOffset;
    private int _recordingThreadId;

    internal OpenGLESGraphicsCommandList(
        OpenGLESGraphicsCommandQueue queue,
        GraphicsCommandListUsage usage) : base(queue.Device)
    {
        _queue = queue;
        Usage = usage;
        _lifecycle = new GraphicsCommandListLifecycle(queue.QueueType, usage);
    }

    public GraphicsQueueType QueueType => _queue.QueueType;
    public GraphicsCommandListUsage Usage { get; }
    public GraphicsCommandListState State => _lifecycle.State;

    internal GraphicsCommandListLifecycle Lifecycle => _lifecycle;
    internal ReadOnlySpan<OpenGLESCmd> Commands => CollectionsMarshal.AsSpan(_commands);
    internal ReadOnlySpan<IGraphicsTextureView> RenderTargetViews => CollectionsMarshal.AsSpan(_renderTargetViews);
    internal ReadOnlySpan<OpenGLESBindingValue> BindingValues => CollectionsMarshal.AsSpan(_bindingValues);

    public void Begin()
    {
        ThrowIfDisposed();
        _lifecycle.Begin();
        _recordingThreadId = Environment.CurrentManagedThreadId;
    }

    public void End()
    {
        EnsureRecording();
        _lifecycle.End();
        _recordingThreadId = 0;
    }

    public void Reset()
    {
        ThrowIfDisposed();
        _lifecycle.Reset();
        ClearRecordedState();
    }

    public void MemoryBarrier(ReadOnlySpan<GraphicsResourceBarrier> barriers)
    {
        EnsureRecording();
        if (barriers.IsEmpty)
            return;

        for (int i = 0; i < barriers.Length; i++)
        {
            GraphicsResourceBarrier barrier = barriers[i];
            barrier.Validate();
            EnsureOwned(barrier.Resource);
        }

        // GLES exposes a global memory barrier rather than resource/range-specific barriers. The
        // all-bits token is defined by GLES 3.1 and covers every common access transition.
        _commands.Add(new OpenGLESCmd { Kind = OpenGLESCmdKind.Barrier });
    }

    public void SetRenderTargets(
        ReadOnlySpan<IGraphicsTextureView> colorTargets,
        IGraphicsTextureView? depthStencilTarget = null)
    {
        EnsureRecording();
        EnsureGraphicsQueue();

        GraphicsRenderTargetExtent extent =
            GraphicsRenderTargetValidation.Validate(Device, colorTargets, depthStencilTarget);
        GraphicsDeviceLimits limits = Device.Capabilities.Limits;
        if ((limits.MaxColorAttachments != 0 && (uint)colorTargets.Length > limits.MaxColorAttachments) ||
            (limits.MaxColorDrawBuffers != 0 && (uint)colorTargets.Length > limits.MaxColorDrawBuffers))
            throw new GraphicsValidationException(
                $"The requested {colorTargets.Length} color attachments exceed the GLES limits " +
                $"(attachments: {limits.MaxColorAttachments}, draw buffers: {limits.MaxColorDrawBuffers}).");

        for (int i = 0; i < colorTargets.Length; i++)
        {
            ValidateRenderTarget(colorTargets[i], depthAttachment: false);
            EnsureOwned(colorTargets[i]);
        }

        if (depthStencilTarget is not null)
        {
            ValidateRenderTarget(depthStencilTarget, depthAttachment: true);
            EnsureOwned(depthStencilTarget);
            GraphicsFormat format = GetViewFormat(depthStencilTarget);
            if (format is not (GraphicsFormat.D16UNorm or GraphicsFormat.D24UNormS8UInt or
                GraphicsFormat.D32Float or GraphicsFormat.D32FloatS8UInt))
                throw new GraphicsValidationException($"Depth format {format} is not implemented for GLES render targets.");
        }

        int offset = _renderTargetViews.Count;
        for (int i = 0; i < colorTargets.Length; i++)
            _renderTargetViews.Add(colorTargets[i]);

        _commands.Add(new OpenGLESCmd
        {
            Kind = OpenGLESCmdKind.RenderTargets,
            ViewOffset = offset,
            ColorCount = colorTargets.Length,
            DepthTarget = depthStencilTarget
        });

        _targetOffset = offset;
        _colorCount = (uint)colorTargets.Length;
        _targetSampleCount = extent.SampleCount;
        _hasTargets = true;
        _hasDepthTarget = depthStencilTarget is not null;
        _depthFormat = depthStencilTarget is null ? GraphicsFormat.Unknown : GetViewFormat(depthStencilTarget);
    }

    public void SetViewport(in GraphicsViewport viewport)
    {
        EnsureRecording();
        EnsureGraphicsQueue();
        GraphicsViewport copy = viewport;
        copy.Validate();
        _hasViewport = true;
        _commands.Add(new OpenGLESCmd { Kind = OpenGLESCmdKind.Viewport, Viewport = copy });
    }

    public void SetScissor(in GraphicsScissorRect scissor)
    {
        EnsureRecording();
        EnsureGraphicsQueue();
        GraphicsScissorRect copy = scissor;
        copy.Validate();
        _commands.Add(new OpenGLESCmd { Kind = OpenGLESCmdKind.Scissor, Scissor = copy });
    }

    public void ClearColor(uint attachmentIndex, in GraphicsClearColor color)
    {
        EnsureRecording();
        EnsureGraphicsQueue();
        if (attachmentIndex >= _colorCount)
            throw new GraphicsValidationException("The requested color attachment is not bound.");
        if (IsIntegerColorFormat(GetBoundColorFormat(attachmentIndex)))
            throw new GraphicsValidationException(
                "Floating-point color clears cannot be used with integer color attachments.");
        GraphicsClearColor copy = color;
        copy.Validate();
        _commands.Add(new OpenGLESCmd
        {
            Kind = OpenGLESCmdKind.ClearColor,
            AttachmentIndex = attachmentIndex,
            ClearColor = copy
        });
    }

    public void ClearColorUInt(uint attachmentIndex, in GraphicsClearColorUInt color)
    {
        EnsureRecording();
        EnsureGraphicsQueue();
        EnsureColorAttachmentIndex(attachmentIndex);
        if (!IsUnsignedIntegerColorFormat(GetBoundColorFormat(attachmentIndex)))
            throw new GraphicsValidationException(
                "Unsigned-integer clears require an unsigned-integer color attachment.");

        _commands.Add(new OpenGLESCmd
        {
            Kind = OpenGLESCmdKind.ClearColorUInt,
            AttachmentIndex = attachmentIndex,
            ClearColorUInt = color
        });
    }

    public void ClearColorSInt(uint attachmentIndex, in GraphicsClearColorSInt color)
    {
        EnsureRecording();
        EnsureGraphicsQueue();
        EnsureColorAttachmentIndex(attachmentIndex);
        if (!IsSignedIntegerColorFormat(GetBoundColorFormat(attachmentIndex)))
            throw new GraphicsValidationException(
                "Signed-integer clears require a signed-integer color attachment.");

        _commands.Add(new OpenGLESCmd
        {
            Kind = OpenGLESCmdKind.ClearColorSInt,
            AttachmentIndex = attachmentIndex,
            ClearColorSInt = color
        });
    }

    public void ClearDepthStencil(
        float depth,
        uint stencil,
        bool clearDepth = true,
        bool clearStencil = false)
    {
        EnsureRecording();
        EnsureGraphicsQueue();
        if (!_hasDepthTarget)
            throw new GraphicsValidationException("A depth attachment must be selected before clearing depth.");
        if (!clearDepth && !clearStencil)
            throw new ArgumentException("At least one depth/stencil aspect must be cleared.");
        if (clearStencil && _depthFormat is not (GraphicsFormat.D24UNormS8UInt or GraphicsFormat.D32FloatS8UInt))
            throw new GraphicsValidationException(
                "A stencil clear requires a packed depth/stencil attachment format (D24UNormS8UInt or D32FloatS8UInt).");
        if (clearDepth && (!float.IsFinite(depth) || depth < 0 || depth > 1))
            throw new ArgumentOutOfRangeException(nameof(depth));

        _commands.Add(new OpenGLESCmd
        {
            Kind = OpenGLESCmdKind.ClearDepthStencil,
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
        if (pipeline is not OpenGLESGraphicsRenderPipeline glesPipeline)
            throw new GraphicsValidationException("The render pipeline belongs to another backend.");

        _renderPipeline = glesPipeline;
        _computePipeline = null;
        _pipelineLayout = (OpenGLESGraphicsPipelineLayout)glesPipeline.Layout;
        _boundSets.Clear();
        _indexBuffer = null;
        Retain(glesPipeline);
        _commands.Add(new OpenGLESCmd { Kind = OpenGLESCmdKind.RenderPipeline, RenderPipeline = glesPipeline });
    }

    public void SetComputePipeline(IGraphicsComputePipeline pipeline)
    {
        EnsureRecording();
        EnsureComputeQueue();
        ArgumentNullException.ThrowIfNull(pipeline);
        EnsureOwned(pipeline);
        if (pipeline is not OpenGLESGraphicsComputePipeline glesPipeline)
            throw new GraphicsValidationException("The compute pipeline belongs to another backend.");

        _computePipeline = glesPipeline;
        _renderPipeline = null;
        _pipelineLayout = (OpenGLESGraphicsPipelineLayout)glesPipeline.Layout;
        _boundSets.Clear();
        Retain(glesPipeline);
        _commands.Add(new OpenGLESCmd { Kind = OpenGLESCmdKind.ComputePipeline, ComputePipeline = glesPipeline });
    }

    public void SetBindingSet(uint setIndex, IGraphicsBindingSet bindingSet)
    {
        EnsureRecording();
        ArgumentNullException.ThrowIfNull(bindingSet);
        EnsureOwned(bindingSet);
        if (_pipelineLayout is null)
            throw new GraphicsValidationException("Select a pipeline before binding a descriptor set.");
        if (bindingSet is not OpenGLESGraphicsBindingSet glesSet)
            throw new GraphicsValidationException("The binding set belongs to another backend.");

        ReadOnlyMemory<IGraphicsBindingLayout> layouts = _pipelineLayout.BindingLayouts;
        if (setIndex >= (uint)layouts.Length ||
            !ReferenceEquals(layouts.Span[(int)setIndex], glesSet.Layout))
            throw new GraphicsValidationException($"Binding set {setIndex} does not match the active pipeline layout.");

        int offset = glesSet.SnapshotTo(_bindingValues);
        ReadOnlySpan<OpenGLESBindingValue> snapshot = CollectionsMarshal.AsSpan(_bindingValues).Slice(offset, glesSet.ValueCount);
        ValidateSnapshot((OpenGLESGraphicsBindingLayout)glesSet.Layout, snapshot);
        for (int i = 0; i < snapshot.Length; i++)
        {
            if (snapshot[i].Buffer is not null) EnsureOwned(snapshot[i].Buffer!);
            if (snapshot[i].Texture is not null) EnsureOwned(snapshot[i].Texture!);
            if (snapshot[i].Sampler is not null) EnsureOwned(snapshot[i].Sampler!);
        }

        _boundSets.Add(setIndex);
        _commands.Add(new OpenGLESCmd
        {
            Kind = OpenGLESCmdKind.BindingSet,
            SetIndex = setIndex,
            BindingLayout = (OpenGLESGraphicsBindingLayout)glesSet.Layout,
            PipelineLayout = _pipelineLayout,
            BindingValueOffset = offset,
            BindingValueCount = glesSet.ValueCount
        });
    }

    public void SetVertexBuffer(uint binding, IGraphicsBuffer buffer, ulong offset = 0)
    {
        EnsureRecording();
        EnsureGraphicsQueue();
        ArgumentNullException.ThrowIfNull(buffer);
        EnsureOwned(buffer);
        if (_renderPipeline?.Description.VertexInput is not { } layout)
            throw new GraphicsValidationException("The active pipeline has no vertex input layout.");
        if ((buffer.Description.Usage & GraphicsBufferUsage.Vertex) == 0)
            throw new GraphicsValidationException("The buffer was not created for vertex usage.");
        if (offset > buffer.Description.SizeInBytes)
            throw new ArgumentOutOfRangeException(nameof(offset));

        bool found = false;
        foreach (GraphicsVertexBufferLayout item in layout.Buffers)
            found |= item.Binding == binding;
        if (!found)
            throw new GraphicsValidationException($"Vertex binding {binding} is not declared by the pipeline.");

        _commands.Add(new OpenGLESCmd
        {
            Kind = OpenGLESCmdKind.VertexBuffer,
            VertexBinding = binding,
            Buffer0 = buffer,
            Offset0 = offset
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
        if ((buffer.Description.Usage & GraphicsBufferUsage.Index) == 0)
            throw new GraphicsValidationException("The buffer was not created for index usage.");

        uint elementSize = format == GraphicsIndexFormat.UInt16 ? 2u : 4u;
        if (offset > buffer.Description.SizeInBytes || offset % elementSize != 0)
            throw new ArgumentOutOfRangeException(nameof(offset));

        _indexBuffer = buffer;
        _indexFormat = format;
        _indexOffset = offset;
        _commands.Add(new OpenGLESCmd
        {
            Kind = OpenGLESCmdKind.IndexBuffer,
            Buffer0 = buffer,
            IndexFormat = (uint)format,
            Offset0 = offset
        });
    }

    public void Draw(uint vertexCount, uint instanceCount = 1, uint firstVertex = 0, uint firstInstance = 0)
    {
        EnsureRecording();
        EnsureGraphicsReady();
        if (firstInstance != 0 &&
            !Device.Capabilities.Features.Supports(GraphicsFeature.BaseInstanceDrawing))
            throw new GraphicsFeatureNotSupportedException(
                GraphicsFeature.BaseInstanceDrawing, GraphicsFeature.BaseInstanceDrawing);
        if (firstVertex > int.MaxValue)
            throw new GraphicsValidationException("The first vertex exceeds the GLES integer range.");
        if (instanceCount == 0 || vertexCount == 0)
            return;

        _commands.Add(new OpenGLESCmd
        {
            Kind = OpenGLESCmdKind.Draw,
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
        EnsureGraphicsReady();
        if (_indexBuffer is null)
            throw new GraphicsValidationException("Set an index buffer before issuing an indexed draw.");
        if (baseVertex != 0 &&
            !Device.Capabilities.Features.Supports(GraphicsFeature.BaseVertexDrawing))
            throw new GraphicsFeatureNotSupportedException(
                GraphicsFeature.BaseVertexDrawing, GraphicsFeature.BaseVertexDrawing);
        if (firstInstance != 0 &&
            !Device.Capabilities.Features.Supports(GraphicsFeature.BaseInstanceDrawing))
            throw new GraphicsFeatureNotSupportedException(
                GraphicsFeature.BaseInstanceDrawing, GraphicsFeature.BaseInstanceDrawing);

        uint indexSize = _indexFormat == GraphicsIndexFormat.UInt16 ? 2u : 4u;
        ulong byteOffset = checked(_indexOffset + (ulong)firstIndex * indexSize);
        ulong bytes = checked((ulong)indexCount * indexSize);
        if (byteOffset > _indexBuffer.Description.SizeInBytes ||
            bytes > _indexBuffer.Description.SizeInBytes - byteOffset)
            throw new GraphicsValidationException("The indexed draw reads past the index buffer.");
        if (instanceCount == 0 || indexCount == 0)
            return;

        _commands.Add(new OpenGLESCmd
        {
            Kind = OpenGLESCmdKind.DrawIndexed,
            Buffer0 = _indexBuffer,
            IndexFormat = (uint)_indexFormat,
            Offset0 = byteOffset,
            IndexCount = indexCount,
            InstanceCount = instanceCount,
            BaseVertex = baseVertex,
            FirstInstance = firstInstance
        });
    }

    public void DrawIndirect(IGraphicsBuffer arguments, ulong offset, uint drawCount = 1, uint stride = 0)
    {
        EnsureRecording();
        EnsureGraphicsReady();
        ValidateIndirect(arguments, offset, drawCount, 16, stride);
        _commands.Add(new OpenGLESCmd
        {
            Kind = OpenGLESCmdKind.DrawIndirect,
            Buffer0 = arguments,
            Offset0 = offset,
            DrawCount = drawCount,
            Stride = stride
        });
    }

    public void DrawIndexedIndirect(IGraphicsBuffer arguments, ulong offset, uint drawCount = 1, uint stride = 0)
    {
        EnsureRecording();
        EnsureGraphicsReady();
        if (_indexBuffer is null)
            throw new GraphicsValidationException("Set an index buffer before issuing indexed indirect drawing.");
        GraphicsCommandValidation.ValidateIndexedIndirectIndexBufferOffset(_indexOffset);
        ValidateIndirect(arguments, offset, drawCount, 20, stride);
        _commands.Add(new OpenGLESCmd
        {
            Kind = OpenGLESCmdKind.DrawIndexedIndirect,
            Buffer0 = arguments,
            Buffer1 = _indexBuffer,
            Offset0 = offset,
            IndexFormat = (uint)_indexFormat,
            DrawCount = drawCount,
            Stride = stride
        });
    }

    public void DrawMeshTasks(uint groupCountX, uint groupCountY = 1, uint groupCountZ = 1)
    {
        EnsureRecording();
        OpenGLESGraphicsRenderPipeline pipeline = EnsureMeshPipeline();
        OpenGLESGraphicsDevice device = _queue.GlesDevice;

        OpenGLMeshTaskValidation.Validate(
            groupCountX,
            groupCountY,
            groupCountZ,
            pipeline.IsTaskPipeline ? device.MaxTaskWorkGroupCountX : device.MaxMeshWorkGroupCountX,
            pipeline.IsTaskPipeline ? device.MaxTaskWorkGroupCountY : device.MaxMeshWorkGroupCountY,
            pipeline.IsTaskPipeline ? device.MaxTaskWorkGroupCountZ : device.MaxMeshWorkGroupCountZ,
            pipeline.IsTaskPipeline ? device.MaxTaskWorkGroupTotalCount : device.MaxMeshWorkGroupTotalCount,
            pipeline.IsTaskPipeline ? GraphicsFeature.TaskShaders : GraphicsFeature.MeshShaders);

        _commands.Add(new OpenGLESCmd
        {
            Kind = OpenGLESCmdKind.DrawMeshTasks,
            GroupsX = groupCountX,
            GroupsY = groupCountY,
            GroupsZ = groupCountZ
        });
    }

    public void DrawMeshTasksIndirect(IGraphicsBuffer arguments, ulong offset, uint drawCount = 1, uint stride = 0)
    {
        EnsureRecording();
        _ = EnsureMeshPipeline();
        ValidateIndirect(arguments, offset, drawCount, 12, stride);

        _commands.Add(new OpenGLESCmd
        {
            Kind = OpenGLESCmdKind.DrawMeshTasksIndirect,
            Buffer0 = arguments,
            Offset0 = offset,
            DrawCount = drawCount,
            Stride = stride
        });
    }

    internal static bool TryCreateDispatchCommand(
        uint groupCountX,
        uint groupCountY,
        uint groupCountZ,
        uint maxGroupCountX,
        uint maxGroupCountY,
        uint maxGroupCountZ,
        out OpenGLESCmd command)
    {
        OpenGLESComputeWorkGroupValidation.ValidateDispatch(
            groupCountX, groupCountY, groupCountZ,
            maxGroupCountX, maxGroupCountY, maxGroupCountZ);

        command = default;
        if (groupCountX == 0 || groupCountY == 0 || groupCountZ == 0)
            return false;

        command = new OpenGLESCmd
        {
            Kind = OpenGLESCmdKind.Dispatch,
            GroupsX = groupCountX,
            GroupsY = groupCountY,
            GroupsZ = groupCountZ
        };
        return true;
    }

    public void Dispatch(uint groupCountX, uint groupCountY = 1, uint groupCountZ = 1)
    {
        EnsureRecording();
        EnsureComputeReady();
        if (TryCreateDispatchCommand(
            groupCountX, groupCountY, groupCountZ,
            _queue.GlesDevice.MaxComputeWorkGroupCountX,
            _queue.GlesDevice.MaxComputeWorkGroupCountY,
            _queue.GlesDevice.MaxComputeWorkGroupCountZ,
            out OpenGLESCmd command))
        {
            _commands.Add(command);
        }
    }

    public void DispatchIndirect(IGraphicsBuffer arguments, ulong offset)
    {
        EnsureRecording();
        EnsureComputeReady();
        ArgumentNullException.ThrowIfNull(arguments);
        EnsureOwned(arguments);
        if ((arguments.Description.Usage & GraphicsBufferUsage.IndirectArguments) == 0)
            throw new GraphicsValidationException("Indirect dispatch requires IndirectArguments buffer usage.");
        if ((offset & 3) != 0 || offset > arguments.Description.SizeInBytes ||
            12UL > arguments.Description.SizeInBytes - offset)
            throw new GraphicsValidationException("Indirect dispatch arguments are unaligned or outside the buffer bounds.");
        _commands.Add(new OpenGLESCmd
        {
            Kind = OpenGLESCmdKind.DispatchIndirect,
            Buffer0 = arguments,
            Offset0 = offset
        });
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

        if ((source.Description.Usage & GraphicsBufferUsage.CopySource) == 0 ||
            (destination.Description.Usage & GraphicsBufferUsage.CopyDestination) == 0)
            throw new GraphicsValidationException("Buffer copy usage flags are missing.");
        new GraphicsBufferRange(sourceOffset, sizeInBytes).Validate(source.Description);
        new GraphicsBufferRange(destinationOffset, sizeInBytes).Validate(destination.Description);

        _commands.Add(new OpenGLESCmd
        {
            Kind = OpenGLESCmdKind.CopyBuffer,
            Buffer0 = source,
            Buffer1 = destination,
            Offset0 = sourceOffset,
            Offset1 = destinationOffset,
            Size = sizeInBytes
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
        OpenGLESTextureCompatibility.ValidateCopy(
            source.Description,
            destination.Description,
            _queue.GlesDevice.HasImageCopy);

        _commands.Add(new OpenGLESCmd
        {
            Kind = OpenGLESCmdKind.CopyTexture,
            Texture0 = source,
            Texture1 = destination
        });
    }

    internal void MarkSubmitted()
    {
        ThrowIfDisposed();
        _lifecycle.MarkSubmitted();
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

    internal void Execute(GL gl)
    {
        uint framebuffer = 0;
        uint readFramebuffer = 0;
        uint drawFramebuffer = 0;
        OpenGLESGraphicsRenderPipeline? activeRenderPipeline = null;
        OpenGLESGraphicsPipelineLayout? activePipelineLayout = null;
        Span<float> clearColorValues = stackalloc float[4];
        Span<uint> unsignedClearColorValues = stackalloc uint[4];
        Span<int> signedClearColorValues = stackalloc int[4];
        Span<float> depthClearValue = stackalloc float[1];
        Span<int> stencilClearValue = stackalloc int[1];
        try
        {
            for (int i = 0; i < _commands.Count; i++)
            {
                OpenGLESCmd command = _commands[i];
                switch (command.Kind)
                {
                    case OpenGLESCmdKind.Barrier:
                        gl.MemoryBarrier(unchecked((uint)MemoryBarrierMask.AllBarrierBits));
                        break;

                    case OpenGLESCmdKind.CopyBuffer:
                        gl.BindBuffer(BufferTargetARB.CopyReadBuffer, ((OpenGLESGraphicsBuffer)command.Buffer0!).Handle);
                        gl.BindBuffer(BufferTargetARB.CopyWriteBuffer, ((OpenGLESGraphicsBuffer)command.Buffer1!).Handle);
                        gl.CopyBufferSubData(
                            (GLEnum)BufferTargetARB.CopyReadBuffer,
                            (GLEnum)BufferTargetARB.CopyWriteBuffer,
                            checked((nint)command.Offset0),
                            checked((nint)command.Offset1),
                            checked((nuint)command.Size));
                        gl.BindBuffer(BufferTargetARB.CopyReadBuffer, 0);
                        gl.BindBuffer(BufferTargetARB.CopyWriteBuffer, 0);
                        break;

                    case OpenGLESCmdKind.CopyTexture:
                        if (_queue.GlesDevice.HasImageCopy)
                        {
                            OpenGLESGraphicsTexture sourceTexture = (OpenGLESGraphicsTexture)command.Texture0!;
                            OpenGLESGraphicsTexture destinationTexture = (OpenGLESGraphicsTexture)command.Texture1!;
                            GraphicsTextureDescription sourceDescription = sourceTexture.Description;

                            for (uint mip = 0; mip < sourceDescription.MipLevels; mip++)
                            {
                                uint width = Math.Max(1u, sourceDescription.Width >> checked((int)mip));
                                uint height = Math.Max(1u, sourceDescription.Height >> checked((int)mip));
                                uint depth = sourceDescription.Dimension switch
                                {
                                    GraphicsTextureDimension.Texture3D =>
                                        Math.Max(1u, sourceDescription.Depth >> checked((int)mip)),
                                    GraphicsTextureDimension.Texture2D when sourceDescription.ArrayLayers > 1 =>
                                        sourceDescription.ArrayLayers,
                                    GraphicsTextureDimension.Cube => sourceDescription.ArrayLayers,
                                    _ => 1u
                                };

                                if (_queue.GlesDevice.CopyImageApi is { } copyImage)
                                {
                                    copyImage.CopyImageSubData(
                                        sourceTexture.Handle,
                                        (EXT)sourceTexture.Target,
                                        checked((int)mip),
                                        0, 0, 0,
                                        destinationTexture.Handle,
                                        (EXT)destinationTexture.Target,
                                        checked((int)mip),
                                        0, 0, 0,
                                        width, height, depth);
                                }
                                else
                                {
                                    _queue.GlesDevice.CopyImageSubDataOes!(
                                        sourceTexture.Handle,
                                        sourceTexture.Target,
                                        checked((int)mip),
                                        0, 0, 0,
                                        destinationTexture.Handle,
                                        destinationTexture.Target,
                                        checked((int)mip),
                                        0, 0, 0,
                                        width, height, depth);
                                }
                            }
                            break;
                        }

                        readFramebuffer = gl.GenFramebuffer();
                        if (readFramebuffer == 0)
                            throw new GraphicsException("OpenGL ES failed to create the texture-copy read framebuffer.");
                        drawFramebuffer = gl.GenFramebuffer();
                        if (drawFramebuffer == 0)
                            throw new GraphicsException("OpenGL ES failed to create the texture-copy draw framebuffer.");
                        gl.BindFramebuffer(FramebufferTarget.ReadFramebuffer, readFramebuffer);
                        gl.FramebufferTexture2D(
                            FramebufferTarget.ReadFramebuffer,
                            GLEnum.ColorAttachment0,
                            TextureTarget.Texture2D,
                            ((OpenGLESGraphicsTexture)command.Texture0!).Handle,
                            0);
                        gl.BindFramebuffer(FramebufferTarget.DrawFramebuffer, drawFramebuffer);
                        gl.FramebufferTexture2D(
                            FramebufferTarget.DrawFramebuffer,
                            GLEnum.ColorAttachment0,
                            TextureTarget.Texture2D,
                            ((OpenGLESGraphicsTexture)command.Texture1!).Handle,
                            0);
                        GLEnum readStatus = gl.CheckFramebufferStatus(FramebufferTarget.ReadFramebuffer);
                        if (readStatus != GLEnum.FramebufferComplete)
                            throw new GraphicsException($"OpenGL ES texture-copy read framebuffer is incomplete: {readStatus}.");
                        GLEnum drawStatus = gl.CheckFramebufferStatus(FramebufferTarget.DrawFramebuffer);
                        if (drawStatus != GLEnum.FramebufferComplete)
                            throw new GraphicsException($"OpenGL ES texture-copy draw framebuffer is incomplete: {drawStatus}.");

                        uint copyWidth = command.Texture0!.Description.Width;
                        uint copyHeight = command.Texture0.Description.Height;
                        gl.BlitFramebuffer(
                            0, 0, (int)copyWidth, (int)copyHeight,
                            0, 0, (int)copyWidth, (int)copyHeight,
                            (uint)GLEnum.ColorBufferBit,
                            GLEnum.Nearest);
                        gl.DeleteFramebuffer(readFramebuffer);
                        gl.DeleteFramebuffer(drawFramebuffer);
                        readFramebuffer = drawFramebuffer = 0;
                        break;

                    case OpenGLESCmdKind.RenderTargets:
                        if (framebuffer != 0)
                            gl.DeleteFramebuffer(framebuffer);
                        framebuffer = CreateFramebuffer(gl, command);
                        gl.BindFramebuffer(FramebufferTarget.Framebuffer, framebuffer);
                        break;

                    case OpenGLESCmdKind.Viewport:
                        gl.Viewport(
                            checked((int)command.Viewport.X),
                            checked((int)command.Viewport.Y),
                            checked((uint)command.Viewport.Width),
                            checked((uint)command.Viewport.Height));
                        gl.DepthRange(command.Viewport.MinDepth, command.Viewport.MaxDepth);
                        break;

                    case OpenGLESCmdKind.Scissor:
                        gl.Enable(GLEnum.ScissorTest);
                        gl.Scissor(
                            command.Scissor.X,
                            command.Scissor.Y,
                            command.Scissor.Width,
                            command.Scissor.Height);
                        break;

                    case OpenGLESCmdKind.ClearColor:
                        gl.ColorMask(true, true, true, true);
                        clearColorValues[0] = command.ClearColor.Red;
                        clearColorValues[1] = command.ClearColor.Green;
                        clearColorValues[2] = command.ClearColor.Blue;
                        clearColorValues[3] = command.ClearColor.Alpha;
                        gl.ClearBuffer(GLEnum.Color, checked((int)command.AttachmentIndex), clearColorValues);
                        RestoreFramebufferWriteMasks(gl, activeRenderPipeline);
                        break;

                    case OpenGLESCmdKind.ClearColorUInt:
                        gl.ColorMask(true, true, true, true);
                        unsignedClearColorValues[0] = command.ClearColorUInt.Red;
                        unsignedClearColorValues[1] = command.ClearColorUInt.Green;
                        unsignedClearColorValues[2] = command.ClearColorUInt.Blue;
                        unsignedClearColorValues[3] = command.ClearColorUInt.Alpha;
                        gl.ClearBuffer(GLEnum.Color, checked((int)command.AttachmentIndex), unsignedClearColorValues);
                        RestoreFramebufferWriteMasks(gl, activeRenderPipeline);
                        break;

                    case OpenGLESCmdKind.ClearColorSInt:
                        gl.ColorMask(true, true, true, true);
                        signedClearColorValues[0] = command.ClearColorSInt.Red;
                        signedClearColorValues[1] = command.ClearColorSInt.Green;
                        signedClearColorValues[2] = command.ClearColorSInt.Blue;
                        signedClearColorValues[3] = command.ClearColorSInt.Alpha;
                        gl.ClearBuffer(GLEnum.Color, checked((int)command.AttachmentIndex), signedClearColorValues);
                        RestoreFramebufferWriteMasks(gl, activeRenderPipeline);
                        break;

                    case OpenGLESCmdKind.ClearDepthStencil:
                        gl.DepthMask(true);
                        gl.StencilMaskSeparate(GLEnum.Front, uint.MaxValue);
                        gl.StencilMaskSeparate(GLEnum.Back, uint.MaxValue);
                        if (command.ClearDepthEnabled)
                        {
                            depthClearValue[0] = command.ClearDepth;
                            gl.ClearBuffer(GLEnum.Depth, 0, depthClearValue);
                        }
                        if (command.ClearStencilEnabled)
                        {
                            stencilClearValue[0] = unchecked((int)command.ClearStencil);
                            gl.ClearBuffer(GLEnum.Stencil, 0, stencilClearValue);
                        }
                        RestoreFramebufferWriteMasks(gl, activeRenderPipeline);
                        break;

                    case OpenGLESCmdKind.RenderPipeline:
                        if (activePipelineLayout is not null)
                            UnbindLayoutResources(gl, activePipelineLayout);
                        activeRenderPipeline = command.RenderPipeline!;
                        activePipelineLayout = (OpenGLESGraphicsPipelineLayout)activeRenderPipeline.Layout;
                        activeRenderPipeline.BindPipelineState(gl);
                        break;

                    case OpenGLESCmdKind.ComputePipeline:
                        if (activePipelineLayout is not null)
                            UnbindLayoutResources(gl, activePipelineLayout);
                        activeRenderPipeline = null;
                        activePipelineLayout = (OpenGLESGraphicsPipelineLayout)command.ComputePipeline!.Layout;
                        command.ComputePipeline.BindPipelineState(gl);
                        break;

                    case OpenGLESCmdKind.BindingSet:
                        BindSet(gl, command);
                        break;

                    case OpenGLESCmdKind.VertexBuffer:
                        (activeRenderPipeline ?? throw new GraphicsValidationException(
                            "A render pipeline must be bound before configuring vertex input.")).ConfigureVertexBinding(
                            gl,
                            command.VertexBinding,
                            ((OpenGLESGraphicsBuffer)command.Buffer0!).Handle,
                            command.Offset0,
                            GetVertexStride(activeRenderPipeline.Description.VertexInput!, command.VertexBinding));
                        break;

                    case OpenGLESCmdKind.IndexBuffer:
                        gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, ((OpenGLESGraphicsBuffer)command.Buffer0!).Handle);
                        break;

                    case OpenGLESCmdKind.Draw:
                    {
                        PrimitiveType primitive = GetPrimitive((activeRenderPipeline ?? throw new GraphicsValidationException(
                            "A render pipeline must be bound before drawing.")).Description.Topology);
                        if (command.FirstInstance != 0)
                        {
                            (_queue.GlesDevice.BaseInstanceApi ?? throw new GraphicsFeatureNotSupportedException(
                                GraphicsFeature.BaseInstanceDrawing, GraphicsFeature.BaseInstanceDrawing))
                                .DrawArraysInstancedBaseInstance(
                                    primitive,
                                    checked((int)command.FirstVertex),
                                    command.VertexCount,
                                    command.InstanceCount,
                                    command.FirstInstance);
                        }
                        else
                        {
                            gl.DrawArraysInstanced(
                                primitive,
                                checked((int)command.FirstVertex),
                                command.VertexCount,
                                command.InstanceCount);
                        }
                        break;
                    }

                    case OpenGLESCmdKind.DrawIndexed:
                    {
                        gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, ((OpenGLESGraphicsBuffer)command.Buffer0!).Handle);
                        PrimitiveType primitive = GetPrimitive((activeRenderPipeline ?? throw new GraphicsValidationException(
                            "A render pipeline must be bound before drawing.")).Description.Topology);
                        DrawElementsType indexType = command.IndexFormat == (uint)GraphicsIndexFormat.UInt16
                            ? DrawElementsType.UnsignedShort
                            : DrawElementsType.UnsignedInt;
                        void* indices = (void*)checked((nint)command.Offset0);

                        if (command.BaseVertex != 0 && command.FirstInstance != 0)
                        {
                            (_queue.GlesDevice.BaseInstanceApi ?? throw new GraphicsFeatureNotSupportedException(
                                GraphicsFeature.BaseInstanceDrawing, GraphicsFeature.BaseInstanceDrawing))
                                .DrawElementsInstancedBaseVertexBaseInstance(
                                    primitive, command.IndexCount, indexType, indices,
                                    command.InstanceCount, command.BaseVertex, command.FirstInstance);
                        }
                        else if (command.BaseVertex != 0)
                        {
                            (_queue.GlesDevice.DrawElementsBaseVertexApi ?? throw new GraphicsFeatureNotSupportedException(
                                GraphicsFeature.BaseVertexDrawing, GraphicsFeature.BaseVertexDrawing))
                                .DrawElementsInstancedBaseVertex(
                                    primitive, command.IndexCount, indexType, indices,
                                    command.InstanceCount, command.BaseVertex);
                        }
                        else if (command.FirstInstance != 0)
                        {
                            (_queue.GlesDevice.BaseInstanceApi ?? throw new GraphicsFeatureNotSupportedException(
                                GraphicsFeature.BaseInstanceDrawing, GraphicsFeature.BaseInstanceDrawing))
                                .DrawElementsInstancedBaseInstance(
                                    primitive, command.IndexCount, indexType, indices,
                                    command.InstanceCount, command.FirstInstance);
                        }
                        else
                        {
                            gl.DrawElementsInstanced(
                                primitive, command.IndexCount, indexType, indices, command.InstanceCount);
                        }
                        break;
                    }

                    case OpenGLESCmdKind.DrawIndirect:
                        gl.BindBuffer(BufferTargetARB.DrawIndirectBuffer, ((OpenGLESGraphicsBuffer)command.Buffer0!).Handle);
                        PrimitiveType indirectPrimitive = GetPrimitive((activeRenderPipeline ?? throw new GraphicsValidationException(
                            "A render pipeline must be bound before drawing.")).Description.Topology);
                        OpenGLESGraphicsDevice.MultiDrawArraysIndirectExtDelegate? multiDraw =
                            _queue.GlesDevice.MultiDrawArraysIndirectExt;
                        if (command.DrawCount > 1 && multiDraw is not null)
                        {
                            multiDraw(
                                indirectPrimitive,
                                (void*)checked((nint)command.Offset0),
                                command.DrawCount,
                                command.Stride);
                        }
                        else
                        {
                            uint arrayStride = command.Stride == 0 ? 16u : command.Stride;
                            for (uint drawIndex = 0; drawIndex < command.DrawCount; drawIndex++)
                            {
                                ulong argumentOffset = checked(command.Offset0 + checked((ulong)drawIndex * arrayStride));
                                gl.DrawArraysIndirect(indirectPrimitive, (void*)checked((nint)argumentOffset));
                            }
                        }
                        gl.BindBuffer(BufferTargetARB.DrawIndirectBuffer, 0);
                        break;

                    case OpenGLESCmdKind.DrawIndexedIndirect:
                        gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, ((OpenGLESGraphicsBuffer)command.Buffer1!).Handle);
                        gl.BindBuffer(BufferTargetARB.DrawIndirectBuffer, ((OpenGLESGraphicsBuffer)command.Buffer0!).Handle);
                        PrimitiveType indexedIndirectPrimitive = GetPrimitive((activeRenderPipeline ?? throw new GraphicsValidationException(
                            "A render pipeline must be bound before drawing.")).Description.Topology);
                        DrawElementsType indexedType = command.IndexFormat == (uint)GraphicsIndexFormat.UInt16
                            ? DrawElementsType.UnsignedShort
                            : DrawElementsType.UnsignedInt;
                        OpenGLESGraphicsDevice.MultiDrawElementsIndirectExtDelegate? indexedMultiDraw =
                            _queue.GlesDevice.MultiDrawElementsIndirectExt;
                        if (command.DrawCount > 1 && indexedMultiDraw is not null)
                        {
                            indexedMultiDraw(
                                indexedIndirectPrimitive,
                                indexedType,
                                (void*)checked((nint)command.Offset0),
                                command.DrawCount,
                                command.Stride);
                        }
                        else
                        {
                            uint indexedStride = command.Stride == 0 ? 20u : command.Stride;
                            for (uint drawIndex = 0; drawIndex < command.DrawCount; drawIndex++)
                            {
                                ulong argumentOffset = checked(command.Offset0 + checked((ulong)drawIndex * indexedStride));
                                gl.DrawElementsIndirect(
                                    indexedIndirectPrimitive,
                                    indexedType,
                                    (void*)checked((nint)argumentOffset));
                            }
                        }
                        gl.BindBuffer(BufferTargetARB.DrawIndirectBuffer, 0);
                        break;

                    case OpenGLESCmdKind.DrawMeshTasks:
                        (_queue.GlesDevice.DrawMeshTasksExt ?? throw new GraphicsFeatureNotSupportedException(
                            GraphicsFeature.MeshShaders, GraphicsFeature.MeshShaders))(
                            command.GroupsX, command.GroupsY, command.GroupsZ);
                        break;

                    case OpenGLESCmdKind.DrawMeshTasksIndirect:
                    {
                        OpenGLESGraphicsDevice meshDevice = _queue.GlesDevice;
                        OpenGLESGraphicsDevice.DrawMeshTasksIndirectExtDelegate drawIndirect =
                            meshDevice.DrawMeshTasksIndirectExt ??
                            throw new GraphicsFeatureNotSupportedException(
                                GraphicsFeature.MeshShaders, GraphicsFeature.MeshShaders);
                        gl.BindBuffer(BufferTargetARB.DrawIndirectBuffer, ((OpenGLESGraphicsBuffer)command.Buffer0!).Handle);
                        if (command.DrawCount > 1 && meshDevice.MultiDrawMeshTasksIndirectExt is { } meshMultiDraw)
                        {
                            meshMultiDraw(
                                checked((nint)command.Offset0),
                                command.DrawCount,
                                command.Stride);
                        }
                        else
                        {
                            uint meshStride = command.Stride == 0 ? 12u : command.Stride;
                            for (uint drawIndex = 0; drawIndex < command.DrawCount; drawIndex++)
                            {
                                ulong argumentOffset = checked(command.Offset0 + checked((ulong)drawIndex * meshStride));
                                drawIndirect(checked((nint)argumentOffset));
                            }
                        }
                        gl.BindBuffer(BufferTargetARB.DrawIndirectBuffer, 0);
                        break;
                    }

                    case OpenGLESCmdKind.Dispatch:
                        gl.DispatchCompute(command.GroupsX, command.GroupsY, command.GroupsZ);
                        break;

                    case OpenGLESCmdKind.DispatchIndirect:
                        gl.BindBuffer(BufferTargetARB.DispatchIndirectBuffer, ((OpenGLESGraphicsBuffer)command.Buffer0!).Handle);
                        gl.DispatchComputeIndirect(checked((nint)command.Offset0));
                        gl.BindBuffer(BufferTargetARB.DispatchIndirectBuffer, 0);
                        break;
                }
            }
        }
        finally
        {
            if (framebuffer != 0)
                gl.DeleteFramebuffer(framebuffer);
            if (readFramebuffer != 0)
                gl.DeleteFramebuffer(readFramebuffer);
            if (drawFramebuffer != 0)
                gl.DeleteFramebuffer(drawFramebuffer);
            gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            gl.BindBuffer(BufferTargetARB.CopyReadBuffer, 0);
            gl.BindBuffer(BufferTargetARB.CopyWriteBuffer, 0);
            gl.BindBuffer(BufferTargetARB.DrawIndirectBuffer, 0);
            gl.BindBuffer(BufferTargetARB.DispatchIndirectBuffer, 0);
            gl.Disable(GLEnum.ScissorTest);
        }
    }


    private static void RestoreFramebufferWriteMasks(
        GL gl,
        OpenGLESGraphicsRenderPipeline? activeRenderPipeline)
    {
        if (activeRenderPipeline is null)
        {
            gl.ColorMask(true, true, true, true);
            gl.DepthMask(true);
            gl.StencilMaskSeparate(GLEnum.Front, uint.MaxValue);
            gl.StencilMaskSeparate(GLEnum.Back, uint.MaxValue);
            return;
        }

        ReadOnlySpan<GraphicsBlendAttachmentState> blendAttachments =
            activeRenderPipeline.Description.Blend.Attachments;
        GraphicsColorWriteMask colorMask = blendAttachments.IsEmpty
            ? GraphicsColorWriteMask.All
            : blendAttachments[0].WriteMask;
        gl.ColorMask(
            (colorMask & GraphicsColorWriteMask.Red) != 0,
            (colorMask & GraphicsColorWriteMask.Green) != 0,
            (colorMask & GraphicsColorWriteMask.Blue) != 0,
            (colorMask & GraphicsColorWriteMask.Alpha) != 0);

        GraphicsDepthStencilState depthStencil = activeRenderPipeline.Description.DepthStencil;
        gl.DepthMask(depthStencil.DepthWriteEnabled);
        if (depthStencil.StencilTestEnabled)
        {
            gl.StencilMaskSeparate(GLEnum.Front, depthStencil.FrontStencil.WriteMask);
            gl.StencilMaskSeparate(GLEnum.Back, depthStencil.BackStencil.WriteMask);
        }
        else
        {
            gl.StencilMaskSeparate(GLEnum.Front, uint.MaxValue);
            gl.StencilMaskSeparate(GLEnum.Back, uint.MaxValue);
        }
    }

    private void BindSet(GL gl, in OpenGLESCmd command)
    {
        if (command.PipelineLayout is null || command.BindingLayout is null)
            throw new GraphicsValidationException("The recorded GLES descriptor set has no pipeline layout.");

        ReadOnlySpan<OpenGLESBindingValue> values = CollectionsMarshal.AsSpan(_bindingValues)
            .Slice(command.BindingValueOffset, command.BindingValueCount);
        ReadOnlySpan<GraphicsBindingLayoutEntry> entries = command.BindingLayout.Entries;
        int cursor = 0;
        for (int i = 0; i < entries.Length; i++)
        {
            GraphicsBindingLayoutEntry entry = entries[i];
            if (!command.PipelineLayout.TryGetMapping(command.SetIndex, entry.Binding, out OpenGLESDescriptorMapping mapping))
                throw new GraphicsValidationException("The active GLES pipeline layout is missing a binding mapping.");

            for (uint element = 0; element < entry.Count; element++)
            {
                OpenGLESBindingValue value = values[cursor++];
                uint slot = mapping.BaseSlot + element;
                switch (entry.Type)
                {
                    case GraphicsDescriptorType.UniformBuffer:
                        if (value.Buffer is null) { gl.BindBufferBase(BufferTargetARB.UniformBuffer, slot, 0); break; }
                        gl.BindBufferRange(
                            BufferTargetARB.UniformBuffer, slot,
                            ((OpenGLESGraphicsBuffer)value.Buffer).Handle,
                            checked((nint)value.Offset), checked((nuint)value.Range));
                        break;

                    case GraphicsDescriptorType.StorageBuffer:
                        if (value.Buffer is null) { gl.BindBufferBase(BufferTargetARB.ShaderStorageBuffer, slot, 0); break; }
                        gl.BindBufferRange(
                            BufferTargetARB.ShaderStorageBuffer, slot,
                            ((OpenGLESGraphicsBuffer)value.Buffer).Handle,
                            checked((nint)value.Offset), checked((nuint)value.Range));
                        break;

                    case GraphicsDescriptorType.SampledTexture:
                    case GraphicsDescriptorType.Sampler:
                    case GraphicsDescriptorType.CombinedTextureSampler:
                        BindTextureUnit(
                            gl,
                            slot,
                            value.Texture,
                            value.Sampler is null ? 0 : ((OpenGLESGraphicsSampler)value.Sampler).Handle);
                        break;

                    case GraphicsDescriptorType.StorageTexture:
                        if (value.Texture is null)
                        {
                            gl.BindImageTexture(slot, 0, 0, false, 0, (GLEnum)BufferAccessARB.ReadWrite, (GLEnum)SizedInternalFormat.Rgba8);
                            break;
                        }
                        OpenGLESGraphicsTextureView storageView = (OpenGLESGraphicsTextureView)value.Texture;
                        bool layered = storageView.Target is
                            TextureTarget.Texture2DArray or TextureTarget.Texture3D or
                        TextureTarget.TextureCubeMap or TextureTarget.TextureCubeMapArray;
                        gl.BindImageTexture(
                            slot,
                            storageView.Handle,
                            0,
                            layered,
                            0,
                            (GLEnum)BufferAccessARB.ReadWrite,
                            (GLEnum)OpenGLESGraphicsTexture.GetInternalFormat(GetViewFormat(storageView)));
                        break;

                    default:
                        throw new GraphicsValidationException($"Unsupported GLES descriptor type {entry.Type}.");
                }
            }
        }
    }

    private static void BindTextureUnit(
        GL gl,
        uint slot,
        IGraphicsTextureView? texture,
        uint sampler)
    {
        gl.ActiveTexture((TextureUnit)(0x84C0 + slot));
        OpenGLESGraphicsTextureView? view = texture as OpenGLESGraphicsTextureView;
        for (int i = 0; i < s_textureTargets.Length; i++)
        {
            uint handle = view is not null && view.Target == s_textureTargets[i]
                ? view.Handle
                : 0;
            gl.BindTexture(s_textureTargets[i], handle);
        }

        gl.BindSampler(slot, sampler);
    }

    private static void UnbindLayoutResources(GL gl, OpenGLESGraphicsPipelineLayout layout)
    {
        ReadOnlySpan<OpenGLESDescriptorMapping> mappings = layout.Mappings;
        ReadOnlyMemory<IGraphicsBindingLayout> bindingLayouts = layout.BindingLayouts;
        for (int i = 0; i < mappings.Length; i++)
        {
            OpenGLESDescriptorMapping mapping = mappings[i];
            OpenGLESGraphicsBindingLayout bindingLayout =
                (OpenGLESGraphicsBindingLayout)bindingLayouts.Span[(int)mapping.SetIndex];
            if (!bindingLayout.TryGetEntry(mapping.Binding, out GraphicsBindingLayoutEntry entry, out _))
                continue;

            for (uint element = 0; element < entry.Count; element++)
            {
                uint slot = mapping.BaseSlot + element;
                switch (mapping.Type)
                {
                    case GraphicsDescriptorType.UniformBuffer:
                        gl.BindBufferBase(BufferTargetARB.UniformBuffer, slot, 0);
                        break;
                    case GraphicsDescriptorType.StorageBuffer:
                        gl.BindBufferBase(BufferTargetARB.ShaderStorageBuffer, slot, 0);
                        break;
                    case GraphicsDescriptorType.SampledTexture:
                    case GraphicsDescriptorType.Sampler:
                    case GraphicsDescriptorType.CombinedTextureSampler:
                        BindTextureUnit(gl, slot, null, 0);
                        break;
                    case GraphicsDescriptorType.StorageTexture:
                        gl.BindImageTexture(
                            slot,
                            0,
                            0,
                            false,
                            0,
                            (GLEnum)BufferAccessARB.ReadWrite,
                            (GLEnum)SizedInternalFormat.Rgba8);
                        break;
                }
            }
        }
    }

    private uint CreateFramebuffer(GL gl, in OpenGLESCmd command)
    {
        uint framebuffer = gl.GenFramebuffer();
        if (framebuffer == 0)
            throw new GraphicsException("OpenGL ES failed to create a framebuffer.");
        try
        {
            gl.BindFramebuffer(FramebufferTarget.Framebuffer, framebuffer);
            Span<GLEnum> drawBuffers = stackalloc GLEnum[Math.Max(1, command.ColorCount)];
            for (int i = 0; i < command.ColorCount; i++)
            {
                OpenGLESGraphicsTextureView textureView =
                    (OpenGLESGraphicsTextureView)_renderTargetViews[command.ViewOffset + i];
                if (textureView.Target is not (TextureTarget.Texture2D or TextureTarget.Texture2DMultisample))
                    throw new GraphicsValidationException("GLES framebuffer color views must be 2D or 2D-multisample.");
                GLEnum attachment = (GLEnum)((uint)GLEnum.ColorAttachment0 + (uint)i);
                gl.FramebufferTexture2D(
                    FramebufferTarget.Framebuffer,
                    attachment,
                    textureView.Target,
                    textureView.Handle,
                    0);
                drawBuffers[i] = attachment;
            }

            if (command.ColorCount == 0)
            {
                drawBuffers[0] = GLEnum.None;
                gl.DrawBuffers(drawBuffers[..1]);
                gl.ReadBuffer(GLEnum.None);
            }
            else
            {
                gl.DrawBuffers(drawBuffers[..command.ColorCount]);
                gl.ReadBuffer(GLEnum.ColorAttachment0);
            }

            if (command.DepthTarget is not null)
            {
                OpenGLESGraphicsTextureView depthView =
                    (OpenGLESGraphicsTextureView)command.DepthTarget;
                if (depthView.Target is not (TextureTarget.Texture2D or TextureTarget.Texture2DMultisample))
                    throw new GraphicsValidationException("GLES framebuffer depth views must be 2D or 2D-multisample.");
                GraphicsFormat format = GetViewFormat(command.DepthTarget);
                GLEnum attachment = format is GraphicsFormat.D24UNormS8UInt or GraphicsFormat.D32FloatS8UInt
                    ? GLEnum.DepthStencilAttachment
                    : GLEnum.DepthAttachment;
                gl.FramebufferTexture2D(
                    FramebufferTarget.Framebuffer,
                    attachment,
                    depthView.Target,
                    depthView.Handle,
                    0);
            }

            GLEnum status = gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
            if (status != GLEnum.FramebufferComplete)
                throw new GraphicsException($"OpenGL ES framebuffer is incomplete: {status}.");
            return framebuffer;
        }
        catch
        {
            gl.DeleteFramebuffer(framebuffer);
            throw;
        }
    }

    private static void ValidateSnapshot(
        OpenGLESGraphicsBindingLayout layout,
        ReadOnlySpan<OpenGLESBindingValue> values)
    {
        ReadOnlySpan<GraphicsBindingLayoutEntry> entries = layout.Entries;
        int offset = 0;
        for (int i = 0; i < entries.Length; i++)
        {
            GraphicsBindingLayoutEntry entry = entries[i];
            for (uint element = 0; element < entry.Count; element++)
            {
                OpenGLESBindingValue value = values[offset++];
                bool complete = entry.Type switch
                {
                    GraphicsDescriptorType.UniformBuffer or GraphicsDescriptorType.StorageBuffer => value.Buffer is not null,
                    GraphicsDescriptorType.SampledTexture or GraphicsDescriptorType.StorageTexture => value.Texture is not null,
                    GraphicsDescriptorType.Sampler => value.Sampler is not null,
                    GraphicsDescriptorType.CombinedTextureSampler => value.Texture is not null && value.Sampler is not null,
                    _ => false
                };
                bool empty = entry.Type switch
                {
                    GraphicsDescriptorType.UniformBuffer or GraphicsDescriptorType.StorageBuffer => value.Buffer is null,
                    GraphicsDescriptorType.SampledTexture or GraphicsDescriptorType.StorageTexture => value.Texture is null,
                    GraphicsDescriptorType.Sampler => value.Sampler is null,
                    GraphicsDescriptorType.CombinedTextureSampler => value.Texture is null && value.Sampler is null,
                    _ => false
                };
                if (!complete && !((entry.Flags & GraphicsBindingFlags.PartiallyBound) != 0 && empty))
                    throw new GraphicsValidationException(
                        $"GLES descriptor {entry.Binding}[{element}] is incomplete.");
            }
        }
    }

    private void ValidateIndirect(
        IGraphicsBuffer arguments,
        ulong offset,
        uint drawCount,
        uint commandSize,
        uint stride)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        EnsureOwned(arguments);
        if ((arguments.Description.Usage & GraphicsBufferUsage.IndirectArguments) == 0)
            throw new GraphicsValidationException("Indirect commands require IndirectArguments buffer usage.");
        if (drawCount == 0)
            throw new ArgumentOutOfRangeException(nameof(drawCount));
        if ((offset & 3) != 0)
            throw new GraphicsValidationException("The indirect command offset must be four-byte aligned.");
        if (stride != 0 && (stride < commandSize || (stride & 3) != 0))
            throw new GraphicsValidationException(
                $"Indirect command stride must be zero or a four-byte-aligned value at least {commandSize} bytes.");
        uint effectiveStride = stride == 0 ? commandSize : stride;
        ulong lastCommandOffset = checked(offset + checked((ulong)(drawCount - 1) * effectiveStride));
        ulong requiredEnd = checked(lastCommandOffset + commandSize);
        if (requiredEnd > arguments.Description.SizeInBytes || lastCommandOffset > (ulong)nint.MaxValue)
            throw new GraphicsValidationException("The indirect command sequence reads outside the argument buffer or native offset range.");
    }

    private void ValidateRenderTarget(
        IGraphicsTextureView view,
        bool depthAttachment)
    {
        ArgumentNullException.ThrowIfNull(view);
        EnsureOwned(view);
        if (view.Texture is not OpenGLESGraphicsTexture texture ||
            view is not OpenGLESGraphicsTextureView textureView)
            throw new GraphicsValidationException("The render attachment is not an OpenGL ES texture view.");
        if (textureView.Target is not (TextureTarget.Texture2D or TextureTarget.Texture2DMultisample))
            throw new GraphicsValidationException(
                "GLES framebuffer attachments require 2D or 2D-multisample textures.");

        GraphicsFormat format = GetViewFormat(view);
        if (depthAttachment)
        {
            if ((texture.Description.Usage & GraphicsTextureUsage.DepthStencilAttachment) == 0 ||
                format is not (GraphicsFormat.D16UNorm or GraphicsFormat.D24UNormS8UInt or
                    GraphicsFormat.D32Float or GraphicsFormat.D32FloatS8UInt))
                throw new GraphicsValidationException(
                    "Depth targets require DepthStencilAttachment usage and a supported depth format.");
        }
        else
        {
            if ((texture.Description.Usage & GraphicsTextureUsage.ColorAttachment) == 0 ||
                !Device.Capabilities.SupportsFormat(format, GraphicsFormatSupport.ColorAttachment))
                throw new GraphicsValidationException(
                    $"Color target format {format} lacks ColorAttachment usage or GLES color-renderable support.");
        }

    }

    private void EnsureColorAttachmentIndex(uint attachmentIndex)
    {
        if (attachmentIndex >= _colorCount)
            throw new GraphicsValidationException("The requested color attachment is not bound.");
    }

    private GraphicsFormat GetBoundColorFormat(uint attachmentIndex) =>
        GetViewFormat(_renderTargetViews[checked(_targetOffset + (int)attachmentIndex)]);

    private static bool IsUnsignedIntegerColorFormat(GraphicsFormat format) =>
        format is GraphicsFormat.R8UInt or GraphicsFormat.RG8UInt or GraphicsFormat.RGBA8UInt;

    private static bool IsSignedIntegerColorFormat(GraphicsFormat format) =>
        format is GraphicsFormat.R8SInt or GraphicsFormat.RG8SInt or GraphicsFormat.RGBA8SInt;

    private static bool IsIntegerColorFormat(GraphicsFormat format) =>
        IsUnsignedIntegerColorFormat(format) || IsSignedIntegerColorFormat(format);

    private OpenGLESGraphicsRenderPipeline EnsureMeshPipeline()
    {
        EnsureGraphicsReady();
        OpenGLESGraphicsRenderPipeline pipeline = _renderPipeline!;
        if (!pipeline.IsMeshPipeline)
            throw new GraphicsValidationException("Mesh-task drawing requires an active mesh render pipeline.");
        if (!_queue.GlesDevice.Capabilities.Features.Supports(GraphicsFeature.MeshShaders))
            throw new GraphicsFeatureNotSupportedException(GraphicsFeature.MeshShaders, GraphicsFeature.MeshShaders);
        if (pipeline.IsTaskPipeline &&
            !_queue.GlesDevice.Capabilities.Features.Supports(GraphicsFeature.TaskShaders))
            throw new GraphicsFeatureNotSupportedException(GraphicsFeature.TaskShaders, GraphicsFeature.TaskShaders);
        return pipeline;
    }

    private void EnsureGraphicsReady()
    {
        EnsureGraphicsQueue();
        if (_renderPipeline is null || !_hasTargets || !_hasViewport)
            throw new GraphicsValidationException("A render pipeline, target and viewport must be selected before drawing.");

        ReadOnlySpan<GraphicsFormat> pipelineColorFormats = _renderPipeline.Description.ColorFormats;
        if (pipelineColorFormats.Length != _colorCount)
            throw new GraphicsValidationException(
                $"The active render pipeline declares {pipelineColorFormats.Length} color target(s), " +
                $"but {_colorCount} color attachment(s) are bound.");
        for (int i = 0; i < pipelineColorFormats.Length; i++)
        {
            IGraphicsTextureView target = _renderTargetViews[_targetOffset + i];
            if (GetViewFormat(target) != pipelineColorFormats[i])
                throw new GraphicsValidationException(
                    $"Color attachment {i} format {GetViewFormat(target)} does not match pipeline format {pipelineColorFormats[i]}.");
        }

        if (_targetSampleCount != _renderPipeline.Description.SampleCount)
            throw new GraphicsValidationException(
                $"Bound render targets use {_targetSampleCount} sample(s), but the pipeline uses {_renderPipeline.Description.SampleCount}.");

        if ((_renderPipeline.Description.DepthStencilFormat == GraphicsFormat.Unknown) != !_hasDepthTarget)
            throw new GraphicsValidationException("The bound depth/stencil attachment does not match the pipeline description.");
        if (_hasDepthTarget && _depthFormat != _renderPipeline.Description.DepthStencilFormat)
            throw new GraphicsValidationException(
                $"Depth/stencil attachment format {_depthFormat} does not match pipeline format {_renderPipeline.Description.DepthStencilFormat}.");

        GraphicsDepthStencilState depthState = _renderPipeline.Description.DepthStencil;
        if ((depthState.DepthTestEnabled || depthState.DepthWriteEnabled) && !_hasDepthTarget)
            throw new GraphicsValidationException("The active pipeline requires a depth attachment.");
        if (depthState.StencilTestEnabled &&
            _depthFormat is not (GraphicsFormat.D24UNormS8UInt or GraphicsFormat.D32FloatS8UInt))
            throw new GraphicsValidationException("Stencil testing requires a D24S8 depth/stencil attachment.");
        for (uint set = 0; set < (uint)_pipelineLayout!.BindingLayouts.Length; set++)
            if (!_boundSets.Contains(set))
                throw new GraphicsValidationException($"Pipeline descriptor set {set} has not been bound.");
    }

    private void EnsureComputeReady()
    {
        EnsureComputeQueue();
        if (_computePipeline is null)
            throw new GraphicsValidationException("A compute pipeline must be selected before dispatch.");
        for (uint set = 0; set < (uint)_pipelineLayout!.BindingLayouts.Length; set++)
            if (!_boundSets.Contains(set))
                throw new GraphicsValidationException($"Compute descriptor set {set} has not been bound.");
    }

    private void EnsureGraphicsQueue()
    {
        if (QueueType != GraphicsQueueType.Graphics)
            throw new GraphicsValidationException("Render commands require the graphics queue.");
    }

    private void EnsureComputeQueue()
    {
        if (QueueType != GraphicsQueueType.Compute)
            throw new GraphicsValidationException("Compute commands require the compute queue.");
    }

    private void EnsureRecording()
    {
        ThrowIfDisposed();
        _lifecycle.EnsureRecording();
        if (_recordingThreadId != Environment.CurrentManagedThreadId)
            throw new InvalidOperationException("A GLES command list may only be recorded by the thread that called Begin.");
    }

    private void EnsureOwned(IGraphicsDeviceObject resource)
    {
        if (resource.IsDisposed || resource.Device.IsDisposed)
            throw new ObjectDisposedException(resource.GetType().Name);
        if (!ReferenceEquals(resource.Device, Device))
            throw new GraphicsValidationException("Command resources must belong to the command list's device.");

        if (resource is IOpenGLESNativeResource native)
            Retain(native);
    }

    private void Retain(IOpenGLESNativeResource resource)
    {
        if (_retained.Add(resource) && !resource.TryRetain())
        {
            _retained.Remove(resource);
            throw new ObjectDisposedException("OpenGL ES native resource");
        }
    }

    private static uint GetVertexStride(GraphicsVertexInputLayout layout, uint binding)
    {
        foreach (GraphicsVertexBufferLayout buffer in layout.Buffers)
            if (buffer.Binding == binding)
                return buffer.Stride;
        throw new GraphicsValidationException($"Vertex binding {binding} is not declared.");
    }

    private static GraphicsFormat GetViewFormat(IGraphicsTextureView view) =>
        view.Description.Format == GraphicsFormat.Unknown ? view.Texture.Description.Format : view.Description.Format;

    private static PrimitiveType GetPrimitive(GraphicsPrimitiveTopology topology) => topology switch
    {
        GraphicsPrimitiveTopology.PointList => PrimitiveType.Points,
        GraphicsPrimitiveTopology.LineList => PrimitiveType.Lines,
        GraphicsPrimitiveTopology.LineStrip => PrimitiveType.LineStrip,
        GraphicsPrimitiveTopology.TriangleList => PrimitiveType.Triangles,
        GraphicsPrimitiveTopology.TriangleStrip => PrimitiveType.TriangleStrip,
        GraphicsPrimitiveTopology.TriangleFan => PrimitiveType.TriangleFan,
        GraphicsPrimitiveTopology.PatchList => PrimitiveType.Patches,
        _ => throw new GraphicsValidationException($"Primitive topology {topology} is not supported by OpenGL ES.")
    };

    private void ClearRecordedState()
    {
        _commands.Clear();
        _renderTargetViews.Clear();
        _bindingValues.Clear();
        _renderPipeline = null;
        _computePipeline = null;
        _pipelineLayout = null;
        _boundSets.Clear();
        _indexBuffer = null;
        _indexOffset = 0;
        _hasTargets = false;
        _hasViewport = false;
        _hasDepthTarget = false;
        _depthFormat = GraphicsFormat.Unknown;
        _colorCount = 0;
        _targetSampleCount = 0;
        _targetOffset = 0;
        Exception? releaseFailure = null;
        foreach (IOpenGLESNativeResource resource in _retained)
        {
            try
            {
                resource.ReleaseReference();
            }
            catch (Exception exception)
            {
                releaseFailure ??= exception;
            }
        }
        _retained.Clear();

        if (releaseFailure is not null)
            throw new GraphicsException(
                "One or more retained GLES resources failed to release during command-list cleanup.",
                releaseFailure);
    }

    protected override void DisposeCore()
    {
        if (_lifecycle.State == GraphicsCommandListState.Pending)
        {
            try
            {
                _queue.WaitForCompletion(this);
            }
            catch (ObjectDisposedException) when (Device.IsDisposed)
            {
                // Device shutdown owns the registered native resources until GPU idleness has
                // been established (or the host context is destroyed after a failed Finish).
                // Releasing this list's managed references cannot delete those native resources.
                _lifecycle.TryMarkCompleted();
            }
        }

        if (_lifecycle.State == GraphicsCommandListState.Recording)
            _lifecycle.End();
        _recordingThreadId = 0;
        ReleaseCompletedResourcesIfDisposed();
    }
}
