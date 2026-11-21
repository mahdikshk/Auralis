using Auralis.Graphics.Abstractions;
using Auralis.Graphics.Pipelines;
using Auralis.Graphics.Resources;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ARB;
using Silk.NET.OpenGL.Legacy.Extensions.EXT;

namespace Auralis.Graphics.Backends.OpenGL;

/// <summary>
/// Conventional graphics pipeline backed by one linked OpenGL program and one DSA vertex array.
/// Descriptor layouts are retained for later binding validation; descriptor binding is implemented
/// independently from the program object and uses the common pipeline-layout contract.
/// </summary>
internal sealed class OpenGLGraphicsRenderPipeline : OpenGLNativeDeviceObject, IGraphicsRenderPipeline
{
    private readonly OpenGLGraphicsDevice _device;
    private readonly bool _isMeshPipeline;
    private readonly bool _isTaskPipeline;
    private uint _program;
    private uint _vertexArray;

    private OpenGLGraphicsRenderPipeline(
        OpenGLGraphicsDevice device,
        GraphicsRenderPipelineDescription description,
        uint program,
        uint vertexArray,
        OpenGLMeshShaderDialect meshShaderDialect) : base(device)
    {
        _device = device;
        Description = description;
        Layout = description.Layout;
        _program = program;
        _vertexArray = vertexArray;
        MeshShaderDialect = meshShaderDialect;
        foreach (IGraphicsShader shader in description.Shaders)
        {
            if (shader.Description.Stage == GraphicsShaderStage.Mesh)
                _isMeshPipeline = true;
            else if (shader.Description.Stage == GraphicsShaderStage.Task)
                _isTaskPipeline = true;
        }
    }

    public IGraphicsPipelineLayout Layout { get; }
    public GraphicsRenderPipelineDescription Description { get; }
    public string? DebugName => null;

    internal uint Program
    {
        get
        {
            uint handle = Volatile.Read(ref _program);
            return handle != 0 ? handle : throw new ObjectDisposedException(nameof(OpenGLGraphicsRenderPipeline));
        }
    }

    internal uint VertexArray
    {
        get
        {
            uint handle = Volatile.Read(ref _vertexArray);
            return handle != 0 ? handle : throw new ObjectDisposedException(nameof(OpenGLGraphicsRenderPipeline));
        }
    }

    internal PrimitiveType Topology => OpenGLPipelineMapping.ToPrimitiveType(Description.Topology);
    internal bool IsMeshPipeline => _isMeshPipeline;
    internal bool IsTaskPipeline => _isTaskPipeline;
    internal OpenGLMeshShaderDialect MeshShaderDialect { get; }

