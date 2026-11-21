using Auralis.Graphics.Abstractions;
using Auralis.Graphics.Devices;
using Auralis.Graphics.Pipelines;
using Auralis.Graphics.Resources;
using Silk.NET.OpenGLES;
using Silk.NET.OpenGLES.Extensions.EXT;

namespace Auralis.Graphics.Backends.OpenGLES;

internal abstract class OpenGLESGraphicsPipeline : OpenGLESNativeDeviceObject, IGraphicsPipeline
{
    private uint _program;

    protected OpenGLESGraphicsPipeline(
        OpenGLESGraphicsDevice device,
        IGraphicsPipelineLayout layout,
        uint program) : base(device)
    {
        Layout = layout;
        _program = program;
    }

    public IGraphicsPipelineLayout Layout { get; }

    internal uint Program
    {
        get
        {
            uint value = Volatile.Read(ref _program);
            return value != 0 ? value : throw new ObjectDisposedException(nameof(OpenGLESGraphicsPipeline));
        }
    }

    public override void ReleaseNative(GL gl)
    {
        uint program = Interlocked.Exchange(ref _program, 0);
        if (program != 0)
            gl.DeleteProgram(program);
        ReleaseAdditionalNative(gl);
    }

    protected virtual void ReleaseAdditionalNative(GL gl) { }

    protected override void DisposeCore() => ReleaseOwnerReference();

    protected static uint LinkProgram(
        OpenGLESGraphicsDevice device,
        GL gl,
        ReadOnlySpan<IGraphicsShader> shaders)
    {
        uint program = gl.CreateProgram();
        if (program == 0)
            throw new GraphicsException("OpenGL ES failed to create a program object.");

        try
        {
            for (int i = 0; i < shaders.Length; i++)
            {
                if (shaders[i] is not OpenGLESGraphicsShader shader ||
                    !ReferenceEquals(shader.Device, device) ||
                    shader.IsDisposed)
                    throw new GraphicsValidationException("All pipeline shaders must be live shaders from this GLES device.");
                gl.AttachShader(program, shader.Handle);
            }

            gl.LinkProgram(program);
            gl.GetProgram(program, GLEnum.LinkStatus, out int linked);
            if (linked == 0)
            {
                string diagnostic = gl.GetProgramInfoLog(program);
                throw new GraphicsException($"OpenGL ES program link failed:{Environment.NewLine}{diagnostic}");
            }

            return program;
        }
        catch
        {
            gl.DeleteProgram(program);
            throw;
        }
    }
}

internal sealed unsafe class OpenGLESGraphicsRenderPipeline : OpenGLESGraphicsPipeline, IGraphicsRenderPipeline
{
    private uint _vertexArray;

    private OpenGLESGraphicsRenderPipeline(
        OpenGLESGraphicsDevice device,
        GraphicsRenderPipelineDescription description,
        uint program,
        uint vertexArray) : base(device, description.Layout, program)
    {
        Description = description;
        _vertexArray = vertexArray;

        ReadOnlySpan<IGraphicsShader> shaders = description.Shaders;
        bool hasMeshShader = false;
        bool hasTaskShader = false;
        for (int i = 0; i < shaders.Length; i++)
        {
            hasMeshShader |= shaders[i].Description.Stage == GraphicsShaderStage.Mesh;
            hasTaskShader |= shaders[i].Description.Stage == GraphicsShaderStage.Task;
        }
        IsMeshPipeline = hasMeshShader;
        IsTaskPipeline = hasTaskShader;
    }

    public GraphicsRenderPipelineDescription Description { get; }

    internal bool IsMeshPipeline { get; }

    internal bool IsTaskPipeline { get; }

    internal uint VertexArray
    {
        get
        {
            uint value = Volatile.Read(ref _vertexArray);
            return value != 0 ? value : throw new ObjectDisposedException(nameof(OpenGLESGraphicsRenderPipeline));
        }
    }

