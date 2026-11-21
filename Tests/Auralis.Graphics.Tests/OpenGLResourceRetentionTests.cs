using Auralis.Graphics.Backends.OpenGL;
using Auralis.Graphics.Abstractions;
using Auralis.Graphics.Commands;
using Auralis.Graphics.Devices;
using Silk.NET.OpenGL;
using Xunit;

namespace Auralis.Graphics.Tests;

/// <summary>
/// Deterministic tests for the same native-reference state machine used by OpenGL device objects
/// and by command-list recording. These tests intentionally do not require a GL context or timing.
/// </summary>
public sealed class OpenGLResourceRetentionTests
{
    [Fact]
    public void DisposedBufferRemainsAliveUntilRecordedReferencesAreReleased()
    {
        var retention = new OpenGLResourceRetention();
        var buffer = new InstrumentedNativeResource();

        retention.Retain(buffer);
        buffer.DisposeOwner();

        Assert.Equal(0, buffer.NativeReleaseCount);
        retention.ReleaseAll();
        Assert.Equal(1, buffer.NativeReleaseCount);
    }

    [Fact]
    public void DisposedTextureRemainsAliveUntilRecordedReferencesAreReleased()
    {
        var retention = new OpenGLResourceRetention();
        var texture = new InstrumentedNativeResource();

        retention.Retain(texture);
        texture.DisposeOwner();

        Assert.Equal(0, texture.NativeReleaseCount);
        retention.ReleaseAll();
        Assert.Equal(1, texture.NativeReleaseCount);
    }

    [Fact]
    public void ResettingCommandListReleasesItsRecordedNativeReferences()
    {
        var retention = new OpenGLResourceRetention();
        var resource = new InstrumentedNativeResource();

        retention.Retain(resource);
        resource.DisposeOwner();
        Assert.Equal(0, resource.NativeReleaseCount);

        // Reset releases the complete recording's retained resource set.
        retention.ReleaseAll();

        Assert.Equal(1, resource.NativeReleaseCount);
        Assert.Equal(0, retention.Count);
    }

    [Fact]
    public void DisposingUnsubmittedCommandListReleasesItsRecordedNativeReferences()
    {
        var retention = new OpenGLResourceRetention();
        var resource = new InstrumentedNativeResource();

        retention.Retain(resource);
        resource.DisposeOwner();
        Assert.Equal(0, resource.NativeReleaseCount);

        // Dispose of an executable list uses the same cleanup path as resetting it.
        retention.ReleaseAll();

        Assert.Equal(1, resource.NativeReleaseCount);
    }

    [Fact]
    public void SubmittedCommandListKeepsDisposedResourcesUntilCompletionAndReset()
    {
        var lifecycle = new GraphicsCommandListLifecycle(GraphicsQueueType.Graphics);
        var retention = new OpenGLResourceRetention();
        var resource = new InstrumentedNativeResource();

        lifecycle.Begin();
        retention.Retain(resource);
        lifecycle.End();
        lifecycle.MarkSubmitted();
        resource.DisposeOwner();

        Assert.Equal(GraphicsCommandListState.Pending, lifecycle.State);
        Assert.Equal(0, resource.NativeReleaseCount);
        Assert.Throws<InvalidOperationException>(lifecycle.Reset);

        lifecycle.MarkCompleted();
        lifecycle.Reset();
        retention.ReleaseAll();

        Assert.Equal(1, resource.NativeReleaseCount);
    }

    [Fact]
    public void MultipleCommandListsRetainSharedResourceIndependently()
    {
        var firstList = new OpenGLResourceRetention();
        var secondList = new OpenGLResourceRetention();
        var resource = new InstrumentedNativeResource();

        firstList.Retain(resource);
        secondList.Retain(resource);
        resource.DisposeOwner();

        firstList.ReleaseAll();
        Assert.Equal(0, resource.NativeReleaseCount);

        secondList.ReleaseAll();
        Assert.Equal(1, resource.NativeReleaseCount);
    }

    [Fact]
    public void FailedSubmissionCanCompleteAndReleaseResourcesOnSubsequentReset()
    {
        var lifecycle = new GraphicsCommandListLifecycle(GraphicsQueueType.Compute);
        var retention = new OpenGLResourceRetention();
        var resource = new InstrumentedNativeResource();

        lifecycle.Begin();
        retention.Retain(resource);
        lifecycle.End();
        lifecycle.MarkSubmitted();
        resource.DisposeOwner();

        // The queue's failed-execution path completes the list after finishing any issued GL work.
        lifecycle.MarkCompleted();
        Assert.Equal(0, resource.NativeReleaseCount);

        lifecycle.Reset();
        retention.ReleaseAll();
        Assert.Equal(1, resource.NativeReleaseCount);
    }

    [Fact]
    public void DeviceShutdownReleasesNativeObjectWhileSubmittedListStillRetainsIt()
    {
        var lifecycle = new GraphicsCommandListLifecycle(GraphicsQueueType.Graphics);
        var retention = new OpenGLResourceRetention();
        var resource = new InstrumentedNativeResource();

        lifecycle.Begin();
        retention.Retain(resource);
        lifecycle.End();
        lifecycle.MarkSubmitted();
        resource.DisposeOwner();

        Assert.Equal(GraphicsCommandListState.Pending, lifecycle.State);
        Assert.Equal(0, resource.NativeReleaseCount);

        // The simulated device shutdown finishes the queue before forcing native deletion.
        resource.DisposeDevice();
        lifecycle.MarkCompleted();
        Assert.Equal(1, resource.NativeReleaseCount);

        // Managed list cleanup after shutdown must not issue another native delete.
        retention.ReleaseAll();
        Assert.Equal(1, resource.NativeReleaseCount);
    }

