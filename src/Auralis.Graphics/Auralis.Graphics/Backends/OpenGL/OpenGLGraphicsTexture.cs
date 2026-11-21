using Auralis.Graphics.Abstractions;
using Auralis.Graphics.Devices;
using Auralis.Graphics.Resources;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ARB;

namespace Auralis.Graphics.Backends.OpenGL;

/// <summary>Immutable-storage OpenGL texture. Its GL name and all views are device-owned.</summary>
internal sealed class OpenGLGraphicsTexture : OpenGLNativeDeviceObject, IGraphicsTexture
{
    private readonly OpenGLGraphicsDevice _device;
    private uint _handle;

    private OpenGLGraphicsTexture(
        OpenGLGraphicsDevice device,
        in GraphicsTextureDescription description,
        uint handle) : base(device)
    {
        _device = device;
        Description = description;
        _handle = handle;
    }

    public string? DebugName => null;
    public GraphicsTextureDescription Description { get; }

    internal uint Handle
    {
        get
        {
            uint handle = Volatile.Read(ref _handle);
            return handle != 0 ? handle : throw new ObjectDisposedException(nameof(OpenGLGraphicsTexture));
        }
    }

    internal static OpenGLGraphicsTexture CreateUnderContext(
        OpenGLGraphicsDevice device,
        GL gl,
        ArbDirectStateAccess dsa,
        in GraphicsTextureDescription description)
    {
        GraphicsTextureDescription copy = description;
        if (!OpenGLFormatCapabilities.TryGetInternalFormat(copy.Format, out SizedInternalFormat format, out _))
            throw new GraphicsValidationException($"OpenGL has no internal-format mapping for {copy.Format}.");

        TextureTarget target = OpenGLTextureTargetMapping.ForTexture(copy);
        dsa.CreateTextures(target, 1, out uint handle);
        if (handle == 0)
            throw new GraphicsException("OpenGL failed to allocate a texture name.");

        try
        {
            AllocateStorage(dsa, handle, target, format, copy);
            device.LabelObject(ObjectIdentifier.Texture, handle, "Auralis Texture");
            OpenGLGraphicsTexture texture = new(device, copy, handle);
            device.RegisterUnderContext(texture);
            return texture;
        }
        catch
        {
            gl.DeleteTexture(handle);
            throw;
        }
    }

    private static void AllocateStorage(
        ArbDirectStateAccess dsa,
        uint handle,
        TextureTarget target,
        SizedInternalFormat format,
        in GraphicsTextureDescription description)
    {
        uint levels = description.MipLevels;
        uint width = description.Width;
        uint height = description.Height;
        uint depth = description.Depth;

        switch (target)
        {
            case TextureTarget.Texture1D:
                dsa.TextureStorage1D(handle, levels, format, width);
                break;
            case TextureTarget.Texture1DArray:
                dsa.TextureStorage2D(handle, levels, format, width, description.ArrayLayers);
                break;
            case TextureTarget.Texture2D:
                dsa.TextureStorage2D(handle, levels, format, width, height);
                break;
            case TextureTarget.Texture2DArray:
                dsa.TextureStorage3D(handle, levels, format, width, height, description.ArrayLayers);
                break;
            case TextureTarget.Texture3D:
                dsa.TextureStorage3D(handle, levels, format, width, height, depth);
                break;
            case TextureTarget.TextureCubeMap:
                dsa.TextureStorage2D(handle, levels, format, width, height);
                break;
            case TextureTarget.TextureCubeMapArray:
                dsa.TextureStorage3D(handle, levels, format, width, height, description.ArrayLayers);
                break;
            case TextureTarget.Texture2DMultisample:
                dsa.TextureStorage2DMultisample(
                    handle, description.SampleCount, format, width, height, fixedsamplelocations: true);
                break;
            case TextureTarget.Texture2DMultisampleArray:
                dsa.TextureStorage3DMultisample(
                    handle, description.SampleCount, format, width, height, description.ArrayLayers,
                    fixedsamplelocations: true);
                break;
            default:
                throw new GraphicsValidationException($"OpenGL texture target {target} is not supported.");
        }
    }