    internal static OpenGLGraphicsRenderPipeline Create(
        OpenGLGraphicsDevice device,
        GraphicsRenderPipelineDescription description)
    {
        ArgumentNullException.ThrowIfNull(description);
        description.Validate();

        if (description.Layout is not OpenGLGraphicsPipelineLayout layout ||
            !ReferenceEquals(layout.Device, device))
            throw new GraphicsValidationException("The pipeline layout must be created by this OpenGL device.");

        OpenGLMeshShaderDialect meshShaderDialect = OpenGLMeshShaderDialect.None;
        for (int i = 0; i < description.Shaders.Length; i++)
        {
            IGraphicsShader shader = description.Shaders[i];
            GraphicsShaderStage stage = shader.Description.Stage;
            if (shader is not OpenGLGraphicsShader openGlShader)
                throw new GraphicsValidationException("All pipeline shaders must belong to this OpenGL backend.");

            if (stage == GraphicsShaderStage.Mesh &&
                !device.Capabilities.Features.Supports(GraphicsFeature.MeshShaders))
                throw new GraphicsFeatureNotSupportedException(
                    GraphicsFeature.MeshShaders,
                    GraphicsFeature.MeshShaders);
            if (stage == GraphicsShaderStage.Task &&
                !device.Capabilities.Features.Supports(GraphicsFeature.TaskShaders))
                throw new GraphicsFeatureNotSupportedException(
                    GraphicsFeature.TaskShaders,
                    GraphicsFeature.TaskShaders);

            if (stage is GraphicsShaderStage.Mesh or GraphicsShaderStage.Task)
            {
                if (openGlShader.MeshShaderDialect == OpenGLMeshShaderDialect.None)
                    throw new GraphicsValidationException(
                        "Mesh/task shaders must declare a validated EXT or NV mesh-shader dialect.");
                if (meshShaderDialect != OpenGLMeshShaderDialect.None &&
                    meshShaderDialect != openGlShader.MeshShaderDialect)
                {
                    throw new GraphicsValidationException(
                        "A mesh/task pipeline cannot mix GL_EXT_mesh_shader and GL_NV_mesh_shader stages.");
                }

                meshShaderDialect = openGlShader.MeshShaderDialect;
            }
        }

        OpenGLPipelineFeatureValidation.ValidateDepthBiasClamp(
            description.Rasterization.DepthBiasClamp,
            device.PolygonOffsetClampApi is not null);
        OpenGLPipelineFeatureValidation.ValidateDepthBounds(
            description.DepthStencil.DepthBoundsTestEnabled,
            device.DepthBoundsApi is not null);
        if (!device.SupportedLineWidths.Contains(description.Rasterization.LineWidth))
            throw new GraphicsValidationException(
                $"Requested line width {description.Rasterization.LineWidth} is outside the driver-reported range " +
                $"[{device.SupportedLineWidths.Minimum}, {device.SupportedLineWidths.Maximum}].");
        OpenGLBlendStateValidation.Validate(
            description.Blend.Attachments,
            description.ColorFormats.Length,
            device.IndexedDrawBuffersApi is not null);
        ReadOnlySpan<GraphicsFormat> colorFormats = description.ColorFormats;
        ReadOnlySpan<GraphicsBlendAttachmentState> blendAttachments = description.Blend.Attachments;
        for (int i = 0; i < colorFormats.Length; i++)
        {
            GraphicsBlendAttachmentState blend = blendAttachments.IsEmpty
                ? default
                : blendAttachments[i];
            if (blend.BlendEnabled &&
                !device.Capabilities.SupportsFormat(colorFormats[i], GraphicsFormatSupport.Blend))
            {
                throw new GraphicsValidationException(
                    $"Color attachment format {colorFormats[i]} does not support blending on this OpenGL context.");
            }
        }

        if (description.VertexInput is { } vertexInput)
        {
            device.Capabilities.ValidateVertexInputLayout(vertexInput);

            uint maxBindings = device.Capabilities.Limits.MaxVertexInputBindings;
            ReadOnlySpan<GraphicsVertexBufferLayout> buffers = vertexInput.Buffers;
            for (int i = 0; i < buffers.Length; i++)
            {
                if (maxBindings != 0 && buffers[i].Binding >= maxBindings)
                    throw new GraphicsValidationException(
                        $"Vertex binding index {buffers[i].Binding} exceeds the OpenGL limit {maxBindings}.");
            }

            uint maxAttributes = device.Capabilities.Limits.MaxVertexInputAttributes;
            ReadOnlySpan<GraphicsVertexAttribute> attributes = vertexInput.Attributes;
            for (int i = 0; i < attributes.Length; i++)
            {
                GraphicsVertexAttribute attribute = attributes[i];
                if (maxAttributes != 0 && attribute.Location >= maxAttributes)
                    throw new GraphicsValidationException(
                        $"Vertex attribute location {attribute.Location} exceeds the OpenGL limit {maxAttributes}.");

                GraphicsVertexBufferLayout buffer = default;
                bool found = false;
                for (int bindingIndex = 0; bindingIndex < buffers.Length; bindingIndex++)
                {
                    if (buffers[bindingIndex].Binding != attribute.Binding)
                        continue;
                    buffer = buffers[bindingIndex];
                    found = true;
                    break;
                }

                if (!found)
                    throw new GraphicsValidationException($"Vertex attribute {attribute.Location} references a missing binding.");
                uint byteSize = OpenGLPipelineMapping.GetVertexFormatByteSize(attribute.Format);
                if (byteSize == 0 || attribute.Offset > buffer.Stride || byteSize > buffer.Stride - attribute.Offset)
                    throw new GraphicsValidationException(
                        $"Vertex attribute {attribute.Location} extends beyond stride {buffer.Stride}.");
            }
        }

        return device.WithContext(gl =>
        {
            uint program = OpenGLProgramLinker.Link(gl, description.Shaders);
            uint vertexArray = 0;
            try
            {
                device.DirectStateAccess.CreateVertexArrays(1, out vertexArray);
                if (vertexArray == 0)
                    throw new GraphicsException("OpenGL failed to create a vertex-array object.");

                ConfigureVertexArray(device.DirectStateAccess, vertexArray, description.VertexInput);
                device.LabelObject(ObjectIdentifier.Program, program, "Auralis Graphics Pipeline Program");
                device.LabelObject(ObjectIdentifier.VertexArray, vertexArray, "Auralis Graphics Pipeline Vertex Array");
                OpenGLGraphicsRenderPipeline pipeline = new(device, description, program, vertexArray, meshShaderDialect);
                device.RegisterUnderContext(pipeline);
                return pipeline;
            }
            catch
            {
                if (vertexArray != 0)
                    gl.DeleteVertexArray(vertexArray);
                gl.DeleteProgram(program);
                throw;
            }
        });
    }

