using Auralis.Graphics.Abstractions;
using Silk.NET.OpenGLES;

namespace Auralis.Graphics.Backends.OpenGLES;

internal interface IOpenGLESNativeResource
{
    bool TryRetain();
    bool TryRetainDependency();
    void ReleaseReference();
    void ReleaseNative(GL gl);
}

/// <summary>
/// Native device object with one owner reference plus references held by views and recorded/submitted
/// commands. Dispose closes public retains immediately; native deletion occurs after the final use ends.
/// </summary>
internal abstract class OpenGLESNativeDeviceObject : GraphicsDeviceObject, IOpenGLESNativeResource
{
    private readonly OpenGLESGraphicsDevice _device;
    private readonly OpenGLESResourceReferenceState _references = new();

    protected OpenGLESNativeDeviceObject(OpenGLESGraphicsDevice device) : base(device) =>
        _device = device;

    public bool TryRetain() => _references.TryRetain();

    public bool TryRetainDependency() => _references.TryRetainDependency();

    public void ReleaseReference()
    {
        if (_references.ReleaseReference())
            _device.ReleaseResource(this);
    }

    protected void ReleaseOwnerReference()
    {
        if (_references.CloseOwner())
            _device.ReleaseResource(this);
    }

    public abstract void ReleaseNative(GL gl);
}
