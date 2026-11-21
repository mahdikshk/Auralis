using Auralis.Graphics.Abstractions;
using Auralis.Graphics.Resources;
using Silk.NET.OpenGLES;

namespace Auralis.Graphics.Backends.OpenGLES;

/// <summary>OpenGL ES sampler object with only core GLES state unless its extension is detected.</summary>
internal sealed class OpenGLESGraphicsSampler : OpenGLESNativeDeviceObject, IGraphicsSampler
{
    private uint _handle;

    private OpenGLESGraphicsSampler(
        OpenGLESGraphicsDevice device,
        in GraphicsSamplerDescription description,
        uint handle) : base(device)
    {
        Description = description;
        _handle = handle;
    }

    public GraphicsSamplerDescription Description { get; }

    public string? DebugName => null;

    internal uint Handle
    {
        get
        {
            uint value = Volatile.Read(ref _handle);
            return value != 0 ? value : throw new ObjectDisposedException(nameof(OpenGLESGraphicsSampler));
        }
    }

    internal static OpenGLESGraphicsSampler CreateUnderContext(
        OpenGLESGraphicsDevice device,
        GL gl,
        in GraphicsSamplerDescription description)
    {
        GraphicsSamplerDescription copy = description;
        if (copy.AddressU is GraphicsAddressMode.ClampToBorder or GraphicsAddressMode.MirrorClampToEdge ||
            copy.AddressV is GraphicsAddressMode.ClampToBorder or GraphicsAddressMode.MirrorClampToEdge ||
            copy.AddressW is GraphicsAddressMode.ClampToBorder or GraphicsAddressMode.MirrorClampToEdge)
            throw new GraphicsValidationException(
                "Clamp-to-border and mirror-clamp-to-edge are not part of the OpenGL ES core sampler state implemented here.");

        if (copy.MaxAnisotropy > 1 &&
            !device.Capabilities.Features.Supports(GraphicsFeature.SamplerAnisotropy))
            throw new GraphicsFeatureNotSupportedException(
                GraphicsFeature.SamplerAnisotropy, GraphicsFeature.SamplerAnisotropy);

        uint handle = gl.GenSampler();
        if (handle == 0)
            throw new GraphicsException("OpenGL ES failed to allocate a sampler object.");

        try
        {
            gl.SamplerParameter(handle, SamplerParameterI.MinFilter, GetMinFilter(copy));
            gl.SamplerParameter(handle, SamplerParameterI.MagFilter, GetMagFilter(copy));
            gl.SamplerParameter(handle, SamplerParameterI.WrapS, GetAddressMode(copy.AddressU));
            gl.SamplerParameter(handle, SamplerParameterI.WrapT, GetAddressMode(copy.AddressV));
            gl.SamplerParameter(handle, SamplerParameterI.WrapR, GetAddressMode(copy.AddressW));
            gl.SamplerParameter(handle, SamplerParameterF.MinLod, copy.MinLod);
            gl.SamplerParameter(handle, SamplerParameterF.MaxLod, copy.MaxLod);
            gl.SamplerParameter(handle, SamplerParameterF.LodBias, copy.LodBias);
            gl.SamplerParameter(
                handle,
                SamplerParameterI.CompareMode,
                copy.ComparisonEnabled ? 0x884E : 0);

            if (copy.ComparisonEnabled)
                gl.SamplerParameter(handle, SamplerParameterI.CompareFunc, GetCompareFunction(copy.CompareOperation));

            if (copy.MaxAnisotropy > 1)
                gl.SamplerParameter(handle, SamplerParameterF.MaxAnisotropy, copy.MaxAnisotropy);

            OpenGLESGraphicsSampler sampler = new(device, copy, handle);
            device.RegisterUnderContext(sampler);
            return sampler;
        }
        catch
        {
            gl.DeleteSampler(handle);
            throw;
        }
    }

    private static int GetMinFilter(in GraphicsSamplerDescription description) =>
        (description.MinFilter, description.MipFilter) switch
        {
            (GraphicsFilter.Nearest, GraphicsFilter.Nearest) => 0x2700, // GL_NEAREST_MIPMAP_NEAREST
            (GraphicsFilter.Linear, GraphicsFilter.Nearest) => 0x2701,  // GL_LINEAR_MIPMAP_NEAREST
            (GraphicsFilter.Nearest, GraphicsFilter.Linear) => 0x2702,  // GL_NEAREST_MIPMAP_LINEAR
            (GraphicsFilter.Linear, GraphicsFilter.Linear) => 0x2703,   // GL_LINEAR_MIPMAP_LINEAR
            _ => throw new ArgumentOutOfRangeException(nameof(description))
        };

    private static int GetMagFilter(in GraphicsSamplerDescription description) =>
        description.MagFilter == GraphicsFilter.Nearest ? 0x2600 : 0x2601;

    private static int GetAddressMode(GraphicsAddressMode mode) => mode switch
    {
        GraphicsAddressMode.Repeat => 0x2901,
        GraphicsAddressMode.MirroredRepeat => 0x8370,
        GraphicsAddressMode.ClampToEdge => 0x812F,
        _ => throw new GraphicsValidationException($"Sampler address mode '{mode}' is not supported by the OpenGL ES backend.")
    };

    private static int GetCompareFunction(GraphicsCompareOperation operation) => operation switch
    {
        GraphicsCompareOperation.Never => 0x0200,
        GraphicsCompareOperation.Less => 0x0201,
        GraphicsCompareOperation.Equal => 0x0202,
        GraphicsCompareOperation.LessEqual => 0x0203,
        GraphicsCompareOperation.Greater => 0x0204,
        GraphicsCompareOperation.NotEqual => 0x0205,
        GraphicsCompareOperation.GreaterEqual => 0x0206,
        GraphicsCompareOperation.Always => 0x0207,
        _ => throw new ArgumentOutOfRangeException(nameof(operation))
    };

    protected override void DisposeCore() => ReleaseOwnerReference();

    public override void ReleaseNative(GL gl)
    {
        uint handle = Interlocked.Exchange(ref _handle, 0);
        if (handle != 0)
            gl.DeleteSampler(handle);
    }
}