    private static void ConfigureVertexArray(
        ArbDirectStateAccess dsa,
        uint vertexArray,
        GraphicsVertexInputLayout? input)
    {
        if (input is null)
            return;

        ReadOnlySpan<GraphicsVertexBufferLayout> buffers = input.Buffers;
        for (int i = 0; i < buffers.Length; i++)
        {
            GraphicsVertexBufferLayout buffer = buffers[i];
            dsa.VertexArrayBindingDivisor(
                vertexArray,
                buffer.Binding,
                buffer.InputRate == GraphicsVertexInputRate.PerInstance ? 1u : 0u);
        }

        ReadOnlySpan<GraphicsVertexAttribute> attributes = input.Attributes;
        for (int i = 0; i < attributes.Length; i++)
        {
            GraphicsVertexAttribute attribute = attributes[i];
            if (!OpenGLPipelineMapping.TryGetVertexFormat(attribute.Format, out OpenGLVertexFormat format))
                throw new GraphicsValidationException(
                    $"OpenGL does not support {attribute.Format} as a vertex attribute.");

            dsa.EnableVertexArrayAttrib(vertexArray, attribute.Location);
            dsa.VertexArrayAttribBinding(vertexArray, attribute.Location, attribute.Binding);
            if (format.IsInteger)
            {
                dsa.VertexArrayAttribIFormat(
                    vertexArray, attribute.Location, format.ComponentCount,
                    format.IntegerType, attribute.Offset);
            }
            else
            {
                dsa.VertexArrayAttribFormat(
                    vertexArray, attribute.Location, format.ComponentCount,
                    format.Type, format.Normalized, attribute.Offset);
            }
        }
    }