    public IGraphicsTextureView CreateView(in GraphicsTextureViewDescription description)
    {
        ThrowIfDisposed();
        GraphicsTextureViewDescription viewDescription = description;
        _device.Capabilities.ValidateTextureViewDescription(Description, viewDescription);

        return _device.WithContext(gl =>
            OpenGLGraphicsTextureView.CreateUnderContext(
                _device, gl, _device.DirectStateAccess, _device.TextureViewApi, this, viewDescription));
    }

    protected override void DisposeCore() => ReleaseOwnerReference();

    public override void ReleaseNative(GL gl)
    {
        uint handle = Interlocked.Exchange(ref _handle, 0);
        if (handle != 0)
            gl.DeleteTexture(handle);
    }
}

/// <summary>Immutable-storage OpenGL texture view, retaining its parent resource identity.</summary>
internal sealed class OpenGLGraphicsTextureView : OpenGLNativeDeviceObject, IGraphicsTextureView
{
    private readonly OpenGLGraphicsDevice _device;
    private uint _handle;

    private OpenGLGraphicsTextureView(
        OpenGLGraphicsDevice device,
        OpenGLGraphicsTexture texture,
        in GraphicsTextureViewDescription description,
        uint handle) : base(device)
    {
        _device = device;
        Texture = texture;
        Description = description;
        _handle = handle;
    }

    public string? DebugName => null;
    public IGraphicsTexture Texture { get; }
    public GraphicsTextureViewDescription Description { get; }

    internal uint Handle
    {
        get
        {
            uint handle = Volatile.Read(ref _handle);
            return handle != 0 ? handle : throw new ObjectDisposedException(nameof(OpenGLGraphicsTextureView));
        }
    }

    internal static OpenGLGraphicsTextureView CreateUnderContext(
        OpenGLGraphicsDevice device,
        GL gl,
        ArbDirectStateAccess dsa,
        ArbTextureView textureViewApi,
        OpenGLGraphicsTexture texture,
        in GraphicsTextureViewDescription description)
    {
        GraphicsTextureViewDescription copy = description;
        GraphicsFormat format = copy.Format == GraphicsFormat.Unknown
            ? texture.Description.Format
            : copy.Format;

        if (!OpenGLFormatCapabilities.TryGetInternalFormat(format, out SizedInternalFormat internalFormat, out _))
            throw new GraphicsValidationException($"OpenGL has no internal-format mapping for texture-view format {format}.");

        TextureTarget target = OpenGLTextureTargetMapping.ForView(copy.ViewType);
        // glTextureView requires a generated name that has not yet been initialized/bound to a target.
        // glCreateTextures would fix the target too early and make the view call invalid.
        uint handle = gl.GenTexture();
        if (handle == 0)
            throw new GraphicsException("OpenGL failed to allocate a texture-view name.");

        try
        {
            textureViewApi.TextureView(
                handle,
                target,
                texture.Handle,
                internalFormat,
                copy.BaseMipLevel,
                copy.MipLevelCount,
                copy.BaseArrayLayer,
                copy.ArrayLayerCount);

            device.LabelObject(ObjectIdentifier.Texture, handle, "Auralis Texture View");
            OpenGLGraphicsTextureView view = new(device, texture, copy, handle);
            device.RegisterUnderContext(view);
            return view;
        }
        catch
        {
            gl.DeleteTexture(handle);
            throw;
        }
    }

    protected override void DisposeCore() => ReleaseOwnerReference();

    public override void ReleaseNative(GL gl)
    {
        uint handle = Interlocked.Exchange(ref _handle, 0);
        if (handle != 0)
            gl.DeleteTexture(handle);
    }
}
