using System.Runtime.ExceptionServices;
using Auralis.Graphics.Abstractions;
using Auralis.Graphics.Commands;
using Auralis.Graphics.Devices;
using Auralis.Graphics.Synchronization;
using Silk.NET.OpenGLES;

namespace Auralis.Graphics.Backends.OpenGLES;

/// <summary>
/// Ordered logical queue over the externally supplied GLES context. Queue roles share one native command
/// stream; command lists retain all recorded resources until a GLES fence reports completion.
/// </summary>
internal sealed class OpenGLESGraphicsCommandQueue : IGraphicsCommandQueue
{
    private readonly OpenGLESGraphicsDevice _device;
    private readonly object _gate = new();
    private readonly List<PendingSubmission> _pending = new(8);

    internal OpenGLESGraphicsCommandQueue(OpenGLESGraphicsDevice device, GraphicsQueueType queueType)
    {
        _device = device;
        QueueType = queueType;
    }

    public IGraphicsDevice Device => _device;
    internal OpenGLESGraphicsDevice GlesDevice => _device;
    public uint Index => 0;
    public GraphicsQueueType QueueType { get; }

    public IGraphicsCommandList CreateCommandList(GraphicsCommandListUsage usage = GraphicsCommandListUsage.Reusable)
    {
        ObjectDisposedException.ThrowIf(_device.IsDisposed, _device);
        if (!Enum.IsDefined(usage))
            throw new ArgumentOutOfRangeException(nameof(usage));
        PollPending();
        return new OpenGLESGraphicsCommandList(this, usage);
    }

    public void Submit(
        ReadOnlySpan<IGraphicsCommandList> commandLists,
        IGraphicsFence? signalFence = null,
        ulong signalValue = 0)
    {
        ObjectDisposedException.ThrowIf(_device.IsDisposed, _device);
        if (commandLists.IsEmpty)
            throw new ArgumentException("At least one command list is required.", nameof(commandLists));

        OpenGLESGraphicsCommandList[] lists = new OpenGLESGraphicsCommandList[commandLists.Length];
        for (int i = 0; i < commandLists.Length; i++)
        {
            if (commandLists[i] is not OpenGLESGraphicsCommandList list ||
                !ReferenceEquals(list.Device, _device) ||
                list.QueueType != QueueType ||
                list.Lifecycle.State != GraphicsCommandListState.Executable)
                throw new GraphicsValidationException(
                    "Submitted lists must be executable lists created by this GLES device and queue.");

            for (int j = 0; j < i; j++)
                if (ReferenceEquals(lists[j], list))
                    throw new GraphicsValidationException("A command list cannot be submitted more than once in one batch.");
            lists[i] = list;
        }

        OpenGLESGraphicsFence? fence = null;
        if (signalFence is not null)
        {
            fence = signalFence as OpenGLESGraphicsFence;
            if (fence is null || !ReferenceEquals(fence.Device, _device) || fence.IsDisposed)
                throw new GraphicsValidationException("The signal fence must be live and owned by this GLES device.");
        }
        else if (signalValue != 0)
        {
            throw new ArgumentException("A non-zero signal value requires a signal fence.", nameof(signalValue));
        }

        int submitted = 0;
        bool commandsStarted = false;
        nint completionSync = 0;

        _device.WithContext(gl =>
        {
            try
            {
                // Keep Executable -> Pending and failure recovery serialized with Dispose.
                for (int i = 0; i < lists.Length; i++)
                {
                    lists[i].MarkSubmitted();
                    submitted++;
                }

                commandsStarted = true;
                for (int i = 0; i < lists.Length; i++)
                    lists[i].Execute(gl);

                completionSync = gl.FenceSync(SyncCondition.SyncGpuCommandsComplete, (uint)0);
                if (completionSync == 0)
                    throw new GraphicsException("OpenGL ES failed to create a command-submission sync object.");

                fence?.SignalFromGpu(gl, signalValue);
                gl.Flush();

                lock (_gate)
                {
                    _pending.Add(new PendingSubmission(lists, completionSync));
                }
            }
            catch (Exception submissionError)
            {
                // This recovery runs before the context gate is released. A disposer waiting on
                // that gate therefore observes either an owning queue record or a completed list.
                Exception? cleanupFailure = null;
                try
                {
                    GraphicsCommandListSubmissionRecovery.RecoverFailedSubmission(
                        lists,
                        submitted,
                        commandsStarted,
                        gl.Finish,
                        static list =>
                        {
                            if (list.Lifecycle.State == GraphicsCommandListState.Pending)
                                list.MarkCompletedAfterGpu();
                        });
                }
                catch (Exception exception)
                {
                    cleanupFailure ??= exception;
                }

                if (completionSync != 0)
                {
                    try { gl.DeleteSync(completionSync); }
                    catch (Exception exception) { cleanupFailure ??= exception; }
                    completionSync = 0;
                }

                if (cleanupFailure is not null)
                    throw new AggregateException(
                        "GLES submission failed and its in-context recovery also failed.",
                        submissionError,
                        cleanupFailure);

                ExceptionDispatchInfo.Capture(submissionError).Throw();
                throw;
            }
        });

    }

