using Auralis.Graphics.Abstractions;
using Auralis.Graphics.Devices;
using Auralis.Graphics.Pipelines;
using Auralis.Graphics.Resources;
using Auralis.Graphics.Synchronization;

namespace Auralis.Graphics.Commands;

/// <summary>Current lifecycle state of a recorded command list.</summary>
public enum GraphicsCommandListState
{
    Initial,
    Recording,
    Executable,
    Pending,
    Disposed
}

/// <summary>Reuse policy selected when a command list is allocated.</summary>
public enum GraphicsCommandListUsage
{
    Reusable,
    OneTimeSubmit
}

public enum GraphicsIndexFormat
{
    UInt16,
    UInt32
}

/// <summary>
/// Small, allocation-free state tracker for backend command-list implementations. Backends should
/// call MarkSubmitted only after successful queue submission and MarkCompleted after GPU completion.
/// </summary>
public sealed class GraphicsCommandListLifecycle
{
    private int _state;
    private int _hasBeenSubmitted;

    public GraphicsQueueType QueueType { get; }

    public GraphicsCommandListUsage Usage { get; }

    public GraphicsCommandListState State => (GraphicsCommandListState)Volatile.Read(ref _state);

    public GraphicsCommandListLifecycle(
        GraphicsQueueType queueType,
        GraphicsCommandListUsage usage = GraphicsCommandListUsage.Reusable)
    {
        if (!Enum.IsDefined(queueType))
            throw new ArgumentOutOfRangeException(nameof(queueType));
        if (!Enum.IsDefined(usage))
            throw new ArgumentOutOfRangeException(nameof(usage));

        QueueType = queueType;
        Usage = usage;
    }

    public void Begin() => Transition(GraphicsCommandListState.Initial, GraphicsCommandListState.Recording);

    public void End() => Transition(GraphicsCommandListState.Recording, GraphicsCommandListState.Executable);

    public void MarkSubmitted()
    {
        if (Usage == GraphicsCommandListUsage.OneTimeSubmit &&
            Interlocked.CompareExchange(ref _hasBeenSubmitted, 1, 0) != 0)
            throw new InvalidOperationException("A one-time command list cannot be submitted more than once.");

        try
        {
            Transition(GraphicsCommandListState.Executable, GraphicsCommandListState.Pending);
        }
        catch
        {
            if (Usage == GraphicsCommandListUsage.OneTimeSubmit)
                Volatile.Write(ref _hasBeenSubmitted, 0);
            throw;
        }
    }

    public void MarkCompleted() =>
        Transition(GraphicsCommandListState.Pending, GraphicsCommandListState.Executable);

    /// <summary>Attempts the Pending-to-Executable transition without failing if another owner won it.</summary>
    internal bool TryMarkCompleted() =>
        Interlocked.CompareExchange(
            ref _state,
            (int)GraphicsCommandListState.Executable,
            (int)GraphicsCommandListState.Pending) == (int)GraphicsCommandListState.Pending;

    public void Reset()
    {
        if (Usage == GraphicsCommandListUsage.OneTimeSubmit && Volatile.Read(ref _hasBeenSubmitted) != 0)
            throw new InvalidOperationException("A one-time command list cannot be reset after submission.");

        while (true)
        {
            GraphicsCommandListState state = State;
            if (state == GraphicsCommandListState.Initial)
                return;
            if (state != GraphicsCommandListState.Executable)
                throw new InvalidOperationException($"A command list in state {state} cannot be reset.");

            if (Interlocked.CompareExchange(
                    ref _state,
                    (int)GraphicsCommandListState.Initial,
                    (int)GraphicsCommandListState.Executable) == (int)GraphicsCommandListState.Executable)
                return;
        }
    }

    public void EnsureRecording()
    {
        if (State != GraphicsCommandListState.Recording)
            throw new InvalidOperationException("Commands can only be recorded between Begin and End.");
    }

    public void EnsureGraphicsCommands()
    {
        EnsureRecording();
        if (QueueType != GraphicsQueueType.Graphics)
            throw new InvalidOperationException("Draw commands require a graphics command list.");
    }

    public void EnsureComputeCommands()
    {
        EnsureRecording();
        if (QueueType == GraphicsQueueType.Copy)
            throw new InvalidOperationException("Dispatch commands are not legal on a copy command list.");
    }

    public void EnsureCopyCommands() => EnsureRecording();

    /// <summary>Marks the tracker disposed after backend-owned in-flight work has completed.</summary>
    public void MarkDisposed()
    {
        while (true)
        {
            GraphicsCommandListState state = State;
            if (state is GraphicsCommandListState.Pending or GraphicsCommandListState.Recording)
                throw new InvalidOperationException("A pending or recording command list cannot be destroyed yet.");
            if (state == GraphicsCommandListState.Disposed)
                return;

            if (Interlocked.CompareExchange(
                    ref _state,
                    (int)GraphicsCommandListState.Disposed,
                    (int)state) == (int)state)
                return;
        }
    }