    internal static OpenGLESGraphicsRenderPipeline Create(
        OpenGLESGraphicsDevice device,
        GraphicsRenderPipelineDescription description)
    {
        ArgumentNullException.ThrowIfNull(description);
        description.Validate();
        if (description.Layout is not OpenGLESGraphicsPipelineLayout ||
            !ReferenceEquals(description.Layout.Device, device))
            throw new GraphicsValidationException("The render pipeline layout must belong to this GLES device.");

        ValidateSupportedPipelineState(device, description);

        uint program = 0;
        uint vertexArray = 0;
        return device.WithContext(gl =>
        {
            try
            {
                program = LinkProgram(device, gl, description.Shaders);
                vertexArray = gl.GenVertexArray();
                if (vertexArray == 0)
                    throw new GraphicsException("OpenGL ES failed to create a vertex-array object.");
                OpenGLESGraphicsRenderPipeline pipeline = new(device, description, program, vertexArray);
                device.RegisterUnderContext(pipeline);
                program = 0;
                vertexArray = 0;
                return pipeline;
            }
            catch
            {
                if (vertexArray != 0)
                    gl.DeleteVertexArray(vertexArray);
                if (program != 0)
                    gl.DeleteProgram(program);
                throw;
            }
        });
    }

    private static void ValidateSupportedPipelineState(
        OpenGLESGraphicsDevice device,
        GraphicsRenderPipelineDescription description)
    {
        bool hasMeshShader = false;
        bool hasTaskShader = false;
        bool hasFragmentShader = false;
        ReadOnlySpan<IGraphicsShader> pipelineShaders = description.Shaders;
        for (int i = 0; i < pipelineShaders.Length; i++)
        {
            hasMeshShader |= pipelineShaders[i].Description.Stage == GraphicsShaderStage.Mesh;
            hasTaskShader |= pipelineShaders[i].Description.Stage == GraphicsShaderStage.Task;
            hasFragmentShader |= pipelineShaders[i].Description.Stage == GraphicsShaderStage.Fragment;
        }
        if (hasMeshShader && !device.Capabilities.Features.Supports(GraphicsFeature.MeshShaders))
            throw new GraphicsFeatureNotSupportedException(GraphicsFeature.MeshShaders, GraphicsFeature.MeshShaders);
        if (hasTaskShader && !device.Capabilities.Features.Supports(GraphicsFeature.TaskShaders))
            throw new GraphicsFeatureNotSupportedException(GraphicsFeature.TaskShaders, GraphicsFeature.TaskShaders);
        if (hasMeshShader && !hasFragmentShader)
            throw new GraphicsValidationException(
                "OpenGL ES EXT mesh/task pipelines require a fragment shader stage.");

        GraphicsRasterizationState raster = description.Rasterization;
        if (raster.PolygonMode != GraphicsPolygonMode.Fill)
            throw new GraphicsValidationException("OpenGL ES does not expose desktop polygon mode; only filled polygons are supported.");
        if (raster.DepthClampEnabled)
            throw new GraphicsFeatureNotSupportedException(GraphicsFeature.DepthClamp, GraphicsFeature.DepthClamp);
        if (raster.DepthBiasClamp != 0)
            throw new GraphicsValidationException("OpenGL ES core does not provide depth-bias clamping.");
        if (raster.LineWidth != 1)
            throw new GraphicsFeatureNotSupportedException(GraphicsFeature.WideLines, GraphicsFeature.WideLines);

        GraphicsDepthStencilState depth = description.DepthStencil;
        if (depth.DepthBoundsTestEnabled)
            throw new GraphicsFeatureNotSupportedException(
                GraphicsFeature.DepthBoundsTest, GraphicsFeature.DepthBoundsTest);

        GraphicsDeviceLimits limits = device.Capabilities.Limits;
        if ((limits.MaxColorAttachments != 0 && (uint)description.ColorFormats.Length > limits.MaxColorAttachments) ||
            (limits.MaxColorDrawBuffers != 0 && (uint)description.ColorFormats.Length > limits.MaxColorDrawBuffers))
            throw new GraphicsValidationException(
                $"The pipeline declares {description.ColorFormats.Length} color format(s), exceeding the GLES " +
                $"limits (attachments: {limits.MaxColorAttachments}, draw buffers: {limits.MaxColorDrawBuffers}).");

        if (description.Topology is GraphicsPrimitiveTopology.LineListAdjacency or
            GraphicsPrimitiveTopology.LineStripAdjacency or
            GraphicsPrimitiveTopology.TriangleListAdjacency or
            GraphicsPrimitiveTopology.TriangleStripAdjacency)
            throw new GraphicsValidationException("Adjacency primitive topologies are not available in OpenGL ES.");

        if (description.Topology == GraphicsPrimitiveTopology.PatchList)
        {
            if (!device.Version.IsAtLeast(3, 2))
                throw new GraphicsFeatureNotSupportedException(
                    GraphicsFeature.TessellationShaders, GraphicsFeature.TessellationShaders);
            if (description.Tessellation is not { } tessellation)
                throw new GraphicsValidationException("Patch topology requires a tessellation patch-control-point description.");
            if (tessellation.PatchControlPoints > int.MaxValue)
                throw new GraphicsValidationException("The patch-control-point count exceeds the native GLES integer range.");
            if (tessellation.PatchControlPoints > device.Capabilities.Limits.MaxTessellationPatchSize)
                throw new GraphicsValidationException(
                    $"Patch control point count {tessellation.PatchControlPoints} exceeds the GLES device limit {device.Capabilities.Limits.MaxTessellationPatchSize}.");
        }
        else if (description.Tessellation is not null)
        {
            throw new GraphicsValidationException("Tessellation state is only valid with PatchList topology.");
        }
    }