    public void Wait(IGraphicsFence fence, ulong value)
    {
        ObjectDisposedException.ThrowIf(_device.IsDisposed, _device);
        if (fence is not OpenGLESGraphicsFence glesFence ||
            !ReferenceEquals(glesFence.Device, _device))
            throw new GraphicsValidationException("The wait fence must belong to this GLES device.");
        _device.WithContext(gl => glesFence.EnqueueGpuWait(gl, value));
    }

    internal void PollPending()
    {
        if (_device.IsDisposed)
            return;

        List<PendingSubmission>? completed = null;
        _device.WithContext(gl =>
        {
            lock (_gate)
            {
                for (int i = _pending.Count - 1; i >= 0; i--)
                {
                    PendingSubmission submission = _pending[i];
                    GLEnum status = gl.ClientWaitSync(submission.Sync, (uint)0, 0);
                    if (status == GLEnum.TimeoutExpired)
                        continue;
                    if (status == GLEnum.WaitFailed)
                        throw new GraphicsException("OpenGL ES failed while polling a command submission.");
                    if (status is not (GLEnum.AlreadySignaled or GLEnum.ConditionSatisfied))
                        throw new GraphicsException($"OpenGL ES returned unexpected sync status {status}.");

                    gl.DeleteSync(submission.Sync);
                    MarkSubmissionCompleted(submission);
                    _pending.RemoveAt(i);
                    completed ??= new List<PendingSubmission>(4);
                    completed.Add(submission);
                }
            }
        });

        if (completed is not null)
            CompleteAll(completed);
    }

    internal void WaitForCompletion(OpenGLESGraphicsCommandList commandList)
    {
        while (commandList.Lifecycle.State == GraphicsCommandListState.Pending)
        {
            PendingSubmission completedSubmission = default;
            bool hasCompletedSubmission = false;
            bool recoveredUnqueuedSubmission = false;

            _device.WithContext(gl =>
            {
                lock (_gate)
                {
                    for (int i = _pending.Count - 1; i >= 0; i--)
                    {
                        PendingSubmission submission = _pending[i];
                        bool contains = false;
                        for (int j = 0; j < submission.CommandLists.Length; j++)
                            contains |= ReferenceEquals(submission.CommandLists[j], commandList);
                        if (!contains)
                            continue;

                        GLEnum status = gl.ClientWaitSync(
                            submission.Sync,
                            (SyncObjectMask)SyncObjectMask.Bit,
                            1_000_000);
                        if (status == GLEnum.WaitFailed)
                            throw new GraphicsException("OpenGL ES failed while waiting for command-list completion.");
                        if (status is not (GLEnum.AlreadySignaled or GLEnum.ConditionSatisfied or GLEnum.TimeoutExpired))
                            throw new GraphicsException($"OpenGL ES returned unexpected sync status {status}.");

                        if (status is GLEnum.AlreadySignaled or GLEnum.ConditionSatisfied)
                        {
                            gl.DeleteSync(submission.Sync);
                            MarkSubmissionCompleted(submission);
                            _pending.RemoveAt(i);
                            completedSubmission = submission;
                            hasCompletedSubmission = true;
                        }
                        return;
                    }
                }

                if (commandList.Lifecycle.State == GraphicsCommandListState.Pending)
                {
                    // This is the exceptional no-record recovery path after submission failure.
                    // Do not drain unrelated queue records.
                    gl.Finish();
                    if (commandList.Lifecycle.State == GraphicsCommandListState.Pending)
                        commandList.MarkCompletedAfterGpu();
                    recoveredUnqueuedSubmission = true;
                }
            });

            if (hasCompletedSubmission)
                CompleteSubmission(completedSubmission);
            else if (recoveredUnqueuedSubmission)
                commandList.ReleaseCompletedResourcesIfDisposed();
        }
    }

    private static void MarkSubmissionCompleted(PendingSubmission submission)
    {
        for (int i = 0; i < submission.CommandLists.Length; i++)
            submission.CommandLists[i].MarkCompletedAfterGpu();
    }

    /// <summary>Called with the owning context current after the device has finished the GPU.</summary>
    internal void DrainUnderContext(GL gl)
    {
        List<PendingSubmission>? completed = null;
        lock (_gate)
        {
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                PendingSubmission submission = _pending[i];
                gl.DeleteSync(submission.Sync);
                MarkSubmissionCompleted(submission);
                completed ??= new List<PendingSubmission>(4);
                completed.Add(submission);
            }
            _pending.Clear();
        }

        if (completed is not null)
            CompleteAll(completed);
    }

    private static void CompleteAll(IReadOnlyList<PendingSubmission> submissions)
    {
        List<Exception>? failures = null;
        for (int i = 0; i < submissions.Count; i++)
        {
            try { CompleteSubmission(submissions[i]); }
            catch (Exception exception)
            {
                failures ??= new List<Exception>();
                failures.Add(exception);
            }
        }

        if (failures is not null)
            throw new AggregateException(
                "One or more GLES submissions failed to release retained resources after GPU completion.",
                failures);
    }

    private static void CompleteSubmission(PendingSubmission submission)
    {
        Exception? failure = null;
        for (int i = 0; i < submission.CommandLists.Length; i++)
        {
            try { submission.CommandLists[i].ReleaseCompletedResourcesIfDisposed(); }
            catch (Exception exception) { failure ??= exception; }
        }

        if (failure is not null)
            ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private readonly record struct PendingSubmission(
        OpenGLESGraphicsCommandList[] CommandLists,
        nint Sync);
}
