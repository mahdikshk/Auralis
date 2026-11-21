using Auralis.Graphics.Abstractions;
using Auralis.Graphics.Commands;
using Auralis.Graphics.Presentation;

namespace Auralis.Graphics.Backends.OpenGL;

/// <summary>
/// Surface adapter for a host-provided drawable that the injected GL context provider explicitly
/// recognizes as the drawable attached to its current context.
/// </summary>
internal sealed class OpenGLGraphicsPresentationSurface : GraphicsDeviceObject, IGraphicsPresentationSurface
{
    private readonly OpenGLGraphicsDevice _device;
    private readonly object _gate = new();
    private GraphicsPresentationTargetSnapshot _snapshot;

    internal OpenGLGraphicsPresentationSurface(
        OpenGLGraphicsDevice device,
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
        GraphicsPresentationTargetSnapshot snapshot = Target.GetSnapshot();
        snapshot.Validate();

        OpenGLPresentationTargetValidation.ValidateTarget(_device.ContextProvider, snapshot);

        lock (_gate)
        {
            if (snapshot.Revision < _snapshot.Revision)
                throw new GraphicsValidationException("Presentation target revisions must be monotonic.");
            _snapshot = snapshot;
            return _snapshot;
        }
    }

    public bool SupportsPresentation(IGraphicsQueue queue)
    {
        if (IsDisposed || queue is not OpenGLGraphicsCommandQueue openGlQueue)
            return false;
        if (!ReferenceEquals(queue.Device, _device) || openGlQueue.QueueType != GraphicsQueueType.Graphics)
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
            throw new GraphicsValidationException(
                "The selected queue cannot present to the drawable represented by this OpenGL surface.");

        OpenGLPresentationTargetValidation.ValidatePresentMode(_device.ContextProvider, copy.PresentMode);

        _device.ContextProvider.SetPresentMode(copy.PresentMode);
        GraphicsPresentationTargetSnapshot snapshot = RefreshSnapshot();
        if (snapshot.IsSuspended)
            throw new GraphicsValidationException("A swapchain cannot be created while its presentation target is suspended.");

        return new OpenGLGraphicsSwapChain(this, (OpenGLGraphicsCommandQueue)presentationQueue, copy, snapshot);
    }

    protected override void DisposeCore()
    {
        // The target, native window/layer, and context provider remain owned by the host.
    }
}