    internal void Apply(
        GL gl,
        ArbDrawBuffersBlend drawBuffersBlend,
        ExtDepthBoundsTest? depthBoundsApi,
        ArbPolygonOffsetClamp? polygonOffsetClampApi)
    {
        ThrowIfDisposed();
        gl.UseProgram(_program);
        gl.BindVertexArray(_vertexArray);

        GraphicsRasterizationState rasterization = Description.Rasterization;
        gl.PolygonMode(TriangleFace.FrontAndBack, rasterization.PolygonMode switch
        {
            GraphicsPolygonMode.Fill => PolygonMode.Fill,
            GraphicsPolygonMode.Line => PolygonMode.Line,
            GraphicsPolygonMode.Point => PolygonMode.Point,
            _ => throw new ArgumentOutOfRangeException()
        });

        if (rasterization.CullMode == GraphicsCullMode.None)
        {
            gl.Disable(EnableCap.CullFace);
        }
        else
        {
            gl.Enable(EnableCap.CullFace);
            gl.CullFace(rasterization.CullMode switch
            {
                GraphicsCullMode.Front => TriangleFace.Front,
                GraphicsCullMode.Back => TriangleFace.Back,
                GraphicsCullMode.FrontAndBack => TriangleFace.FrontAndBack,
                _ => throw new ArgumentOutOfRangeException()
            });
        }

        gl.FrontFace(rasterization.FrontFace == GraphicsFrontFace.Clockwise
            ? FrontFaceDirection.CW
            : FrontFaceDirection.Ccw);

        SetEnabled(gl, EnableCap.DepthClamp, rasterization.DepthClampEnabled);
        SetEnabled(
            gl,
            EnableCap.PolygonOffsetFill,
            rasterization.DepthBiasEnabled && rasterization.PolygonMode == GraphicsPolygonMode.Fill);
        SetEnabled(
            gl,
            EnableCap.PolygonOffsetLine,
            rasterization.DepthBiasEnabled && rasterization.PolygonMode == GraphicsPolygonMode.Line);
        SetEnabled(
            gl,
            EnableCap.PolygonOffsetPoint,
            rasterization.DepthBiasEnabled && rasterization.PolygonMode == GraphicsPolygonMode.Point);
        if (rasterization.DepthBiasEnabled)
        {
            if (rasterization.DepthBiasClamp != 0 && polygonOffsetClampApi is not null)
            {
                polygonOffsetClampApi.PolygonOffsetClamp(
                    rasterization.DepthBiasSlopeFactor,
                    rasterization.DepthBiasConstantFactor,
                    rasterization.DepthBiasClamp);
            }
            else
            {
                gl.PolygonOffset(rasterization.DepthBiasSlopeFactor, rasterization.DepthBiasConstantFactor);
            }
        }
        gl.LineWidth(rasterization.LineWidth);

        ApplyDepthStencil(gl, Description.DepthStencil, depthBoundsApi);
        ApplyBlend(
            gl, drawBuffersBlend, _device.IndexedDrawBuffersApi,
            Description.Blend, Description.ColorFormats.Length);

        if (Description.Tessellation is { } tessellation)
            gl.PatchParameter(PatchParameterName.Vertices, checked((int)tessellation.PatchControlPoints));
    }

    private static void ApplyDepthStencil(
        GL gl,
        GraphicsDepthStencilState state,
        ExtDepthBoundsTest? depthBoundsApi)
    {
        SetEnabled(gl, EnableCap.DepthTest, state.DepthTestEnabled);
        if (depthBoundsApi is not null)
        {
            SetEnabled(gl, (EnableCap)EXT.DepthBoundsTestExt, state.DepthBoundsTestEnabled);
            if (state.DepthBoundsTestEnabled)
                depthBoundsApi.DepthBounds(state.MinimumDepthBounds, state.MaximumDepthBounds);
        }
        gl.DepthMask(state.DepthWriteEnabled);
        gl.DepthFunc(OpenGLPipelineMapping.ToDepthFunction(state.DepthCompare));

        SetEnabled(gl, EnableCap.StencilTest, state.StencilTestEnabled);
        if (state.StencilTestEnabled)
        {
            ApplyStencilFace(gl, TriangleFace.Front, state.FrontStencil);
            ApplyStencilFace(gl, TriangleFace.Back, state.BackStencil);
        }
    }

    private static void ApplyStencilFace(GL gl, TriangleFace face, GraphicsStencilFaceState state)
    {
        gl.StencilOpSeparate(
            face,
            OpenGLPipelineMapping.ToStencilOperation(state.Fail),
            OpenGLPipelineMapping.ToStencilOperation(state.DepthFail),
            OpenGLPipelineMapping.ToStencilOperation(state.Pass));
        gl.StencilFuncSeparate(
            (GLEnum)face,
            (GLEnum)OpenGLPipelineMapping.ToDepthFunction(state.Compare),
            unchecked((int)state.Reference),
            state.CompareMask);
        gl.StencilMaskSeparate(face, state.WriteMask);
    }

