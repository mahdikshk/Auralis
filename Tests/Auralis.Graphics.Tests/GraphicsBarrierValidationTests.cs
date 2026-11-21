using Auralis.Graphics.Abstractions;
using Auralis.Graphics.Commands;
using Auralis.Graphics.Devices;
using Auralis.Graphics.Presentation;
using Auralis.Graphics.Resources;
using Auralis.Graphics.Synchronization;
using Xunit;

namespace Auralis.Graphics.Tests;

public sealed class GraphicsBarrierValidationTests
{
    [Fact]
    public void AllGraphicsIncludesEverySupportedGraphicsStage()
    {
        GraphicsPipelineStage expected =
            GraphicsPipelineStage.DrawIndirect |
            GraphicsPipelineStage.VertexInput |
            GraphicsPipelineStage.VertexShader |
            GraphicsPipelineStage.TessellationControlShader |
            GraphicsPipelineStage.TessellationEvaluationShader |
            GraphicsPipelineStage.GeometryShader |
            GraphicsPipelineStage.TaskShader |
            GraphicsPipelineStage.MeshShader |
            GraphicsPipelineStage.FragmentShader |
            GraphicsPipelineStage.DepthStencil |
            GraphicsPipelineStage.ColorAttachmentOutput;

        Assert.Equal(GraphicsPipelineStage.AllGraphics, expected);
        Assert.Equal(GraphicsPipelineStage.None,
            GraphicsPipelineStage.AllGraphics &
            (GraphicsPipelineStage.ComputeShader |
             GraphicsPipelineStage.Transfer |
             GraphicsPipelineStage.Host |
             GraphicsPipelineStage.TopOfPipe |
             GraphicsPipelineStage.BottomOfPipe |
             GraphicsPipelineStage.AllCommands));
    }

