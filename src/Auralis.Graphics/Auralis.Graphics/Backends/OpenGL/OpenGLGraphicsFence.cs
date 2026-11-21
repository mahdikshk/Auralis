using System.Diagnostics;
using Auralis.Graphics.Abstractions;
using Auralis.Graphics.Commands;
using Auralis.Graphics.Synchronization;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ARB;

namespace Auralis.Graphics.Backends.OpenGL;

/// <summary>
/// Timeline-style fence backed by ordered GLsync objects. Signal order is monotonic, so a FIFO
/// queue is sufficient and avoids per-entry tree nodes and temporary cleanup collections.
/// </summary>
internal sealed class OpenGLGraphicsFence : OpenGLNativeDeviceObject, IGraphicsFence
{
    private readonly OpenGLGraphicsDevice _device;
    private readonly ArbSync _syncApi;
    private readonly object _gate = new();
    private readonly Queue<SyncPoint> _syncs = new(16);
    private ulong _completedValue;
    private ulong _lastSignaledValue;

    internal OpenGLGraphicsFence(
        OpenGLGraphicsDevice device,
        ArbSync syncApi,
        ulong initialValue) : base(device)
    {
        _device = device;
        _syncApi = syncApi;
        _completedValue = initialValue;
        _lastSignaledValue = initialValue;
    }

    public ulong CompletedValue
    {
        get
        {
            ThrowIfDisposed();
            return _device.WithContext(gl =>
            {
                lock (_gate)
                {
                    PollCompleted(gl, 0);
                    return _completedValue;
                }
            });
        }
    }

    /// <summary>Signals the timeline from the CPU. GPU submissions use SignalFromGpu instead.</summary>
    public void Signal(ulong value)
    {
        ThrowIfDisposed();
        _device.WithContext(gl =>
        {
            lock (_gate)
            {
                EnsureMonotonic(value);
                PollCompleted(gl, 0);
                GraphicsCommandValidation.ValidateCpuFenceSignalDoesNotOvertakeGpu(
                    value,
                    _syncs.TryPeek(out SyncPoint pending) ? pending.Value : null);
                _lastSignaledValue = value;
                _completedValue = value;
                DeleteCompletedSyncs(gl, value);
                Monitor.PulseAll(_gate);
            }
        });
    }

    internal void SignalFromGpu(GL gl, ulong value)
    {
        ThrowIfDisposed();
        lock (_gate)
        {
            EnsureMonotonic(value);
            nint sync = _syncApi.FenceSync(
                SyncCondition.SyncGpuCommandsComplete,
                SyncBehaviorFlags.None);
            if (sync == 0)
                throw new GraphicsException("OpenGL failed to create a GPU fence sync object.");

            _syncs.Enqueue(new SyncPoint(value, sync));
            _lastSignaledValue = value;
            Monitor.PulseAll(_gate);
        }
    }

    internal void EnqueueGpuWait(GL gl, ulong value)
    {
        ThrowIfDisposed();
        lock (_gate)
        {
            PollCompleted(gl, 0);
            if (value <= _completedValue)
                return;

            foreach (SyncPoint point in _syncs)
            {
                if (point.Value >= value)
                {
                    _syncApi.WaitSync(point.Handle, SyncBehaviorFlags.None, ulong.MaxValue);
                    return;
                }
            }
        }

        throw new GraphicsValidationException(
            $"Fence value {value} has not been submitted or CPU-signaled; OpenGL cannot enqueue a wait for an unknown future sync object.");
    }

    public bool Wait(ulong value, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (timeout < TimeSpan.Zero && timeout != Timeout.InfiniteTimeSpan)
            throw new ArgumentOutOfRangeException(nameof(timeout));

        long start = Stopwatch.GetTimestamp();
        long timeoutTicks = timeout == Timeout.InfiniteTimeSpan
            ? long.MaxValue
            : (long)(timeout.TotalSeconds * Stopwatch.Frequency);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            bool completed = _device.WithContext(gl =>
            {
                lock (_gate)
                    return PollCompleted(gl, 1_000_000) >= value;
            });

            if (completed)
                return true;

            if (timeoutTicks != long.MaxValue && Stopwatch.GetTimestamp() - start >= timeoutTicks)
                return false;

            lock (_gate)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (_completedValue < value && _syncs.Count == 0)
                    Monitor.Wait(_gate, TimeSpan.FromMilliseconds(1));
            }
        }
    }

    private ulong PollCompleted(GL gl, ulong waitNanoseconds)
    {
        if (_syncs.Count == 0)
            return _completedValue;

        ulong completed = _completedValue;
        foreach (SyncPoint point in _syncs)
        {
            ARB status = _syncApi.ClientWaitSync(
                point.Handle,
                SyncObjectMask.Bit,
                waitNanoseconds);

            if (status == ARB.AlreadySignaled || status == ARB.ConditionSatisfied)
            {
                completed = point.Value;
                continue;
            }

            if (status == ARB.TimeoutExpired)
                break;

            if (status == ARB.WaitFailed)
                throw new GraphicsException("OpenGL ClientWaitSync failed while waiting for a fence.");
        }

        if (completed > _completedValue)
        {
            _completedValue = completed;
            DeleteCompletedSyncs(gl, completed);
            Monitor.PulseAll(_gate);
        }

        return _completedValue;
    }

    private void DeleteCompletedSyncs(GL gl, ulong value)
    {
        while (_syncs.TryPeek(out SyncPoint point) && point.Value <= value)
        {
            _syncApi.DeleteSync(point.Handle);
            _syncs.Dequeue();
        }
    }

    private void EnsureMonotonic(ulong value)
    {
        if (value <= _lastSignaledValue)
            throw new GraphicsValidationException(
                $"Fence values must increase monotonically. Last signal: {_lastSignaledValue}; requested: {value}.");
    }

    protected override void DisposeCore() => ReleaseOwnerReference();

    public override void ReleaseNative(GL gl)
    {
        lock (_gate)
        {
            while (_syncs.TryDequeue(out SyncPoint point))
                _syncApi.DeleteSync(point.Handle);
            Monitor.PulseAll(_gate);
        }
    }

    private readonly record struct SyncPoint(ulong Value, nint Handle);
}
