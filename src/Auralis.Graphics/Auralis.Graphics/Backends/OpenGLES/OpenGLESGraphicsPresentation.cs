using System.Diagnostics;
using Auralis.Graphics.Abstractions;
using Auralis.Graphics.Commands;
using Auralis.Graphics.Presentation;
using Auralis.Graphics.Resources;
using Auralis.Graphics.Synchronization;
using Silk.NET.OpenGLES;

namespace Auralis.Graphics.Backends.OpenGLES;

/// <summary>Presentation surface tied to a host-supplied GLES drawable; it never owns the native target.</summary>
internal sealed class OpenGLESGraphicsPresentationSurface : GraphicsDeviceObject, IGraphicsPresentationSurface
{
    private readonly OpenGLESGraphicsDevice _device;
    private readonly object _gate = new();
    private GraphicsPresentationTargetSnapshot _snapshot;

    internal OpenGLESGraphicsPresentationSurface(
        OpenGLESGraphicsDevice device,
        IGraphicsPresentationTarget target,
        in GraphicsPresentationTargetSnapshot snapshot) : base(device)
    {
        _device = device;
        Target = target;
        snapshot.Validate();
        _snapshot = snapshot;
    }

    public IGraphicsPresentationTarget Target { get; }

    public GraphicsPresentationTargetSnapshot Snapshot
    {
        get
        {
            ThrowIfDisposed();
            lock (_gate)
                return _snapshot;
        }
    }

    internal GraphicsPresentationTargetSnapshot RefreshSnapshot()
    {
        ThrowIfDisposed();
        GraphicsPresentationTargetSnapshot current = Target.GetSnapshot();
        current.Validate();
        if (!_device.ContextProvider.IsPresentationTargetCompatible(current))
            throw new GraphicsValidationException("The host target no longer matches the injected OpenGL ES drawable.");

        lock (_gate)
        {
            if (current.Revision < _snapshot.Revision)
                throw new GraphicsValidationException("Presentation target revisions must increase monotonically.");
            _snapshot = current;
            return current;
        }
    }

    public bool SupportsPresentation(IGraphicsQueue queue)
    {
        if (IsDisposed || queue is not OpenGLESGraphicsCommandQueue glesQueue)
            return false;
        if (!ReferenceEquals(queue.Device, _device) || glesQueue.QueueType != GraphicsQueueType.Graphics)
            return false;
        if (!_device.ContextProvider.SupportsPresentation)
            return false;

        try
        {
            GraphicsPresentationTargetSnapshot current = Target.GetSnapshot();
            current.Validate();
            return _device.ContextProvider.IsPresentationTargetCompatible(current);
        }
        catch
        {
            return false;
        }
    }

    public IGraphicsSwapChain CreateSwapChain(
        IGraphicsQueue presentationQueue,
        in GraphicsSwapChainDescription description)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(presentationQueue);
        GraphicsSwapChainDescription copy = description;
        copy.Validate();
        if (!SupportsPresentation(presentationQueue))
            throw new GraphicsValidationException("The selected graphics queue cannot present to this GLES drawable.");
        if (!_device.ContextProvider.SupportsPresentMode(copy.PresentMode))
            throw new NotSupportedException($"The host drawable does not support present mode {copy.PresentMode}.");

        GraphicsPresentationTargetSnapshot snapshot = RefreshSnapshot();
        if (snapshot.IsSuspended)
            throw new GraphicsValidationException("A swapchain cannot be created for a suspended drawable.");
        if (copy.Width != snapshot.Extent.Width || copy.Height != snapshot.Extent.Height)
            throw new GraphicsValidationException("The swapchain extent must match the current host drawable extent.");

        _device.ContextProvider.SetPresentMode(copy.PresentMode);
        return new OpenGLESGraphicsSwapChain(this, (OpenGLESGraphicsCommandQueue)presentationQueue, copy, snapshot);
    }

    protected override void DisposeCore() { }
}

internal sealed class OpenGLESSwapChainImage
{
    internal required OpenGLESGraphicsTexture Texture;
    internal uint Framebuffer;
    internal nint PresentSync;
    internal bool Acquired;
}