    private void Transition(GraphicsCommandListState expected, GraphicsCommandListState next)
    {
        if (Interlocked.CompareExchange(ref _state, (int)next, (int)expected) != (int)expected)
            throw new InvalidOperationException($"Cannot transition a command list from {State}; expected {expected}.");
    }
}

/// <summary>
/// A device-owned queue identity. A queue may support presentation without supporting command recording.
/// </summary>
public interface IGraphicsQueue
{
    IGraphicsDevice Device { get; }

    uint Index { get; }
}

/// <summary>
/// A device-owned command-capable queue. Presentation-only queues intentionally do not implement this interface.
/// </summary>
public interface IGraphicsCommandQueue : IGraphicsQueue
{
    GraphicsQueueType QueueType { get; }

    IGraphicsCommandList CreateCommandList(GraphicsCommandListUsage usage = GraphicsCommandListUsage.Reusable);

    void Submit(
        ReadOnlySpan<IGraphicsCommandList> commandLists,
        IGraphicsFence? signalFence = null,
        ulong signalValue = 0);

    /// <summary>Enqueues a GPU-side wait. This does not block the calling CPU thread.</summary>
    void Wait(IGraphicsFence fence, ulong value);
}

/// <summary>
/// Backend command recorder. Begin/End/Reset and operation legality are enforced by the implementation.
/// Recording is caller-thread-owned; separate command lists may be recorded concurrently.
/// </summary>
public interface IGraphicsCommandList : IGraphicsDeviceObject
{
    GraphicsQueueType QueueType { get; }

    GraphicsCommandListUsage Usage { get; }

    GraphicsCommandListState State { get; }

    void Begin();

    void End();

    void Reset();

    void MemoryBarrier(ReadOnlySpan<GraphicsResourceBarrier> barriers);

    /// <summary>
    /// Selects the color and optional depth/stencil attachments for subsequent draw and clear commands.
    /// The backend retains references to these views until the recording is reset or completed.
    /// An empty color span is valid when a depth/stencil-only target is supplied.
    /// </summary>
    void SetRenderTargets(
        ReadOnlySpan<IGraphicsTextureView> colorTargets,
        IGraphicsTextureView? depthStencilTarget = null);

    void SetViewport(in GraphicsViewport viewport);

    void SetScissor(in GraphicsScissorRect scissor);

    void ClearColor(uint attachmentIndex, in GraphicsClearColor color);

    void ClearColorUInt(uint attachmentIndex, in GraphicsClearColorUInt color);

    void ClearColorSInt(uint attachmentIndex, in GraphicsClearColorSInt color);

    void ClearDepthStencil(
        float depth,
        uint stencil,
        bool clearDepth = true,
        bool clearStencil = false);

    void SetRenderPipeline(IGraphicsRenderPipeline pipeline);

    void SetComputePipeline(IGraphicsComputePipeline pipeline);

    void SetBindingSet(uint setIndex, IGraphicsBindingSet bindingSet);

    void SetVertexBuffer(uint binding, IGraphicsBuffer buffer, ulong offset = 0);

    void SetIndexBuffer(IGraphicsBuffer buffer, GraphicsIndexFormat format, ulong offset = 0);

    void Draw(uint vertexCount, uint instanceCount = 1, uint firstVertex = 0, uint firstInstance = 0);

    void DrawIndexed(
        uint indexCount,
        uint instanceCount = 1,
        uint firstIndex = 0,
        int baseVertex = 0,
        uint firstInstance = 0);

    void DrawIndirect(IGraphicsBuffer arguments, ulong offset, uint drawCount = 1, uint stride = 0);

    void DrawIndexedIndirect(IGraphicsBuffer arguments, ulong offset, uint drawCount = 1, uint stride = 0);

    /// <summary>Records a mesh-shader work-group dispatch. Requires a mesh pipeline.</summary>
    void DrawMeshTasks(uint groupCountX, uint groupCountY = 1, uint groupCountZ = 1);

    /// <summary>Records one or more mesh-task commands from a GPU indirect-argument buffer.</summary>
    void DrawMeshTasksIndirect(IGraphicsBuffer arguments, ulong offset, uint drawCount = 1, uint stride = 0);

    /// <summary>
    /// Records a compute dispatch. If any group count is zero, the operation is a valid no-op and no GPU dispatch is recorded.
    /// </summary>
    void Dispatch(uint groupCountX, uint groupCountY = 1, uint groupCountZ = 1);

    void DispatchIndirect(IGraphicsBuffer arguments, ulong offset);

    void CopyBuffer(
        IGraphicsBuffer source,
        ulong sourceOffset,
        IGraphicsBuffer destination,
        ulong destinationOffset,
        ulong sizeInBytes);

    void CopyTexture(IGraphicsTexture source, IGraphicsTexture destination);
}