    [Fact]
    public void BufferRangeAcceptsValidByteRangeAndRejectsInvalidBounds()
    {
        var description = new GraphicsBufferDescription(1024, GraphicsBufferUsage.Storage);

        new GraphicsBufferRange(128, 256).Validate(description);
        new GraphicsBufferRange(1023, 1).Validate(description);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new GraphicsBufferRange(1024, 1).Validate(description));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new GraphicsBufferRange(0, 0).Validate(description));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new GraphicsBufferRange(ulong.MaxValue, 2).Validate(description));
    }

    [Fact]
    public void TextureRangeValidatesMipLevelsAndArrayLayers()
    {
        var description = new GraphicsTextureDescription(
            GraphicsTextureDimension.Texture2D,
            GraphicsFormat.RGBA8UNorm,
            1024, 1024, 1,
            MipLevels: 11,
            ArrayLayers: 8,
            SampleCount: 1,
            Usage: GraphicsTextureUsage.Sampled);

        new GraphicsTextureSubresourceRange(
            GraphicsTextureAspect.Color, 4, 3, 2, 4).Validate(description);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new GraphicsTextureSubresourceRange(GraphicsTextureAspect.Color, 10, 2, 0, 1).Validate(description));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new GraphicsTextureSubresourceRange(GraphicsTextureAspect.Color, 0, 1, 7, 2).Validate(description));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new GraphicsTextureSubresourceRange(GraphicsTextureAspect.Color, 0, 0, 0, 1).Validate(description));
    }

    [Fact]
    public void TextureRangeValidatesFormatAspects()
    {
        var depthOnly = new GraphicsTextureDescription(
            GraphicsTextureDimension.Texture2D,
            GraphicsFormat.D32Float,
            128, 128, 1, 1, 1, 1,
            GraphicsTextureUsage.DepthStencilAttachment);

        new GraphicsTextureSubresourceRange(
            GraphicsTextureAspect.Depth, 0, 1, 0, 1).Validate(depthOnly);

        Assert.Throws<ArgumentException>(() =>
            new GraphicsTextureSubresourceRange(GraphicsTextureAspect.Stencil, 0, 1, 0, 1).Validate(depthOnly));
        Assert.Throws<ArgumentException>(() =>
            new GraphicsTextureSubresourceRange(GraphicsTextureAspect.Color, 0, 1, 0, 1).Validate(depthOnly));

        var depthStencil = depthOnly with { Format = GraphicsFormat.D24UNormS8UInt };
        new GraphicsTextureSubresourceRange(
            GraphicsTextureAspect.Depth | GraphicsTextureAspect.Stencil, 0, 1, 0, 1)
            .Validate(depthStencil);
    }

    [Fact]
    public void BufferBarrierValidatesOptionalByteRangeAndWholeResourceBarrier()
    {
        var device = new GraphicsDeviceFactoryTests.TestDevice(
            GraphicsBackend.Vulkan, GraphicsFeature.None, GraphicsQueueCapabilities.Graphics);
        var buffer = new TestBuffer(device, new GraphicsBufferDescription(1024, GraphicsBufferUsage.Storage));

        var barrier = new GraphicsResourceBarrier(
            buffer,
            GraphicsPipelineStage.ComputeShader,
            GraphicsPipelineStage.VertexShader,
            GraphicsResourceAccess.ShaderWrite,
            GraphicsResourceAccess.ShaderRead,
            BufferRange: new GraphicsBufferRange(128, 512));

        barrier.Validate();
        (barrier with { BufferRange = null }).Validate();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            (barrier with { BufferRange = new GraphicsBufferRange(900, 200) }).Validate());
        Assert.Throws<ArgumentException>(() =>
            (barrier with { TextureRange = new GraphicsTextureSubresourceRange(
                GraphicsTextureAspect.Color, 0, 1, 0, 1) }).Validate());
    }

    [Fact]
    public void TextureBarrierValidatesOptionalSubresourceRange()
    {
        var device = new GraphicsDeviceFactoryTests.TestDevice(
            GraphicsBackend.Vulkan, GraphicsFeature.None, GraphicsQueueCapabilities.Graphics);
        var texture = new TestTexture(device, new GraphicsTextureDescription(
            GraphicsTextureDimension.Texture2D,
            GraphicsFormat.RGBA8UNorm,
            256, 256, 1, 9, 4, 1,
            GraphicsTextureUsage.Sampled | GraphicsTextureUsage.CopyDestination));

        var barrier = new GraphicsResourceBarrier(
            texture,
            GraphicsPipelineStage.Transfer,
            GraphicsPipelineStage.FragmentShader,
            GraphicsResourceAccess.TransferWrite,
            GraphicsResourceAccess.ShaderRead,
            GraphicsTextureLayout.TransferDestination,
            GraphicsTextureLayout.ShaderReadOnly,
            TextureRange: new GraphicsTextureSubresourceRange(
                GraphicsTextureAspect.Color, 2, 3, 1, 2));

        barrier.Validate();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            (barrier with { TextureRange = new GraphicsTextureSubresourceRange(
                GraphicsTextureAspect.Color, 8, 2, 0, 1) }).Validate());
        Assert.Throws<ArgumentException>(() =>
            (barrier with { BufferRange = new GraphicsBufferRange(0, 16) }).Validate());
    }

    [Fact]
    public void BarrierRejectsUnsupportedResourceTypesAndTextureViews()
    {
        var device = new GraphicsDeviceFactoryTests.TestDevice(
            GraphicsBackend.Vulkan, GraphicsFeature.None, GraphicsQueueCapabilities.Graphics);
        var unsupported = new TestUnsupportedResource(device);

        var unsupportedBarrier = new GraphicsResourceBarrier(
            unsupported,
            GraphicsPipelineStage.Transfer,
            GraphicsPipelineStage.Transfer,
            GraphicsResourceAccess.MemoryWrite,
            GraphicsResourceAccess.MemoryRead);

        Assert.Throws<ArgumentException>(unsupportedBarrier.Validate);

        var texture = new TestTexture(device, new GraphicsTextureDescription(
            GraphicsTextureDimension.Texture2D,
            GraphicsFormat.RGBA8UNorm,
            32, 32, 1, 1, 1, 1,
            GraphicsTextureUsage.Sampled));
        var view = new TestTextureView(device, texture);

        var viewBarrier = new GraphicsResourceBarrier(
            view,
            GraphicsPipelineStage.Transfer,
            GraphicsPipelineStage.FragmentShader,
            GraphicsResourceAccess.TransferWrite,
            GraphicsResourceAccess.ShaderRead,
            GraphicsTextureLayout.TransferDestination,
            GraphicsTextureLayout.ShaderReadOnly);

        Assert.Throws<ArgumentException>(viewBarrier.Validate);
    }

    private sealed class TestBuffer(
        IGraphicsDevice device,
        GraphicsBufferDescription description) : GraphicsDeviceObject(device), IGraphicsBuffer
    {
        public string? DebugName => null;
        public GraphicsBufferDescription Description { get; } = description;
        public Memory<byte> Map(GraphicsMapMode mode, ulong offset = 0, ulong size = ulong.MaxValue) =>
            throw new NotSupportedException();
        public void Unmap() { }
        protected override void DisposeCore() { }
    }

    private sealed class TestTexture(
        IGraphicsDevice device,
        GraphicsTextureDescription description) : GraphicsDeviceObject(device), IGraphicsTexture
    {
        public string? DebugName => null;
        public GraphicsTextureDescription Description { get; } = description;
        public IGraphicsTextureView CreateView(in GraphicsTextureViewDescription description) =>
            throw new NotSupportedException();
        protected override void DisposeCore() { }
    }

    private sealed class TestTextureView(
        IGraphicsDevice device,
        IGraphicsTexture texture) : GraphicsDeviceObject(device), IGraphicsTextureView
    {
        public string? DebugName => null;
        public IGraphicsTexture Texture { get; } = texture;
        public GraphicsTextureViewDescription Description { get; } = new(
            GraphicsFormat.Unknown, GraphicsTextureViewType.Texture2D, 0, 1, 0, 1);
        protected override void DisposeCore() { }
    }

    private sealed class TestUnsupportedResource(IGraphicsDevice device)
        : GraphicsDeviceObject(device), IGraphicsResource
    {
        public string? DebugName => null;
        protected override void DisposeCore() { }
    }
}