/// <summary>
/// Swapchain implemented with backend-owned 2D color textures. Present blits to framebuffer zero and asks
/// the supplied host provider to swap its drawable. The GLES backend creates no platform context or surface.
/// </summary>
internal sealed class OpenGLESGraphicsSwapChain : OpenGLESNativeDeviceObject, IGraphicsSwapChain
{
    private readonly OpenGLESGraphicsPresentationSurface _surface;
    private readonly OpenGLESGraphicsCommandQueue _queue;
    private readonly OpenGLESGraphicsDevice _device;
    private OpenGLESSwapChainImage[] _images;
    private GraphicsSwapChainDescription _description;
    private ulong _generation = 1;
    private ulong _surfaceRevision;
    private uint _nextImage;

    internal OpenGLESGraphicsSwapChain(
        OpenGLESGraphicsPresentationSurface surface,
        OpenGLESGraphicsCommandQueue queue,
        in GraphicsSwapChainDescription description,
        in GraphicsPresentationTargetSnapshot snapshot) : base((OpenGLESGraphicsDevice)surface.Device)
    {
        _surface = surface;
        _queue = queue;
        _device = (OpenGLESGraphicsDevice)surface.Device;
        _description = description;
        _surfaceRevision = snapshot.Revision;
        _images = CreateImages(description);
        _device.WithContext(_ => _device.RegisterUnderContext(this));
    }

    public GraphicsSwapChainDescription Description
    {
        get { ThrowIfDisposed(); return _description; }
    }

    public IGraphicsQueue PresentationQueue => _queue;

    public ulong Generation
    {
        get { ThrowIfDisposed(); return Volatile.Read(ref _generation); }
    }

    public GraphicsAcquireResult AcquireNextImage(TimeSpan timeout)
    {
        ThrowIfDisposed();
        if (timeout < TimeSpan.Zero && timeout != Timeout.InfiniteTimeSpan)
            throw new ArgumentOutOfRangeException(nameof(timeout));

        GraphicsPresentationTargetSnapshot snapshot = _surface.RefreshSnapshot();
        if (snapshot.IsSuspended)
            return new GraphicsAcquireResult(GraphicsAcquireStatus.Suspended, default);
        if (snapshot.Revision != _surfaceRevision ||
            snapshot.Extent.Width != _description.Width || snapshot.Extent.Height != _description.Height)
            return new GraphicsAcquireResult(GraphicsAcquireStatus.OutOfDate, default);

        long start = Stopwatch.GetTimestamp();
        long timeoutTicks = timeout == Timeout.InfiniteTimeSpan
            ? long.MaxValue
            : (long)(timeout.TotalSeconds * Stopwatch.Frequency);

        while (true)
        {
            (int index, bool anyUnacquired) result = _device.WithContext(gl =>
            {
                bool anyUnacquired = false;
                for (int attempt = 0; attempt < _images.Length; attempt++)
                {
                    uint index = (_nextImage + (uint)attempt) % (uint)_images.Length;
                    OpenGLESSwapChainImage image = _images[index];
                    if (image.Acquired)
                        continue;

                    anyUnacquired = true;
                    if (image.PresentSync != 0)
                    {
                        GLEnum status = gl.ClientWaitSync(image.PresentSync, (uint)0, 0);
                        if (status == GLEnum.TimeoutExpired)
                            continue;
                        if (status == GLEnum.WaitFailed)
                            throw new GraphicsException("OpenGL ES failed while waiting to reacquire a presented image.");
                        gl.DeleteSync(image.PresentSync);
                        image.PresentSync = 0;
                    }

                    image.Acquired = true;
                    _nextImage = (index + 1) % (uint)_images.Length;
                    return ((int)index, true);
                }

                return (-1, anyUnacquired);
            });

            if (result.index >= 0)
            {
                int index = result.index;
                return new GraphicsAcquireResult(
                    GraphicsAcquireStatus.Success,
                    new GraphicsAcquiredImage((uint)index, _generation, _images[index].Texture));
            }

            if (!result.anyUnacquired || timeoutTicks == 0 ||
                (timeoutTicks != long.MaxValue && Stopwatch.GetTimestamp() - start >= timeoutTicks))
                return new GraphicsAcquireResult(GraphicsAcquireStatus.Timeout, default);

            Thread.Yield();
        }
    }

