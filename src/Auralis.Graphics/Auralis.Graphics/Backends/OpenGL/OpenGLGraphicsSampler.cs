using Auralis.Graphics.Abstractions;
using Auralis.Graphics.Resources;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ARB;

namespace Auralis.Graphics.Backends.OpenGL;

/// <summary>Immutable sampler object implemented with OpenGL sampler-object state.</summary>
internal sealed class OpenGLGraphicsSampler : OpenGLNativeDeviceObject, IGraphicsSampler
{
    private readonly OpenGLGraphicsDevice _device;
    private uint _handle;

    private OpenGLGraphicsSampler(
        OpenGLGraphicsDevice device,
        in GraphicsSamplerDescription description,
        uint handle) : base(device)
    {
        _device = device;
        Description = description;
        _handle = handle;
    }

    public string? DebugName => null;
    public GraphicsSamplerDescription Description { get; }

    internal uint Handle
    {
        get
        {
            uint handle = Volatile.Read(ref _handle);
            return handle != 0 ? handle : throw new ObjectDisposedException(nameof(OpenGLGraphicsSampler));
        }
    }

    internal static OpenGLGraphicsSampler CreateUnderContext(
        OpenGLGraphicsDevice device,
        ArbDirectStateAccess dsa,
        ArbSamplerObjects samplerObjects,
        in GraphicsSamplerDescription description)
    {
        GraphicsSamplerDescription copy = description;
        dsa.CreateSamplers(1, out uint handle);
        if (handle == 0)
            throw new GraphicsException("OpenGL failed to allocate a sampler object.");

        try
        {
            SetState(samplerObjects, device, handle, copy);
            device.LabelObject(ObjectIdentifier.Sampler, handle, "Auralis Sampler");
            OpenGLGraphicsSampler sampler = new(device, copy, handle);
            device.RegisterUnderContext(sampler);
            return sampler;
        }
        catch
        {
            samplerObjects.DeleteSamplers(1, ref handle);
            throw;
        }
    }

    private static void SetState(
        ArbSamplerObjects samplerObjects,
        OpenGLGraphicsDevice device,
        uint handle,
        in GraphicsSamplerDescription description)
    {
        samplerObjects.SamplerParameter(
            handle, SamplerParameterI.MinFilter,
            (int)OpenGLSamplerMapping.ToMinFilter(description.MinFilter, description.MipFilter));
        samplerObjects.SamplerParameter(
            handle, SamplerParameterI.MagFilter,
            (int)OpenGLSamplerMapping.ToMagFilter(description.MagFilter));
        samplerObjects.SamplerParameter(handle, SamplerParameterI.WrapS,
            (int)OpenGLSamplerMapping.ToAddressMode(description.AddressU));
        samplerObjects.SamplerParameter(handle, SamplerParameterI.WrapT,
            (int)OpenGLSamplerMapping.ToAddressMode(description.AddressV));
        samplerObjects.SamplerParameter(handle, SamplerParameterI.WrapR,
            (int)OpenGLSamplerMapping.ToAddressMode(description.AddressW));
        samplerObjects.SamplerParameter(handle, SamplerParameterF.MinLod, description.MinLod);
        samplerObjects.SamplerParameter(handle, SamplerParameterF.MaxLod, description.MaxLod);
        samplerObjects.SamplerParameter(handle, SamplerParameterF.LodBias, description.LodBias);

        samplerObjects.SamplerParameter(
            handle, SamplerParameterI.CompareMode,
            description.ComparisonEnabled
                ? (int)TextureCompareMode.CompareRefToTexture
                : (int)TextureCompareMode.None);
        samplerObjects.SamplerParameter(
            handle, SamplerParameterI.CompareFunc,
            (int)OpenGLSamplerMapping.ToCompareFunction(description.CompareOperation));

        SetBorderColor(samplerObjects, handle, description.BorderColor);

        if (description.MaxAnisotropy > 1)
        {
            if (!device.Capabilities.Features.Supports(Auralis.Graphics.Abstractions.GraphicsFeature.SamplerAnisotropy))
                throw new GraphicsValidationException("Anisotropic filtering is not supported by this OpenGL context.");
            samplerObjects.SamplerParameter(handle, SamplerParameterF.MaxAnisotropy, description.MaxAnisotropy);
        }
    }

    private static void SetBorderColor(
        ArbSamplerObjects samplerObjects,
        uint handle,
        GraphicsBorderColor borderColor)
    {
        Span<float> floatColor = stackalloc float[4];
        Span<int> intColor = stackalloc int[4];

        bool integer = borderColor is GraphicsBorderColor.IntTransparentBlack or
            GraphicsBorderColor.IntOpaqueBlack or GraphicsBorderColor.IntOpaqueWhite;
        bool white = borderColor is GraphicsBorderColor.FloatOpaqueWhite or GraphicsBorderColor.IntOpaqueWhite;
        bool opaque = borderColor is not (GraphicsBorderColor.FloatTransparentBlack or GraphicsBorderColor.IntTransparentBlack);

        if (white)
        {
            floatColor.Fill(1);
            intColor.Fill(1);
        }
        else
        {
            floatColor.Clear();
            intColor.Clear();
        }

        if (opaque)
        {
            floatColor[3] = 1;
            intColor[3] = 1;
        }

        if (integer)
        {
            SamplerParameterI integerBorderColor = (SamplerParameterI)(int)TextureParameterName.TextureBorderColor;
            samplerObjects.SamplerParameter(handle, integerBorderColor, intColor);
        }
        else
        {
            samplerObjects.SamplerParameter(handle, SamplerParameterF.BorderColor, floatColor);
        }
    }

    protected override void DisposeCore() => ReleaseOwnerReference();

    public override void ReleaseNative(GL gl)
    {
        uint handle = Interlocked.Exchange(ref _handle, 0);
        if (handle == 0)
            return;

        // The delete entry point is exposed by the same Silk sampler-object wrapper used for creation.
        _device.SamplerObjects.DeleteSamplers(1, ref handle);
    }
}