    internal void BindPipelineState(GL gl)
    {
        gl.UseProgram(Program);
        gl.BindVertexArray(VertexArray);
        if (Description.Tessellation is { } tessellation)
            gl.PatchParameter(
                PatchParameterName.Vertices,
                checked((int)tessellation.PatchControlPoints));

        GraphicsRasterizationState raster = Description.Rasterization;
        if (raster.CullMode == GraphicsCullMode.None)
        {
            gl.Disable(GLEnum.CullFace);
        }
        else
        {
            gl.Enable(GLEnum.CullFace);
            gl.CullFace(raster.CullMode switch
            {
                GraphicsCullMode.Front => GLEnum.Front,
                GraphicsCullMode.Back => GLEnum.Back,
                GraphicsCullMode.FrontAndBack => GLEnum.FrontAndBack,
                _ => GLEnum.Back
            });
        }
        gl.FrontFace(raster.FrontFace == GraphicsFrontFace.Clockwise
            ? FrontFaceDirection.CW
            : FrontFaceDirection.Ccw);

        SetEnabled(gl, GLEnum.PolygonOffsetFill, raster.DepthBiasEnabled);
        if (raster.DepthBiasEnabled)
            gl.PolygonOffset(raster.DepthBiasSlopeFactor, raster.DepthBiasConstantFactor);

        GraphicsDepthStencilState depth = Description.DepthStencil;
        SetEnabled(gl, GLEnum.DepthTest, depth.DepthTestEnabled);
        gl.DepthMask(depth.DepthWriteEnabled);
        gl.DepthFunc(GetCompareFunction(depth.DepthCompare));
        SetEnabled(gl, GLEnum.StencilTest, depth.StencilTestEnabled);
        if (depth.StencilTestEnabled)
        {
            ApplyStencilFace(gl, GLEnum.Front, depth.FrontStencil);
            ApplyStencilFace(gl, GLEnum.Back, depth.BackStencil);
        }

        GraphicsBlendState blend = Description.Blend;
        ReadOnlySpan<GraphicsBlendAttachmentState> blendStates = blend.Attachments;
        if (blendStates.Length > 1)
        {
            ExtDrawBuffersIndexed? indexedBlend = ((OpenGLESGraphicsDevice)Device).DrawBuffersIndexedApi;
            bool differs = false;
            for (int i = 1; i < blendStates.Length; i++)
                differs |= blendStates[i] != blendStates[0];

            if (differs && indexedBlend is null)
                throw new GraphicsFeatureNotSupportedException(
                    GraphicsFeature.IndependentBlend, GraphicsFeature.IndependentBlend);

            if (indexedBlend is not null && differs)
            {
                for (uint i = 0; i < (uint)blendStates.Length; i++)
                {
                    GraphicsBlendAttachmentState state = blendStates[(int)i];
                    if (state.BlendEnabled)
                        indexedBlend.Enable(EnableCap.Blend, i);
                    else
                        indexedBlend.Disable(EnableCap.Blend, i);

                    indexedBlend.BlendEquationSeparate(
                        i,
                        (BlendEquationModeEXT)GetBlendEquation(state.ColorOperation),
                        (BlendEquationModeEXT)GetBlendEquation(state.AlphaOperation));
                    indexedBlend.BlendFuncSeparate(
                        i,
                        (BlendingFactor)GetBlendFactor(state.SourceColor),
                        (BlendingFactor)GetBlendFactor(state.DestinationColor),
                        (BlendingFactor)GetBlendFactor(state.SourceAlpha),
                        (BlendingFactor)GetBlendFactor(state.DestinationAlpha));
                    indexedBlend.ColorMask(
                        i,
                        (state.WriteMask & GraphicsColorWriteMask.Red) != 0,
                        (state.WriteMask & GraphicsColorWriteMask.Green) != 0,
                        (state.WriteMask & GraphicsColorWriteMask.Blue) != 0,
                        (state.WriteMask & GraphicsColorWriteMask.Alpha) != 0);
                }
            }
            else
            {
                ApplyCommonBlendState(gl, blendStates[0]);
            }
        }
        else if (!blendStates.IsEmpty)
        {
            ApplyCommonBlendState(gl, blendStates[0]);
        }
        else
        {
            gl.Disable(GLEnum.Blend);
            gl.ColorMask(true, true, true, true);
        }
    }