    [Fact]
    public void RepeatedDisposalAndDuplicateReleaseCannotDeleteNativeResourceTwice()
    {
        var retention = new OpenGLResourceRetention();
        var resource = new InstrumentedNativeResource();

        retention.Retain(resource);
        retention.Retain(resource); // A list holds one reference per distinct object.
        resource.DisposeOwner();
        resource.DisposeOwner();

        Assert.Equal(0, resource.NativeReleaseCount);
        Assert.Equal(1, retention.Count);

        retention.ReleaseAll();
        Assert.Equal(1, resource.NativeReleaseCount);
        Assert.Throws<InvalidOperationException>(resource.ReleaseReference);
    }

    [Fact]
    public void ClosedResourceRejectsNewRetains()
    {
        var resource = new InstrumentedNativeResource();
        resource.DisposeOwner();

        Assert.False(resource.TryRetain());
        Assert.Equal(1, resource.NativeReleaseCount);
    }

    [Fact]
    public async Task ConcurrentRetainsRacingOwnerDisposalReleaseExactlyOnce()
    {
        var resource = new InstrumentedNativeResource();
        using var start = new ManualResetEventSlim(initialState: false);

        Task retainingWorkers = Task.Run(() =>
        {
            Parallel.For(0, 256, _ =>
            {
                start.Wait();
                if (!resource.TryRetain())
                    return;

                Thread.SpinWait(100);
                resource.ReleaseReference();
            });
        });

        Task ownerDisposal = Task.Run(() =>
        {
            start.Wait();
            Thread.SpinWait(100);
            resource.DisposeOwner();
        });

        start.Set();
        await Task.WhenAll(retainingWorkers, ownerDisposal);

        Assert.Equal(1, resource.NativeReleaseCount);
    }

    [Fact]
    public void ReleaseAllContinuesAfterAnIndividualReferenceReleaseThrows()
    {
        var retention = new OpenGLResourceRetention();
        var throwing = new ThrowingReleaseResource();
        var resource = new InstrumentedNativeResource();

        retention.Retain(throwing);
        retention.Retain(resource);
        resource.DisposeOwner();

        Assert.Throws<InvalidOperationException>(retention.ReleaseAll);
        Assert.Equal(1, resource.NativeReleaseCount);
        Assert.Equal(0, retention.Count);
    }

    private sealed class ThrowingReleaseResource : IOpenGLNativeResource
    {
        public bool TryRetain() => true;
        public void ReleaseReference() => throw new InvalidOperationException("Synthetic release failure.");
        public void ReleaseNative(GL gl) { }
    }

    private sealed class InstrumentedNativeResource : IOpenGLNativeResource
    {
        private OpenGLNativeResourceReferenceState _references = new(1);
        private int _ownerDisposed;
        private int _deviceDisposed;
        private int _nativeReleaseCount;

        internal int NativeReleaseCount => Volatile.Read(ref _nativeReleaseCount);
        public bool TryRetain() => _references.TryRetain();

        public void ReleaseReference()
        {
            if (_references.ReleaseReference() && Volatile.Read(ref _deviceDisposed) == 0)
                ReleaseNative(null!);
        }

        internal void DisposeOwner()
        {
            if (Interlocked.Exchange(ref _ownerDisposed, 1) == 0 &&
                _references.ReleaseOwnerReference() &&
                Volatile.Read(ref _deviceDisposed) == 0)
                ReleaseNative(null!);
        }

        internal void DisposeDevice()
        {
            Volatile.Write(ref _deviceDisposed, 1);
            ReleaseNative(null!);
        }

        public void ReleaseNative(GL gl) => Interlocked.Increment(ref _nativeReleaseCount);
    }
    [Fact]
    public void CompletedResourceIsReclaimedWhileAnUnrelatedSubmissionRemainsPending()
    {
        var completedList = new GraphicsCommandListLifecycle(GraphicsQueueType.Copy);
        var unrelatedList = new GraphicsCommandListLifecycle(GraphicsQueueType.Copy);
        var completedRetention = new OpenGLResourceRetention();
        var unrelatedRetention = new OpenGLResourceRetention();
        var completedResource = new InstrumentedNativeResource();
        var unrelatedResource = new InstrumentedNativeResource();

        completedList.Begin();
        completedRetention.Retain(completedResource);
        completedList.End();
        completedList.MarkSubmitted();

        unrelatedList.Begin();
        unrelatedRetention.Retain(unrelatedResource);
        unrelatedList.End();
        unrelatedList.MarkSubmitted();

        completedResource.DisposeOwner();
        unrelatedResource.DisposeOwner();
        Assert.Equal(0, completedResource.NativeReleaseCount);
        Assert.Equal(0, unrelatedResource.NativeReleaseCount);

        // Complete and release only the first list; no queue-wide idle point or later State poll
        // is required to reclaim a resource whose final list reference has gone away.
        completedList.MarkCompleted();
        completedRetention.ReleaseAll();

        Assert.Equal(1, completedResource.NativeReleaseCount);
        Assert.Equal(GraphicsCommandListState.Pending, unrelatedList.State);
        Assert.Equal(0, unrelatedResource.NativeReleaseCount);

        unrelatedList.MarkCompleted();
        unrelatedRetention.ReleaseAll();
        Assert.Equal(1, unrelatedResource.NativeReleaseCount);
    }


}