    public GraphicsPresentStatus Present(
        in GraphicsAcquiredImage image,
        IGraphicsFence? waitFence = null,
        ulong waitValue = 0)
    {
        ThrowIfDisposed();
        if (waitFence is null && waitValue != 0)
            throw new ArgumentException("A non-zero wait value requires a wait fence.", nameof(waitValue));

        if (image.Generation != _generation || image.ImageIndex >= (uint)_images.Length)
            return GraphicsPresentStatus.OutOfDate;

        OpenGLESSwapChainImage slot = _images[image.ImageIndex];
        if (!slot.Acquired || !ReferenceEquals(slot.Texture, image.Texture))
            throw new GraphicsValidationException("The acquired GLES image is stale or belongs to another swapchain.");

        GraphicsPresentationTargetSnapshot snapshot = _surface.RefreshSnapshot();
        if (snapshot.IsSuspended)
        {
            slot.Acquired = false;
            return GraphicsPresentStatus.Suspended;
        }
        if (snapshot.Revision != _surfaceRevision ||
            snapshot.Extent.Width != _description.Width || snapshot.Extent.Height != _description.Height)
        {
            slot.Acquired = false;
            return GraphicsPresentStatus.OutOfDate;
        }

        OpenGLESGraphicsFence? fence = null;
        if (waitFence is not null)
        {
            fence = waitFence as OpenGLESGraphicsFence;
            if (fence is null || !ReferenceEquals(fence.Device, _device))
                throw new GraphicsValidationException("The wait fence must belong to this GLES device.");
        }

        _device.WithContext(gl =>
        {
            fence?.EnqueueGpuWait(gl, waitValue);
            gl.BindFramebuffer(FramebufferTarget.ReadFramebuffer, slot.Framebuffer);
            gl.BindFramebuffer(FramebufferTarget.DrawFramebuffer, 0);
            gl.BlitFramebuffer(
                0, 0,
                checked((int)_description.Width),
                checked((int)_description.Height),
                0, 0,
                checked((int)snapshot.Extent.Width),
                checked((int)snapshot.Extent.Height),
                (uint)GLEnum.ColorBufferBit,
                GLEnum.Linear);

            nint sync = gl.FenceSync(SyncCondition.SyncGpuCommandsComplete, (uint)0);
            if (sync == 0)
                throw new GraphicsException("OpenGL ES failed to create a presentation sync object.");

            if (slot.PresentSync != 0)
                gl.DeleteSync(slot.PresentSync);
            slot.PresentSync = sync;
            gl.Flush();
            _device.ContextProvider.SwapBuffers();
            slot.Acquired = false;
        });

        return GraphicsPresentStatus.Presented;
    }

    public void Resize(in GraphicsSwapChainDescription description)
    {
        ThrowIfDisposed();
        GraphicsSwapChainDescription copy = description;
        copy.Validate();
        if (!_surface.SupportsPresentation(_queue))
            throw new GraphicsValidationException("The swapchain queue is no longer compatible with the host drawable.");
        if (!_device.ContextProvider.SupportsPresentMode(copy.PresentMode))
            throw new NotSupportedException($"The host drawable does not support present mode {copy.PresentMode}.");

        GraphicsPresentationTargetSnapshot snapshot = _surface.RefreshSnapshot();
        if (snapshot.IsSuspended ||
            snapshot.Extent.Width != copy.Width || snapshot.Extent.Height != copy.Height)
            throw new GraphicsValidationException("The replacement swapchain extent must match a non-suspended host drawable.");

        _device.ContextProvider.SetPresentMode(copy.PresentMode);
        // Allocate the replacement set first so a failed resize leaves the existing swapchain usable.
        OpenGLESSwapChainImage[] replacement = CreateImages(copy);
        OpenGLESSwapChainImage[] old = _images;
        try
        {
            _device.WithContext(gl =>
            {
                gl.Finish();
                ReleaseImageNative(gl, old);
            });
        }
        catch
        {
            _device.WithContext(gl => ReleaseImageNative(gl, replacement));
            for (int i = 0; i < replacement.Length; i++)
                replacement[i].Texture.Dispose();
            throw;
        }

        for (int i = 0; i < old.Length; i++)
            old[i].Texture.Dispose();

        _description = copy;
        _surfaceRevision = snapshot.Revision;
        _nextImage = 0;
        _generation = checked(_generation + 1);
        _images = replacement;
    }

