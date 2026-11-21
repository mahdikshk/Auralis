using System.Diagnostics;
using Auralis.Graphics.Abstractions;
using Auralis.Graphics.Commands;
using Auralis.Graphics.Presentation;
using Auralis.Graphics.Resources;
using Auralis.Graphics.Synchronization;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ARB;

namespace Auralis.Graphics.Backends.OpenGL;

internal sealed class OpenGLSwapChainImage
{
    internal required OpenGLGraphicsTexture Texture;
    internal uint Framebuffer;
    internal nint PresentSync;
    internal bool Acquired;
}

/// <summary>
/// Swapchain implemented as a ring of Auralis-owned offscreen color textures. Present blits the
/// selected texture to the provider's actual default drawable and asks that provider to swap it.
/// The default framebuffer is never exposed as an IGraphicsTexture.
/// </summary>
internal sealed class OpenGLGraphicsSwapChain : OpenGLNativeDeviceObject, IGraphicsSwapChain
{
    private readonly OpenGLGraphicsPresentationSurface _surface;
    private readonly OpenGLGraphicsCommandQueue _queue;
    private readonly OpenGLGraphicsDevice _device;
    private OpenGLSwapChainImage[] _images;
    private GraphicsSwapChainDescription _description;
    private ulong _generation = 1;
    private ulong _surfaceRevision;
    private uint _nextImage;
    private int _nativeReleased;

    internal OpenGLGraphicsSwapChain(
        OpenGLGraphicsPresentationSurface surface,
        OpenGLGraphicsCommandQueue queue,
        in GraphicsSwapChainDescription description,
        in GraphicsPresentationTargetSnapshot snapshot) : base((OpenGLGraphicsDevice)surface.Device)
    {
        _surface = surface;
        _queue = queue;
        _device = (OpenGLGraphicsDevice)surface.Device;
        _description = description;
        _surfaceRevision = snapshot.Revision;
        _images = CreateImages(description);
        _device.WithContext(_ => _device.RegisterUnderContext(this));
    }

    public GraphicsSwapChainDescription Description
    {
        get
        {
            ThrowIfDisposed();
            return _description;
        }
    }

    public IGraphicsQueue PresentationQueue => _queue;

    public ulong Generation
    {
        get
        {
            ThrowIfDisposed();
            return Volatile.Read(ref _generation);
        }
    }

    public GraphicsAcquireResult AcquireNextImage(TimeSpan timeout)
    {
        ThrowIfDisposed();
        if (timeout < TimeSpan.Zero && timeout != Timeout.InfiniteTimeSpan)
            throw new ArgumentOutOfRangeException(nameof(timeout));

        GraphicsPresentationTargetSnapshot snapshot;
        try
        {
            snapshot = _surface.RefreshSnapshot();
        }
        catch (GraphicsBackendUnavailableException)
        {
            return new GraphicsAcquireResult(GraphicsAcquireStatus.OutOfDate, default);
        }

        if (snapshot.IsSuspended)
            return new GraphicsAcquireResult(GraphicsAcquireStatus.Suspended, default);

        if (snapshot.Revision != _surfaceRevision)
            return new GraphicsAcquireResult(GraphicsAcquireStatus.OutOfDate, default);

        long start = Stopwatch.GetTimestamp();
        long timeoutTicks = timeout == Timeout.InfiniteTimeSpan
            ? long.MaxValue
            : (long)(timeout.TotalSeconds * Stopwatch.Frequency);

        while (true)
        {
            ulong remainingNanoseconds = GetRemainingNanoseconds(start, timeoutTicks);

            GraphicsAcquireResult result = _device.WithContext(gl =>
            {
                int imageCount = _images.Length;
                for (int attempt = 0; attempt < imageCount; attempt++)
                {
                    uint index = (_nextImage + (uint)attempt) % (uint)imageCount;
                    OpenGLSwapChainImage image = _images[index];
                    if (image.Acquired)
                        continue;

                    if (image.PresentSync != 0)
                    {
                        ARB wait = _device.SyncApi.ClientWaitSync(
                            image.PresentSync,
                            (SyncObjectMask)0,
                            remainingNanoseconds);

                        if (wait == ARB.TimeoutExpired)
                            continue;
                        if (wait == ARB.WaitFailed)
                            throw new GraphicsException("OpenGL failed while waiting for a presented swapchain image.");

                        _device.SyncApi.DeleteSync(image.PresentSync);
                        image.PresentSync = 0;
                    }

                    image.Acquired = true;
                    _nextImage = (index + 1) % (uint)imageCount;
                    return new GraphicsAcquireResult(
                        GraphicsAcquireStatus.Success,
                        new GraphicsAcquiredImage(index, _generation, image.Texture));
                }

                return new GraphicsAcquireResult(GraphicsAcquireStatus.Timeout, default);
            });

            if (result.HasImage)
                return result;
            if (timeoutTicks == 0 || timeoutTicks != long.MaxValue && Stopwatch.GetTimestamp() - start >= timeoutTicks)
                return new GraphicsAcquireResult(GraphicsAcquireStatus.Timeout, default);

            // If every image is held by the application, no GPU wait can make one available.
            bool hasGpuPendingImage = false;
            for (int i = 0; i < _images.Length; i++)
                hasGpuPendingImage |= !_images[i].Acquired && _images[i].PresentSync != 0;

            if (!hasGpuPendingImage)
                return new GraphicsAcquireResult(GraphicsAcquireStatus.Timeout, default);
        }

    }

