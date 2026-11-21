using Auralis.Graphics.Abstractions;
using Auralis.Graphics.Pipelines;
using Auralis.Graphics.Resources;
using Silk.NET.OpenGL;

namespace Auralis.Graphics.Backends.OpenGL;

/// <summary>Compute pipeline backed by a linked OpenGL compute program.</summary>
internal sealed class OpenGLGraphicsComputePipeline : OpenGLNativeDeviceObject, IGraphicsComputePipeline
{
    private readonly OpenGLGraphicsDevice _device;
    private uint _program;

    private OpenGLGraphicsComputePipeline(
        OpenGLGraphicsDevice device,
        GraphicsComputePipelineDescription description,
        uint program) : base(device)
    {
        _device = device;
        Description = description;
        Layout = description.Layout;
        _program = program;
    }

    public IGraphicsPipelineLayout Layout { get; }
    public GraphicsComputePipelineDescription Description { get; }
    public string? DebugName => null;

    internal uint Program
    {
        get
        {
            uint handle = Volatile.Read(ref _program);
            return handle != 0 ? handle : throw new ObjectDisposedException(nameof(OpenGLGraphicsComputePipeline));
        }
    }

    internal static OpenGLGraphicsComputePipeline Create(
        OpenGLGraphicsDevice device,
        GraphicsComputePipelineDescription description)
    {
        ArgumentNullException.ThrowIfNull(description);
        description.Validate();
        if (!device.Capabilities.Features.Supports(GraphicsFeature.ComputeShaders))
            throw new GraphicsFeatureNotSupportedException(
                GraphicsFeature.ComputeShaders, GraphicsFeature.ComputeShaders);
        if (description.Layout is not OpenGLGraphicsPipelineLayout layout ||
            !ReferenceEquals(layout.Device, device))
            throw new GraphicsValidationException("The compute pipeline layout must belong to this OpenGL device.");
        if (description.ComputeShader is not OpenGLGraphicsShader shader)
            throw new GraphicsValidationException("The compute shader must be created by this OpenGL backend.");
        if (shader.Description.Stage != GraphicsShaderStage.Compute)
            throw new GraphicsValidationException("A compute pipeline requires a Compute-stage GLSL shader.");

        return device.WithContext(gl =>
        {
            IGraphicsShader[] shaders = [shader];
            uint program = OpenGLProgramLinker.Link(gl, shaders);
            try
            {
                device.LabelObject(ObjectIdentifier.Program, program, "Auralis Compute Pipeline Program");
                OpenGLGraphicsComputePipeline pipeline = new(device, description, program);
                device.RegisterUnderContext(pipeline);
                return pipeline;
            }
            catch
            {
                gl.DeleteProgram(program);
                throw;
            }
        });
    }

    protected override void DisposeCore() => ReleaseOwnerReference();

    public override void ReleaseNative(GL gl)
    {
        uint program = Interlocked.Exchange(ref _program, 0);
        if (program != 0)
            gl.DeleteProgram(program);
    }
}