    private static void ApplyBlend(
        GL gl,
        ArbDrawBuffersBlend drawBuffersBlend,
        ExtDrawBuffers2? indexedDrawBuffers,
        GraphicsBlendState blend,
        int colorTargetCount)
    {
        if (colorTargetCount == 0)
            return;

        ReadOnlySpan<GraphicsBlendAttachmentState> attachments = blend.Attachments;
        GraphicsBlendAttachmentState first = attachments.IsEmpty
            ? new GraphicsBlendAttachmentState(
                false,
                GraphicsBlendFactor.One,
                GraphicsBlendFactor.Zero,
                GraphicsBlendOperation.Add,
                GraphicsBlendFactor.One,
                GraphicsBlendFactor.Zero,
                GraphicsBlendOperation.Add,
                GraphicsColorWriteMask.All)
            : attachments[0];

        // Per-attachment blend factors/equations are core on the required GL 4.5 baseline.
        // Independent enable and write masks use Silk.NET's official EXT_draw_buffers2 wrapper.
        if (indexedDrawBuffers is null)
        {
            SetEnabled(gl, EnableCap.Blend, first.BlendEnabled);
            GraphicsColorWriteMask commonMask = first.WriteMask;
            gl.ColorMask(
                (commonMask & GraphicsColorWriteMask.Red) != 0,
                (commonMask & GraphicsColorWriteMask.Green) != 0,
                (commonMask & GraphicsColorWriteMask.Blue) != 0,
                (commonMask & GraphicsColorWriteMask.Alpha) != 0);
        }

        for (int i = 0; i < colorTargetCount; i++)
        {
            GraphicsBlendAttachmentState state = attachments.IsEmpty ? first : attachments[i];
            if (indexedDrawBuffers is not null)
            {
                if (state.BlendEnabled)
                    indexedDrawBuffers.EnableIndexed(Silk.NET.OpenGL.Legacy.EnableCap.Blend, (uint)i);
                else
                    indexedDrawBuffers.DisableIndexed(Silk.NET.OpenGL.Legacy.EnableCap.Blend, (uint)i);

                GraphicsColorWriteMask mask = state.WriteMask;
                indexedDrawBuffers.ColorMaskIndexed(
                    (uint)i,
                    (mask & GraphicsColorWriteMask.Red) != 0,
                    (mask & GraphicsColorWriteMask.Green) != 0,
                    (mask & GraphicsColorWriteMask.Blue) != 0,
                    (mask & GraphicsColorWriteMask.Alpha) != 0);
            }

            drawBuffersBlend.BlendFuncSeparate(
                (uint)i,
                OpenGLPipelineMapping.ToBlendFactor(state.SourceColor),
                OpenGLPipelineMapping.ToBlendFactor(state.DestinationColor),
                OpenGLPipelineMapping.ToBlendFactor(state.SourceAlpha),
                OpenGLPipelineMapping.ToBlendFactor(state.DestinationAlpha));
            drawBuffersBlend.BlendEquationSeparate(
                (uint)i,
                OpenGLPipelineMapping.ToBlendEquation(state.ColorOperation),
                OpenGLPipelineMapping.ToBlendEquation(state.AlphaOperation));
        }
    }

    private static void SetEnabled(GL gl, EnableCap cap, bool enabled)
    {
        if (enabled)
            gl.Enable(cap);
        else
            gl.Disable(cap);
    }

    protected override void DisposeCore() => ReleaseOwnerReference();

    public override void ReleaseNative(GL gl)
    {
        uint vertexArray = Interlocked.Exchange(ref _vertexArray, 0);
        uint program = Interlocked.Exchange(ref _program, 0);
        if (vertexArray != 0)
            gl.DeleteVertexArray(vertexArray);
        if (program != 0)
            gl.DeleteProgram(program);
    }
}