    private OpenGLESSwapChainImage[] CreateImages(in GraphicsSwapChainDescription description)
    {
        if (description.BufferCount is < 2 or > 8)
            throw new GraphicsValidationException("The GLES swapchain supports two to eight buffered images.");

        OpenGLESSwapChainImage[] images = new OpenGLESSwapChainImage[checked((int)description.BufferCount)];
        GraphicsTextureDescription textureDescription = new(
            GraphicsTextureDimension.Texture2D,
            description.Format,
            description.Width,
            description.Height,
            1,
            1,
            1,
            1,
            GraphicsTextureUsage.ColorAttachment | GraphicsTextureUsage.CopySource);

        try
        {
            for (int i = 0; i < images.Length; i++)
            {
                OpenGLESGraphicsTexture texture = (OpenGLESGraphicsTexture)_device.CreateTexture(textureDescription);
                uint framebuffer = 0;
                try
                {
                    framebuffer = _device.WithContext(gl =>
                    {
                        uint handle = gl.GenFramebuffer();
                        if (handle == 0)
                            throw new GraphicsException("OpenGL ES failed to create a swapchain framebuffer.");
                        try
                        {
                            gl.BindFramebuffer(FramebufferTarget.Framebuffer, handle);
                            gl.FramebufferTexture2D(
                                FramebufferTarget.Framebuffer,
                                GLEnum.ColorAttachment0,
                                TextureTarget.Texture2D,
                                texture.Handle,
                                0);
                            GLEnum status = gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
                            if (status != GLEnum.FramebufferComplete)
                                throw new GraphicsException($"GLES swapchain framebuffer is incomplete: {status}.");
                            gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
                            return handle;
                        }
                        catch
                        {
                            gl.DeleteFramebuffer(handle);
                            throw;
                        }
                    });
                    images[i] = new OpenGLESSwapChainImage { Texture = texture, Framebuffer = framebuffer };
                }
                catch
                {
                    texture.Dispose();
                    throw;
                }
            }
            return images;
        }
        catch
        {
            _device.WithContext(gl => ReleaseImageNative(gl, images));
            for (int i = 0; i < images.Length; i++)
                images[i]?.Texture.Dispose();
            throw;
        }
    }

    private static void ReleaseImageNative(GL gl, OpenGLESSwapChainImage[] images)
    {
        for (int i = 0; i < images.Length; i++)
        {
            OpenGLESSwapChainImage? image = images[i];
            if (image is null)
                continue;
            if (image.PresentSync != 0)
            {
                gl.DeleteSync(image.PresentSync);
                image.PresentSync = 0;
            }
            if (image.Framebuffer != 0)
            {
                gl.DeleteFramebuffer(image.Framebuffer);
                image.Framebuffer = 0;
            }
            image.Acquired = false;
        }
    }

    protected override void DisposeCore()
    {
        Exception? failure = null;
        try
        {
            ReleaseOwnerReference();
        }
        catch (Exception exception)
        {
            failure = exception;
        }

        for (int i = 0; i < _images.Length; i++)
        {
            try
            {
                _images[i].Texture.Dispose();
            }
            catch (Exception exception)
            {
                failure ??= exception;
            }
        }

        if (failure is not null)
            throw new GraphicsException("GLES swapchain disposal encountered a native cleanup failure.", failure);
    }

    public override void ReleaseNative(GL gl) => ReleaseImageNative(gl, _images);
}