    public GraphicsPresentStatus Present(
        in GraphicsAcquiredImage image,
        IGraphicsFence? waitFence = null,
        ulong waitValue = 0)
    {
        ThrowIfDisposed();

        if (waitFence is null && waitValue != 0)
            throw new ArgumentException("A non-zero wait value requires a fence.", nameof(waitValue));

        if (image.Generation != _generation || image.ImageIndex >= (uint)_images.Length)
            return GraphicsPresentStatus.OutOfDate;

        OpenGLSwapChainImage slot = _images[image.ImageIndex];
        if (!slot.Acquired || !ReferenceEquals(slot.Texture, image.Texture))
            throw new GraphicsValidationException("The acquired image is stale, already presented, or belongs to another swapchain.");

        GraphicsPresentationTargetSnapshot snapshot;
        try
        {
            snapshot = _surface.RefreshSnapshot();
        }
        catch (GraphicsBackendUnavailableException)
        {
            slot.Acquired = false;
            return GraphicsPresentStatus.OutOfDate;
        }

        if (snapshot.IsSuspended)
        {
            slot.Acquired = false;
            return GraphicsPresentStatus.Suspended;
        }

        if (snapshot.Revision != _surfaceRevision)
        {
            slot.Acquired = false;
            return GraphicsPresentStatus.OutOfDate;
        }

        OpenGLGraphicsFence? openGlFence = null;
        if (waitFence is not null)
        {
            openGlFence = waitFence as OpenGLGraphicsFence;
            if (openGlFence is null || !ReferenceEquals(openGlFence.Device, _device))
                throw new GraphicsValidationException("The present wait fence must belong to this OpenGL device.");
        }

        _device.WithContext(gl =>
        {
            if (openGlFence is not null)
                openGlFence.EnqueueGpuWait(gl, waitValue);

            // The default framebuffer's draw-buffer selection belongs to the current
            // drawable. Do not inherit a stale host setting; select BACK for double-buffered
            // contexts and FRONT for single-buffered contexts.
            gl.BindFramebuffer(FramebufferTarget.DrawFramebuffer, 0);
            gl.DrawBuffer(gl.GetInteger(GLEnum.Doublebuffer) != 0
                ? DrawBufferMode.Back
                : DrawBufferMode.Front);

            _device.DirectStateAccess.BlitNamedFramebuffer(
                slot.Framebuffer,
                0,
                0,
                0,
                checked((int)_description.Width),
                checked((int)_description.Height),
                0,
                0,
                checked((int)snapshot.Extent.Width),
                checked((int)snapshot.Extent.Height),
                ClearBufferMask.ColorBufferBit,
                BlitFramebufferFilter.Linear);

            nint sync = _device.SyncApi.FenceSync(
                SyncCondition.SyncGpuCommandsComplete,
                SyncBehaviorFlags.None);
            if (sync == 0)
                throw new GraphicsException("OpenGL failed to create a presentation completion sync.");

            if (slot.PresentSync != 0)
                _device.SyncApi.DeleteSync(slot.PresentSync);
            slot.PresentSync = sync;
            slot.Acquired = false;

            _device.ContextProvider.SwapBuffers();
        });

        return GraphicsPresentStatus.Presented;
    }