    private static void ApplyCommonBlendState(GL gl, GraphicsBlendAttachmentState state)
    {
        SetEnabled(gl, GLEnum.Blend, state.BlendEnabled);
        gl.BlendEquationSeparate(GetBlendEquation(state.ColorOperation), GetBlendEquation(state.AlphaOperation));
        gl.BlendFuncSeparate(
            GetBlendFactor(state.SourceColor),
            GetBlendFactor(state.DestinationColor),
            GetBlendFactor(state.SourceAlpha),
            GetBlendFactor(state.DestinationAlpha));
        gl.ColorMask(
            (state.WriteMask & GraphicsColorWriteMask.Red) != 0,
            (state.WriteMask & GraphicsColorWriteMask.Green) != 0,
            (state.WriteMask & GraphicsColorWriteMask.Blue) != 0,
            (state.WriteMask & GraphicsColorWriteMask.Alpha) != 0);
    }

    internal void ConfigureVertexBinding(
        GL gl,
        uint binding,
        uint buffer,
        ulong offset,
        uint stride)
    {
        GraphicsVertexInputLayout? layout = Description.VertexInput;
        if (layout is null)
            throw new GraphicsValidationException("The pipeline has no vertex input layout.");

        gl.BindVertexArray(VertexArray);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, buffer);

        ReadOnlySpan<GraphicsVertexAttribute> attributes = layout.Attributes;
        for (int i = 0; i < attributes.Length; i++)
        {
            GraphicsVertexAttribute attribute = attributes[i];
            if (attribute.Binding != binding)
                continue;

            if (!TryGetVertexFormat(attribute.Format, out int componentCount, out GLEnum nativeType, out bool integer))
                throw new GraphicsValidationException($"Vertex format {attribute.Format} is not supported by GLES.");

            nint attributeOffset = checked((nint)(offset + attribute.Offset));
            if (integer)
                gl.VertexAttribIPointer(attribute.Location, componentCount, nativeType, stride, (void*)attributeOffset);
            else
                gl.VertexAttribPointer(attribute.Location, componentCount, nativeType, attribute.Format is
                    GraphicsFormat.R8UNorm or GraphicsFormat.R8SNorm or GraphicsFormat.RG8UNorm or
                    GraphicsFormat.RG8SNorm or GraphicsFormat.RGBA8UNorm or GraphicsFormat.RGBA8UNormSrgb or
                    GraphicsFormat.RGBA8SNorm, stride, (void*)attributeOffset);

            gl.EnableVertexAttribArray(attribute.Location);
            GraphicsVertexBufferLayout bufferLayout = FindBufferLayout(layout, binding);
            gl.VertexAttribDivisor(attribute.Location,
                bufferLayout.InputRate == GraphicsVertexInputRate.PerInstance ? 1u : 0u);
        }

        gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
    }

    protected override void ReleaseAdditionalNative(GL gl)
    {
        uint vao = Interlocked.Exchange(ref _vertexArray, 0);
        if (vao != 0)
            gl.DeleteVertexArray(vao);
    }

    private static GraphicsVertexBufferLayout FindBufferLayout(GraphicsVertexInputLayout layout, uint binding)
    {
        ReadOnlySpan<GraphicsVertexBufferLayout> buffers = layout.Buffers;
        for (int i = 0; i < buffers.Length; i++)
            if (buffers[i].Binding == binding)
                return buffers[i];
        throw new GraphicsValidationException($"Vertex binding {binding} is not declared.");
    }

    private static bool TryGetVertexFormat(
        GraphicsFormat format,
        out int components,
        out GLEnum type,
        out bool integer)
    {
        (components, type, integer) = format switch
        {
            GraphicsFormat.R8UNorm => (1, GLEnum.UnsignedByte, false),
            GraphicsFormat.R8SNorm => (1, GLEnum.Byte, false),
            GraphicsFormat.R8UInt => (1, GLEnum.UnsignedByte, true),
            GraphicsFormat.R8SInt => (1, GLEnum.Byte, true),
            GraphicsFormat.RG8UNorm => (2, GLEnum.UnsignedByte, false),
            GraphicsFormat.RG8SNorm => (2, GLEnum.Byte, false),
            GraphicsFormat.RG8UInt => (2, GLEnum.UnsignedByte, true),
            GraphicsFormat.RG8SInt => (2, GLEnum.Byte, true),
            GraphicsFormat.RGBA8UNorm or GraphicsFormat.RGBA8UNormSrgb => (4, GLEnum.UnsignedByte, false),
            GraphicsFormat.RGBA8SNorm => (4, GLEnum.Byte, false),
            GraphicsFormat.RGBA8UInt => (4, GLEnum.UnsignedByte, true),
            GraphicsFormat.RGBA8SInt => (4, GLEnum.Byte, true),
            GraphicsFormat.R16Float => (1, GLEnum.HalfFloat, false),
            GraphicsFormat.RG16Float => (2, GLEnum.HalfFloat, false),
            GraphicsFormat.RGBA16Float => (4, GLEnum.HalfFloat, false),
            GraphicsFormat.R32Float => (1, GLEnum.Float, false),
            GraphicsFormat.RG32Float => (2, GLEnum.Float, false),
            GraphicsFormat.RGB32Float => (3, GLEnum.Float, false),
            GraphicsFormat.RGBA32Float => (4, GLEnum.Float, false),
            _ => (0, GLEnum.Float, false)
        };
        return components != 0;
    }

    private static GLEnum GetCompareFunction(GraphicsCompareOperation operation) => operation switch
    {
        GraphicsCompareOperation.Never => GLEnum.Never,
        GraphicsCompareOperation.Less => GLEnum.Less,
        GraphicsCompareOperation.Equal => GLEnum.Equal,
        GraphicsCompareOperation.LessEqual => GLEnum.Lequal,
        GraphicsCompareOperation.Greater => GLEnum.Greater,
        GraphicsCompareOperation.NotEqual => GLEnum.Notequal,
        GraphicsCompareOperation.GreaterEqual => GLEnum.Gequal,
        GraphicsCompareOperation.Always => GLEnum.Always,
        _ => throw new ArgumentOutOfRangeException(nameof(operation))
    };

    private static GLEnum GetBlendFactor(GraphicsBlendFactor factor) => factor switch
    {
        GraphicsBlendFactor.Zero => GLEnum.Zero,
        GraphicsBlendFactor.One => GLEnum.One,
        GraphicsBlendFactor.SourceColor => GLEnum.SrcColor,
        GraphicsBlendFactor.OneMinusSourceColor => GLEnum.OneMinusSrcColor,
        GraphicsBlendFactor.DestinationColor => GLEnum.DstColor,
        GraphicsBlendFactor.OneMinusDestinationColor => GLEnum.OneMinusDstColor,
        GraphicsBlendFactor.SourceAlpha => GLEnum.SrcAlpha,
        GraphicsBlendFactor.OneMinusSourceAlpha => GLEnum.OneMinusSrcAlpha,
        GraphicsBlendFactor.DestinationAlpha => GLEnum.DstAlpha,
        GraphicsBlendFactor.OneMinusDestinationAlpha => GLEnum.OneMinusDstAlpha,
        GraphicsBlendFactor.ConstantColor => GLEnum.ConstantColor,
        GraphicsBlendFactor.OneMinusConstantColor => GLEnum.OneMinusConstantColor,
        GraphicsBlendFactor.ConstantAlpha => GLEnum.ConstantAlpha,
        GraphicsBlendFactor.OneMinusConstantAlpha => GLEnum.OneMinusConstantAlpha,
        GraphicsBlendFactor.SourceAlphaSaturate => GLEnum.SrcAlphaSaturate,
        _ => throw new ArgumentOutOfRangeException(nameof(factor))
    };

    private static GLEnum GetBlendEquation(GraphicsBlendOperation operation) => operation switch
    {
        GraphicsBlendOperation.Add => GLEnum.FuncAdd,
        GraphicsBlendOperation.Subtract => GLEnum.FuncSubtract,
        GraphicsBlendOperation.ReverseSubtract => GLEnum.FuncReverseSubtract,
        GraphicsBlendOperation.Minimum => GLEnum.Min,
        GraphicsBlendOperation.Maximum => GLEnum.Max,
        _ => throw new ArgumentOutOfRangeException(nameof(operation))
    };

    private static void ApplyStencilFace(GL gl, GLEnum face, GraphicsStencilFaceState state)
    {
        gl.StencilFuncSeparate(face, GetCompareFunction(state.Compare), (int)state.Reference, state.CompareMask);
        gl.StencilOpSeparate(face, GetStencilOperation(state.Fail), GetStencilOperation(state.DepthFail), GetStencilOperation(state.Pass));
        gl.StencilMaskSeparate(face, state.WriteMask);
    }

    private static GLEnum GetStencilOperation(GraphicsStencilOperation operation) => operation switch
    {
        GraphicsStencilOperation.Keep => GLEnum.Keep,
        GraphicsStencilOperation.Zero => GLEnum.Zero,
        GraphicsStencilOperation.Replace => GLEnum.Replace,
        GraphicsStencilOperation.IncrementClamp => GLEnum.Incr,
        GraphicsStencilOperation.DecrementClamp => GLEnum.Decr,
        GraphicsStencilOperation.Invert => GLEnum.Invert,
        GraphicsStencilOperation.IncrementWrap => GLEnum.IncrWrap,
        GraphicsStencilOperation.DecrementWrap => GLEnum.DecrWrap,
        _ => throw new ArgumentOutOfRangeException(nameof(operation))
    };

    private static void SetEnabled(GL gl, GLEnum capability, bool enabled)
    {
        if (enabled) gl.Enable(capability);
        else gl.Disable(capability);
    }
}

