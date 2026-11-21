using Auralis.Graphics.Abstractions;
using Auralis.Graphics.Devices;
using Auralis.Graphics.Resources;

namespace Auralis.Graphics.Commands;

/// <summary>Viewport state shared by graphics backends.</summary>
public readonly record struct GraphicsViewport(
    float X,
    float Y,
    float Width,
    float Height,
    float MinDepth = 0,
    float MaxDepth = 1)
{
    public void Validate()
    {
        if (!float.IsFinite(X) || !float.IsFinite(Y) ||
            !float.IsFinite(Width) || !float.IsFinite(Height) ||
            !float.IsFinite(MinDepth) || !float.IsFinite(MaxDepth))
            throw new ArgumentException("Viewport values must be finite.");

        if (Width < 0 || Height < 0)
            throw new ArgumentOutOfRangeException(nameof(Width), "Viewport dimensions cannot be negative.");

        if (MinDepth < 0 || MaxDepth > 1 || MinDepth > MaxDepth)
            throw new ArgumentOutOfRangeException(nameof(MinDepth), "Viewport depth range must satisfy 0 <= MinDepth <= MaxDepth <= 1.");
    }
}

/// <summary>Integer scissor rectangle. Zero width or height intentionally discards all fragments.</summary>
public readonly record struct GraphicsScissorRect(int X, int Y, uint Width, uint Height)
{
    public void Validate()
    {
        if (Width > int.MaxValue || Height > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(Width), "Scissor dimensions exceed the native signed range.");
    }
}

public readonly record struct GraphicsClearColor(float Red, float Green, float Blue, float Alpha)
{
    public void Validate()
    {
        if (!float.IsFinite(Red) || !float.IsFinite(Green) ||
            !float.IsFinite(Blue) || !float.IsFinite(Alpha))
            throw new ArgumentException("Floating-point clear color components must be finite.");
    }
}

public readonly record struct GraphicsClearColorUInt(uint Red, uint Green, uint Blue, uint Alpha);

public readonly record struct GraphicsClearColorSInt(int Red, int Green, int Blue, int Alpha);

/// <summary>Extent and sample count shared by every attachment in one render-target set.</summary>
public readonly record struct GraphicsRenderTargetExtent(uint Width, uint Height, uint SampleCount);

/// <summary>
/// Shared validation for attachment sets. It ensures device ownership, declared usage, format support,
/// matching mip extents, and matching sample counts before a backend records or binds an attachment.
/// </summary>
public static class GraphicsRenderTargetValidation
{
    public static GraphicsRenderTargetExtent Validate(
        IGraphicsDevice device,
        ReadOnlySpan<IGraphicsTextureView> colorTargets,
        IGraphicsTextureView? depthStencilTarget = null)
    {
        ArgumentNullException.ThrowIfNull(device);
        ObjectDisposedException.ThrowIf(device.IsDisposed, device);

        if (colorTargets.IsEmpty && depthStencilTarget is null)
            throw new ArgumentException("At least one color or depth/stencil attachment is required.");

        GraphicsRenderTargetExtent? extent = null;

        for (int i = 0; i < colorTargets.Length; i++)
        {
            IGraphicsTextureView view = colorTargets[i] ??
                throw new ArgumentException("Color attachment views cannot contain null entries.", nameof(colorTargets));
            GraphicsRenderTargetExtent current = ValidateAttachment(
                device, view, GraphicsTextureUsage.ColorAttachment, GraphicsFormatSupport.ColorAttachment);
            MergeExtent(ref extent, current);
        }

        if (depthStencilTarget is not null)
        {
            GraphicsRenderTargetExtent current = ValidateAttachment(
                device, depthStencilTarget,
                GraphicsTextureUsage.DepthStencilAttachment,
                GraphicsFormatSupport.DepthStencilAttachment);
            MergeExtent(ref extent, current);
        }

        return extent!.Value;
    }

    private static GraphicsRenderTargetExtent ValidateAttachment(
        IGraphicsDevice device,
        IGraphicsTextureView view,
        GraphicsTextureUsage requiredUsage,
        GraphicsFormatSupport requiredSupport)
    {
        if (view.IsDisposed || view.Device.IsDisposed)
            throw new ObjectDisposedException(nameof(view));
        if (!ReferenceEquals(view.Device, device))
            throw new ArgumentException("All render attachments must belong to the command list's device.", nameof(view));

        IGraphicsTexture texture = view.Texture ??
            throw new ArgumentException("A texture view must reference a texture.", nameof(view));
        if (texture.IsDisposed || texture.Device.IsDisposed)
            throw new ObjectDisposedException(nameof(texture));
        if (!ReferenceEquals(texture.Device, device))
            throw new ArgumentException("Render attachment textures must belong to the command list's device.", nameof(view));

        GraphicsTextureDescription description = texture.Description;
        GraphicsTextureViewDescription viewDescription = view.Description;
        viewDescription.Validate(description);

        if ((description.Usage & requiredUsage) != requiredUsage)
            throw new GraphicsValidationException(
                $"Texture usage {description.Usage} does not include required render-target usage {requiredUsage}.");

        GraphicsFormat format = viewDescription.Format == GraphicsFormat.Unknown
            ? description.Format
            : viewDescription.Format;
        if (!device.Capabilities.SupportsFormat(format, requiredSupport))
            throw new GraphicsException($"Format {format} does not support {requiredSupport}.");
        if (!device.Capabilities.SupportsSampleCount(format, description.SampleCount))
            throw new GraphicsException($"Format {format} does not support {description.SampleCount} samples.");

        uint width = Math.Max(1u, description.Width >> checked((int)viewDescription.BaseMipLevel));
        uint height = description.Dimension == GraphicsTextureDimension.Texture1D
            ? 1u
            : Math.Max(1u, description.Height >> checked((int)viewDescription.BaseMipLevel));

        return new GraphicsRenderTargetExtent(width, height, description.SampleCount);
    }

    private static void MergeExtent(
        ref GraphicsRenderTargetExtent? expected,
        GraphicsRenderTargetExtent actual)
    {
        if (expected is null)
        {
            expected = actual;
            return;
        }

        if (expected.Value != actual)
            throw new GraphicsValidationException(
                "All render attachments must have identical mip extents and sample counts.");
    }
}