    public void Resize(in GraphicsSwapChainDescription description)
    {
        ThrowIfDisposed();
        GraphicsSwapChainDescription copy = description;
        copy.Validate();

        if (!_surface.SupportsPresentation(_queue))
            throw new GraphicsValidationException("The swapchain's queue is no longer compatible with its surface.");

        OpenGLPresentationTargetValidation.ValidatePresentMode(_device.ContextProvider, copy.PresentMode);
        _device.ContextProvider.SetPresentMode(copy.PresentMode);
        GraphicsPresentationTargetSnapshot snapshot = _surface.RefreshSnapshot();
        if (snapshot.IsSuspended)
            throw new GraphicsValidationException("A swapchain cannot be resized to a suspended presentation target.");

        // Resize invalidates every acquired image and waits for all previous presentation blits
        // before releasing their backing textures/framebuffers.
        OpenGLSwapChainImage[] oldImages = _images;
        _device.WithContext(gl =>
        {
            gl.Finish();
            ReleaseImageNative(gl, oldImages);
        });

        for (int i = 0; i < oldImages.Length; i++)
            oldImages[i].Texture.Dispose();

        _description = copy;
        _surfaceRevision = snapshot.Revision;
        _nextImage = 0;
        _generation = checked(_generation + 1);
        _images = CreateImages(copy);
    }

    private OpenGLSwapChainImage[] CreateImages(in GraphicsSwapChainDescription description)
    {
        if (description.BufferCount > int.MaxValue)
            throw new GraphicsValidationException("The requested swapchain buffer count exceeds the managed array limit.");

        OpenGLSwapChainImage[] images = new OpenGLSwapChainImage[checked((int)description.BufferCount)];
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
                IGraphicsTexture created = _device.CreateTexture(textureDescription);
                if (created is not OpenGLGraphicsTexture texture)
                {
                    created.Dispose();
                    throw new GraphicsException("The OpenGL swapchain texture was created by an incompatible backend.");
                }

                uint framebuffer = 0;
                try
                {
                    framebuffer = _device.WithContext(gl =>
                    {
                        _device.DirectStateAccess.CreateFramebuffers(1, out uint handle);
                        if (handle == 0)
                            throw new GraphicsException("OpenGL failed to create a swapchain framebuffer.");

                        try
                        {
                            _device.DirectStateAccess.NamedFramebufferTexture(
                                handle,
                                FramebufferAttachment.ColorAttachment0,
                                texture.Handle,
                                0);
                            _device.DirectStateAccess.NamedFramebufferDrawBuffer(handle, ColorBuffer.ColorAttachment0);
                            _device.DirectStateAccess.NamedFramebufferReadBuffer(handle, ColorBuffer.ColorAttachment0);

                            ARB status = _device.DirectStateAccess.CheckNamedFramebufferStatus(
                                handle,
                                FramebufferTarget.Framebuffer);
                            if (status != ARB.FramebufferComplete)
                                throw new GraphicsException($"Swapchain framebuffer is incomplete: {status}.");

                            return handle;
                        }
                        catch
                        {
                            gl.DeleteFramebuffer(handle);
                            throw;
                        }
                    });

                    images[i] = new OpenGLSwapChainImage
                    {
                        Texture = texture,
                        Framebuffer = framebuffer
                    };
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

    private void ReleaseImageNative(GL gl, OpenGLSwapChainImage[] images)
    {
        for (int i = 0; i < images.Length; i++)
        {
            OpenGLSwapChainImage? image = images[i];
            if (image is null)
                continue;

            if (image.PresentSync != 0)
            {
                _device.SyncApi.DeleteSync(image.PresentSync);
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

    private static ulong GetRemainingNanoseconds(long startTimestamp, long timeoutTicks)
    {
        if (timeoutTicks == long.MaxValue)
            return ulong.MaxValue;

        long elapsed = Stopwatch.GetTimestamp() - startTimestamp;
        long remainingTicks = Math.Max(0, timeoutTicks - elapsed);
        if (remainingTicks == 0)
            return 0;

        double nanoseconds = remainingTicks * (1_000_000_000d / Stopwatch.Frequency);
        return nanoseconds >= ulong.MaxValue ? ulong.MaxValue : (ulong)nanoseconds;
    }

    protected override void DisposeCore()
    {
        // Enqueue the framebuffer/sync objects before textures so FIFO deferred deletion drops
        // framebuffer references before releasing the attached texture names.
        ReleaseOwnerReference();
        for (int i = 0; i < _images.Length; i++)
            _images[i].Texture.Dispose();
    }

    public override void ReleaseNative(GL gl)
    {
        if (Interlocked.Exchange(ref _nativeReleased, 1) != 0)
            return;
        ReleaseImageNative(gl, _images);
    }
}