internal sealed class OpenGLESGraphicsComputePipeline : OpenGLESGraphicsPipeline, IGraphicsComputePipeline
{
    private OpenGLESGraphicsComputePipeline(
        OpenGLESGraphicsDevice device,
        GraphicsComputePipelineDescription description,
        uint program) : base(device, description.Layout, program) =>
        Description = description;

    public GraphicsComputePipelineDescription Description { get; }

    internal static OpenGLESGraphicsComputePipeline Create(
        OpenGLESGraphicsDevice device,
        GraphicsComputePipelineDescription description)
    {
        ArgumentNullException.ThrowIfNull(description);
        description.Validate();
        if (description.Layout is not OpenGLESGraphicsPipelineLayout ||
            !ReferenceEquals(description.Layout.Device, device))
            throw new GraphicsValidationException("The compute pipeline layout must belong to this GLES device.");

        return device.WithContext(gl =>
        {
            uint program = LinkProgram(device, gl, [description.ComputeShader]);
            try
            {
                Span<int> localWorkGroupSize = stackalloc int[3];
                gl.GetProgram(program, GLEnum.ComputeWorkGroupSize, localWorkGroupSize);
                OpenGLESComputeWorkGroupValidation.ValidateLocalSize(
                    localWorkGroupSize[0],
                    localWorkGroupSize[1],
                    localWorkGroupSize[2],
                    device.Capabilities.Limits);

                OpenGLESGraphicsComputePipeline pipeline = new(device, description, program);
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

    internal void BindPipelineState(GL gl) => gl.UseProgram(Program);
}
