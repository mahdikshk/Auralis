using System.Diagnostics;
using System.Text;
using System.Runtime.InteropServices;
using System.Runtime.ExceptionServices;
using Auralis.Graphics.Abstractions;
using Auralis.Graphics.Commands;
using Auralis.Graphics.Devices;
using Auralis.Graphics.Pipelines;
using Auralis.Graphics.Presentation;
using Auralis.Graphics.Resources;
using Auralis.Graphics.Synchronization;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ARB;
using Silk.NET.OpenGL.Extensions.NV;
using Silk.NET.OpenGL.Legacy.Extensions.EXT;
using OpenGLExtMeshShader = Silk.NET.OpenGL.Extensions.EXT.ExtMeshShader;

namespace Auralis.Graphics.Backends.OpenGL;

/// <summary>
/// Initial desktop OpenGL device implementation. Native work is serialized because one current
/// context cannot be concurrently driven by unrelated threads.
/// </summary>
internal sealed class OpenGLGraphicsDevice : IGraphicsDevice
{
    private readonly IOpenGLContextProvider _contextProvider;
    private readonly object _contextGate = new();
    private readonly HashSet<IOpenGLNativeResource> _resources = new(ReferenceEqualityComparer.Instance);
    private readonly object _queueGate = new();
    private readonly OpenGLGraphicsCommandQueue?[] _queues = new OpenGLGraphicsCommandQueue?[3];
    private int _pendingSubmissionCount;
    private readonly GL _gl;
    private readonly ArbDirectStateAccess _directStateAccess;
    private readonly ArbTextureView _textureViewApi;
    private readonly ArbSamplerObjects _samplerObjects;
    private readonly ArbInternalformatQuery _internalFormatQuery;
    private readonly ArbSync _syncApi;
    private readonly ArbCopyImage _copyImageApi;
    private readonly ArbViewportArray _viewportApi;
    private readonly ArbBaseInstance _baseInstanceApi;
    private readonly ArbDrawIndirect _drawIndirectApi;
    private readonly ArbMultiDrawIndirect _multiDrawIndirectApi;
    private readonly ArbShaderImageLoadStore _imageLoadStoreApi;
    private readonly ArbDrawBuffersBlend _drawBuffersBlendApi;
    private readonly ExtDrawBuffers2? _indexedDrawBuffersApi;
    private readonly ExtDepthBoundsTest? _depthBoundsApi;
    private readonly ArbPolygonOffsetClamp? _polygonOffsetClampApi;
    private readonly OpenGLExtMeshShader? _meshShaderApi;
    private readonly NVMeshShader? _nvMeshShaderApi;
    private readonly ArbES2Compatibility _shaderBinaryApi;
    private readonly ArbGlSpirv? _spirvApi;
    private readonly ArbComputeShader _computeShaderApi;
    private readonly ArbUniformBufferObject _uniformBufferObjectApi;
    private readonly string _renderer;
    internal uint MaxComputeWorkGroupCountX { get; }
    internal uint MaxComputeWorkGroupCountY { get; }
    internal uint MaxComputeWorkGroupCountZ { get; }
    internal uint MaxMeshWorkGroupCountX { get; }
    internal uint MaxMeshWorkGroupCountY { get; }
    internal uint MaxMeshWorkGroupCountZ { get; }
    internal uint MaxMeshWorkGroupTotalCount { get; }
    internal uint MaxTaskWorkGroupCountX { get; }
    internal uint MaxTaskWorkGroupCountY { get; }
    internal uint MaxTaskWorkGroupCountZ { get; }
    internal uint MaxTaskWorkGroupTotalCount { get; }
    internal uint MaxNvDrawMeshTasksCount { get; }
    internal uint MaxUniformBufferBindings { get; }
    internal uint MaxStorageBufferBindings { get; }
    internal uint MaxTextureUnits { get; }
    internal uint MaxImageUnits { get; }
    internal uint UniformBufferOffsetAlignment { get; }
    internal uint StorageBufferOffsetAlignment { get; }
    internal OpenGLLineWidthRange SupportedLineWidths { get; }
    internal ExtDrawBuffers2? IndexedDrawBuffersApi => _indexedDrawBuffersApi;
    internal ExtDepthBoundsTest? DepthBoundsApi => _depthBoundsApi;
    internal ArbPolygonOffsetClamp? PolygonOffsetClampApi => _polygonOffsetClampApi;
    internal OpenGLExtMeshShader? MeshShaderApi => _meshShaderApi;
    internal NVMeshShader? NvMeshShaderApi => _nvMeshShaderApi;
    internal ArbES2Compatibility ShaderBinaryApi => _shaderBinaryApi;
    internal ArbGlSpirv? SpirVApi => _spirvApi;
    internal bool DebugLayerEnabled => _debugLayerEnabled;
    private int _disposeState;
    private DebugProc? _debugCallback;
    private GCHandle _debugUserData;
    private bool _debugLayerEnabled;

    private OpenGLGraphicsDevice(
        IOpenGLContextProvider contextProvider,
        GL gl,
        ArbDirectStateAccess directStateAccess,
        ArbTextureView textureViewApi,
        ArbSamplerObjects samplerObjects,
        ArbInternalformatQuery internalFormatQuery,
        ArbSync syncApi,
        ArbCopyImage copyImageApi,
        ArbViewportArray viewportApi,
        ArbBaseInstance baseInstanceApi,
        ArbDrawIndirect drawIndirectApi,
        ArbMultiDrawIndirect multiDrawIndirectApi,
        ArbShaderImageLoadStore imageLoadStoreApi,
        ArbDrawBuffersBlend drawBuffersBlendApi,
        ExtDrawBuffers2? indexedDrawBuffersApi,
        ExtDepthBoundsTest? depthBoundsApi,
        ArbPolygonOffsetClamp? polygonOffsetClampApi,
        OpenGLExtMeshShader? meshShaderApi,
        NVMeshShader? nvMeshShaderApi,
        ArbES2Compatibility shaderBinaryApi,
        ArbGlSpirv? spirvApi,
        OpenGLLineWidthRange supportedLineWidths,
        ArbComputeShader computeShaderApi,
        ArbUniformBufferObject uniformBufferObjectApi,
        uint maxComputeWorkGroupCountX,
        uint maxComputeWorkGroupCountY,
        uint maxComputeWorkGroupCountZ,
        uint maxMeshWorkGroupCountX,
        uint maxMeshWorkGroupCountY,
        uint maxMeshWorkGroupCountZ,
        uint maxMeshWorkGroupTotalCount,
        uint maxTaskWorkGroupCountX,
        uint maxTaskWorkGroupCountY,
        uint maxTaskWorkGroupCountZ,
        uint maxTaskWorkGroupTotalCount,
        uint maxNvDrawMeshTasksCount,
        uint maxUniformBufferBindings,
        uint maxStorageBufferBindings,
        uint maxTextureUnits,
        uint maxImageUnits,
        uint uniformBufferOffsetAlignment,
        uint storageBufferOffsetAlignment,
        int major,
        int minor,
        string vendor,
        string renderer,
        string version,
        GraphicsDeviceCapabilities capabilities)
    {
        _contextProvider = contextProvider;
        _gl = gl;
        _directStateAccess = directStateAccess;
        _textureViewApi = textureViewApi;
        _samplerObjects = samplerObjects;
        _internalFormatQuery = internalFormatQuery;
        _syncApi = syncApi;
        _copyImageApi = copyImageApi;
        _viewportApi = viewportApi;
        _baseInstanceApi = baseInstanceApi;
        _drawIndirectApi = drawIndirectApi;
        _multiDrawIndirectApi = multiDrawIndirectApi;
        _imageLoadStoreApi = imageLoadStoreApi;
        _drawBuffersBlendApi = drawBuffersBlendApi;
        _indexedDrawBuffersApi = indexedDrawBuffersApi;
        _depthBoundsApi = depthBoundsApi;
        _polygonOffsetClampApi = polygonOffsetClampApi;
        _meshShaderApi = meshShaderApi;
        _nvMeshShaderApi = nvMeshShaderApi;
        _shaderBinaryApi = shaderBinaryApi;
        _spirvApi = spirvApi;
        SupportedLineWidths = supportedLineWidths;
        _computeShaderApi = computeShaderApi;
        _uniformBufferObjectApi = uniformBufferObjectApi;
        _renderer = renderer;
        MaxComputeWorkGroupCountX = maxComputeWorkGroupCountX;
        MaxComputeWorkGroupCountY = maxComputeWorkGroupCountY;
        MaxComputeWorkGroupCountZ = maxComputeWorkGroupCountZ;
        MaxMeshWorkGroupCountX = maxMeshWorkGroupCountX;
        MaxMeshWorkGroupCountY = maxMeshWorkGroupCountY;
        MaxMeshWorkGroupCountZ = maxMeshWorkGroupCountZ;
        MaxMeshWorkGroupTotalCount = maxMeshWorkGroupTotalCount;
        MaxTaskWorkGroupCountX = maxTaskWorkGroupCountX;
        MaxTaskWorkGroupCountY = maxTaskWorkGroupCountY;
        MaxTaskWorkGroupCountZ = maxTaskWorkGroupCountZ;
        MaxTaskWorkGroupTotalCount = maxTaskWorkGroupTotalCount;
        MaxNvDrawMeshTasksCount = maxNvDrawMeshTasksCount;
        MaxUniformBufferBindings = maxUniformBufferBindings;
        MaxStorageBufferBindings = maxStorageBufferBindings;
        MaxTextureUnits = maxTextureUnits;
        MaxImageUnits = maxImageUnits;
        UniformBufferOffsetAlignment = Math.Max(1u, uniformBufferOffsetAlignment);
        StorageBufferOffsetAlignment = Math.Max(1u, storageBufferOffsetAlignment);
        Capabilities = capabilities;
        Backend = GraphicsBackend.OpenGL;
        Adapter = new GraphicsAdapterInfo(default, renderer, GraphicsAdapterType.Unknown, 0, 0, 0);
        Info = new GraphicsDeviceInfo(renderer, vendor, version, $"{major}.{minor} Core", 0, 0, false);
    }

    public GraphicsBackend Backend { get; }
    public GraphicsAdapterInfo Adapter { get; }
    public GraphicsDeviceInfo Info { get; }
    public GraphicsDeviceCapabilities Capabilities { get; }
    public bool IsDisposed => Volatile.Read(ref _disposeState) != 0;

    internal static OpenGLGraphicsDevice Create(
        IOpenGLContextProvider contextProvider,
        GraphicsDeviceOptions options,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        OpenGLDeviceOptionsValidation.Validate(options);
        GL? gl = null;
        ArbDirectStateAccess? directStateAccess = null;
        ArbTextureView? textureViewApi = null;
        ArbSamplerObjects? samplerObjects = null;
        ArbInternalformatQuery? internalFormatQuery = null;
        ArbSync? syncApi = null;
        ArbCopyImage? copyImageApi = null;
        ArbViewportArray? viewportApi = null;
        ArbBaseInstance? baseInstanceApi = null;
        ArbDrawIndirect? drawIndirectApi = null;
        ArbMultiDrawIndirect? multiDrawIndirectApi = null;
        ArbShaderImageLoadStore? imageLoadStoreApi = null;
        ArbDrawBuffersBlend? drawBuffersBlendApi = null;
        ExtDrawBuffers2? indexedDrawBuffersApi = null;
        ExtDepthBoundsTest? depthBoundsApi = null;
        ArbPolygonOffsetClamp? polygonOffsetClampApi = null;
        OpenGLExtMeshShader? meshShaderApi = null;
        NVMeshShader? nvMeshShaderApi = null;
        ArbES2Compatibility? shaderBinaryApi = null;
        ArbGlSpirv? spirvApi = null;
        ArbComputeShader? computeShaderApi = null;
        ArbUniformBufferObject? uniformBufferObjectApi = null;
        if (contextProvider.StateOwnership != OpenGLContextStateOwnership.Exclusive)
            throw new GraphicsBackendUnavailableException(
                GraphicsBackend.OpenGL,
                "The initial OpenGL backend requires exclusive ownership of context state; shared host-state preservation is not implemented.");

        using OpenGLContextLease contextLease = OpenGLContextLease.Acquire(contextProvider);
        try
        {
            gl = GL.GetApi(contextProvider.GetProcAddress);
            if (gl is null)
            {
                throw new GraphicsBackendUnavailableException(
                    GraphicsBackend.OpenGL,
                    "Failed to resolve OpenGL entry points from the provided context provider.");
            }

            int major = gl.GetInteger(GLEnum.MajorVersion);
            int minor = gl.GetInteger(GLEnum.MinorVersion);
            int profile = gl.GetInteger(GLEnum.ContextProfileMask);
            if (major < 4 || (major == 4 && minor < 5))
                throw new GraphicsBackendUnavailableException(
                    GraphicsBackend.OpenGL,
                    $"OpenGL 4.5 is required; the current context reports {major}.{minor}.");
            if ((profile & (int)ContextProfileMask.CoreProfileBit) == 0)
                throw new GraphicsBackendUnavailableException(
                    GraphicsBackend.OpenGL,
                    "A desktop OpenGL Core Profile context is required.");

            // These Silk.NET wrappers load entry points promoted into the required desktop 4.5
            // core version. Construct them only after validation, with this context current.
            directStateAccess = new ArbDirectStateAccess(gl.Context);
            textureViewApi = new ArbTextureView(gl.Context);
            samplerObjects = new ArbSamplerObjects(gl.Context);
            internalFormatQuery = new ArbInternalformatQuery(gl.Context);
            syncApi = new ArbSync(gl.Context);
            copyImageApi = new ArbCopyImage(gl.Context);
            viewportApi = new ArbViewportArray(gl.Context);
            baseInstanceApi = new ArbBaseInstance(gl.Context);
            drawIndirectApi = new ArbDrawIndirect(gl.Context);
            multiDrawIndirectApi = new ArbMultiDrawIndirect(gl.Context);
            imageLoadStoreApi = new ArbShaderImageLoadStore(gl.Context);
            drawBuffersBlendApi = new ArbDrawBuffersBlend(gl.Context);
            if (gl.IsExtensionPresent(ExtDrawBuffers2.ExtensionName))
                indexedDrawBuffersApi = new ExtDrawBuffers2(gl.Context);
            if (gl.IsExtensionPresent(ExtDepthBoundsTest.ExtensionName))
                depthBoundsApi = new ExtDepthBoundsTest(gl.Context);
            if (gl.IsExtensionPresent(ArbPolygonOffsetClamp.ExtensionName))
                polygonOffsetClampApi = new ArbPolygonOffsetClamp(gl.Context);
            if (gl.IsExtensionPresent(OpenGLExtMeshShader.ExtensionName) &&
                HasEntryPoints(
                    contextProvider,
                    "glDrawMeshTasksEXT",
                    "glDrawMeshTasksIndirectEXT",
                    "glMultiDrawMeshTasksIndirectEXT"))
            {
                meshShaderApi = new OpenGLExtMeshShader(gl.Context);
            }
            if (gl.IsExtensionPresent(NVMeshShader.ExtensionName) &&
                contextProvider.GetProcAddress("glDrawMeshTasksNV") != 0)
            {
                nvMeshShaderApi = new NVMeshShader(gl.Context);
            }
            shaderBinaryApi = new ArbES2Compatibility(gl.Context);
            if (gl.IsExtensionPresent(ArbGlSpirv.ExtensionName))
                spirvApi = new ArbGlSpirv(gl.Context);
            if (major > 4 || (major == 4 && minor >= 3))
                computeShaderApi = new ArbComputeShader(gl.Context);
            uniformBufferObjectApi = new ArbUniformBufferObject(gl.Context);

            string vendor = gl.GetStringS(StringName.Vendor);
            string renderer = gl.GetStringS(StringName.Renderer);
            string version = gl.GetStringS(StringName.Version);
            gl.GetFloat(GLEnum.AliasedLineWidthRange, out System.Numerics.Vector2 nativeLineWidths);
            OpenGLLineWidthRange supportedLineWidths = OpenGLLineWidthRange.FromNative(
                nativeLineWidths.X, nativeLineWidths.Y);
            uint maxUniformBufferBindings = ToUInt(gl.GetInteger(GLEnum.MaxUniformBufferBindings));
            uint maxStorageBufferBindings = ToUInt(gl.GetInteger(GLEnum.MaxShaderStorageBufferBindings));
            uint maxTextureUnits = ToUInt(gl.GetInteger(GLEnum.MaxCombinedTextureImageUnits));
            uint maxImageUnits = ToUInt(gl.GetInteger(GLEnum.MaxImageUnits));
            uint uniformBufferOffsetAlignment = ToUInt(gl.GetInteger(GLEnum.UniformBufferOffsetAlignment));
            uint storageBufferOffsetAlignment = ToUInt(gl.GetInteger(GLEnum.ShaderStorageBufferOffsetAlignment));
            uint maxComputeWorkGroupCountX = 0;
            uint maxComputeWorkGroupCountY = 0;
            uint maxComputeWorkGroupCountZ = 0;
            uint maxMeshWorkGroupCountX = 0;
            uint maxMeshWorkGroupCountY = 0;
            uint maxMeshWorkGroupCountZ = 0;
            uint maxMeshWorkGroupTotalCount = 0;
            uint maxTaskWorkGroupCountX = 0;
            uint maxTaskWorkGroupCountY = 0;
            uint maxTaskWorkGroupCountZ = 0;
            uint maxTaskWorkGroupTotalCount = 0;
            uint maxNvDrawMeshTasksCount = 0;
            if (computeShaderApi is not null)
            {
                uniformBufferObjectApi!.GetInteger(GetPName.MaxComputeWorkGroupCount, 0, out int countX);
                uniformBufferObjectApi.GetInteger(GetPName.MaxComputeWorkGroupCount, 1, out int countY);
                uniformBufferObjectApi.GetInteger(GetPName.MaxComputeWorkGroupCount, 2, out int countZ);
                maxComputeWorkGroupCountX = ToUInt(countX);
                maxComputeWorkGroupCountY = ToUInt(countY);
                maxComputeWorkGroupCountZ = ToUInt(countZ);
            }
            if (meshShaderApi is not null)
            {
                uniformBufferObjectApi!.GetInteger(GetPName.MaxMeshWorkGroupCountExt, 0, out int countX);
                uniformBufferObjectApi.GetInteger(GetPName.MaxMeshWorkGroupCountExt, 1, out int countY);
                uniformBufferObjectApi.GetInteger(GetPName.MaxMeshWorkGroupCountExt, 2, out int countZ);
                maxMeshWorkGroupCountX = ToUInt(countX);
                maxMeshWorkGroupCountY = ToUInt(countY);
                maxMeshWorkGroupCountZ = ToUInt(countZ);
                uniformBufferObjectApi.GetInteger(GetPName.MaxTaskWorkGroupCountExt, 0, out countX);
                uniformBufferObjectApi.GetInteger(GetPName.MaxTaskWorkGroupCountExt, 1, out countY);
                uniformBufferObjectApi.GetInteger(GetPName.MaxTaskWorkGroupCountExt, 2, out countZ);
                maxTaskWorkGroupCountX = ToUInt(countX);
                maxTaskWorkGroupCountY = ToUInt(countY);
                maxTaskWorkGroupCountZ = ToUInt(countZ);
                maxMeshWorkGroupTotalCount = ToUInt(gl.GetInteger(GetPName.MaxMeshWorkGroupTotalCountExt));
                maxTaskWorkGroupTotalCount = ToUInt(gl.GetInteger(GetPName.MaxTaskWorkGroupTotalCountExt));
            }
            if (nvMeshShaderApi is not null)
            {
                maxNvDrawMeshTasksCount = ToUInt(gl.GetInteger((GLEnum)0x953D)); // GL_MAX_DRAW_MESH_TASKS_COUNT_NV
                if (maxNvDrawMeshTasksCount == 0)
                    throw new GraphicsBackendUnavailableException(
                        GraphicsBackend.OpenGL,
                        "GL_NV_mesh_shader was advertised but GL_MAX_DRAW_MESH_TASKS_COUNT_NV is zero.");

                // When EXT is also available, keep its 3D limits as the default capability
                // snapshot. NV pipelines use the independently queried one-dimensional limit.
                if (meshShaderApi is null)
                {
                    maxMeshWorkGroupCountX = maxNvDrawMeshTasksCount;
                    maxMeshWorkGroupCountY = 1;
                    maxMeshWorkGroupCountZ = 1;
                    maxMeshWorkGroupTotalCount = maxNvDrawMeshTasksCount;
                    maxTaskWorkGroupCountX = maxNvDrawMeshTasksCount;
                    maxTaskWorkGroupCountY = 1;
                    maxTaskWorkGroupCountZ = 1;
                    maxTaskWorkGroupTotalCount = maxNvDrawMeshTasksCount;
                }
            }

            bool supportsMeshShaders = OpenGLMeshTaskValidation.IsSupported(
                meshShaderApi is not null || nvMeshShaderApi is not null,
                maxMeshWorkGroupCountX,
                maxMeshWorkGroupCountY,
                maxMeshWorkGroupCountZ,
                maxMeshWorkGroupTotalCount);
            bool supportsTaskShaders = supportsMeshShaders && OpenGLMeshTaskValidation.IsSupported(
                meshShaderApi is not null || nvMeshShaderApi is not null,
                maxTaskWorkGroupCountX,
                maxTaskWorkGroupCountY,
                maxTaskWorkGroupCountZ,
                maxTaskWorkGroupTotalCount);
            GraphicsDeviceCapabilities capabilities = QueryCapabilities(
                gl, major, minor, internalFormatQuery!, uniformBufferObjectApi!,
                contextProvider.SupportsPresentation,
                indexedDrawBuffersApi is not null,
                depthBoundsApi is not null,
                polygonOffsetClampApi is not null,
                spirvApi is not null,
                supportsMeshShaders,
                supportsTaskShaders,
                supportedLineWidths,
                maxMeshWorkGroupCountX,
                maxMeshWorkGroupCountY,
                maxMeshWorkGroupCountZ,
                maxMeshWorkGroupTotalCount,
                maxTaskWorkGroupCountX,
                maxTaskWorkGroupCountY,
                maxTaskWorkGroupCountZ,
                maxTaskWorkGroupTotalCount);

            cancellationToken.ThrowIfCancellationRequested();

            OpenGLGraphicsDevice device = new(
                contextProvider, gl, directStateAccess, textureViewApi, samplerObjects,
                internalFormatQuery, syncApi, copyImageApi, viewportApi,
                baseInstanceApi, drawIndirectApi, multiDrawIndirectApi, imageLoadStoreApi,
                drawBuffersBlendApi, indexedDrawBuffersApi, depthBoundsApi, polygonOffsetClampApi,
                meshShaderApi, nvMeshShaderApi, shaderBinaryApi!, spirvApi, supportedLineWidths, computeShaderApi!, uniformBufferObjectApi!,
                maxComputeWorkGroupCountX, maxComputeWorkGroupCountY, maxComputeWorkGroupCountZ,
                maxMeshWorkGroupCountX, maxMeshWorkGroupCountY, maxMeshWorkGroupCountZ,
                maxMeshWorkGroupTotalCount,
                maxTaskWorkGroupCountX, maxTaskWorkGroupCountY, maxTaskWorkGroupCountZ,
                maxTaskWorkGroupTotalCount,
                maxNvDrawMeshTasksCount,
                maxUniformBufferBindings, maxStorageBufferBindings, maxTextureUnits, maxImageUnits,
                uniformBufferOffsetAlignment, storageBufferOffsetAlignment,
                major, minor, vendor, renderer, version, capabilities);
            if ((options.DebugOptions & GraphicsDebugOptions.EnableDebugLayer) != 0)
                device.InitializeDebugOutput();
            gl = null;
            directStateAccess = null;
            textureViewApi = null;
            samplerObjects = null;
            internalFormatQuery = null;
            syncApi = null;
            copyImageApi = null;
            viewportApi = null;
            baseInstanceApi = null;
            drawIndirectApi = null;
            multiDrawIndirectApi = null;
            imageLoadStoreApi = null;
            drawBuffersBlendApi = null;
            indexedDrawBuffersApi = null;
            depthBoundsApi = null;
            polygonOffsetClampApi = null;
            meshShaderApi = null;
            nvMeshShaderApi = null;
            shaderBinaryApi = null;
            spirvApi = null;
            computeShaderApi = null;
            uniformBufferObjectApi = null;
            return device;
        }
        catch
        {
            uniformBufferObjectApi?.Dispose();
            computeShaderApi?.Dispose();
            spirvApi?.Dispose();
            shaderBinaryApi?.Dispose();
            polygonOffsetClampApi?.Dispose();
            nvMeshShaderApi?.Dispose();
            meshShaderApi?.Dispose();
            depthBoundsApi?.Dispose();
            indexedDrawBuffersApi?.Dispose();
            drawBuffersBlendApi?.Dispose();
            imageLoadStoreApi?.Dispose();
            multiDrawIndirectApi?.Dispose();
            drawIndirectApi?.Dispose();
            baseInstanceApi?.Dispose();
            viewportApi?.Dispose();
            copyImageApi?.Dispose();
            syncApi?.Dispose();
            internalFormatQuery?.Dispose();
            samplerObjects?.Dispose();
            textureViewApi?.Dispose();
            directStateAccess?.Dispose();
            gl?.Dispose();
            throw;
        }
    }

    private static GraphicsDeviceCapabilities QueryCapabilities(
        GL gl,
        int major,
        int minor,
        ArbInternalformatQuery internalFormatQuery,
        ArbUniformBufferObject uniformBufferObjectApi,
        bool supportsPresentation,
        bool supportsIndependentBlend,
        bool supportsDepthBounds,
        bool supportsDepthBiasClamp,
        bool supportsSpirV,
        bool supportsMeshShaders,
        bool supportsTaskShaders,
        OpenGLLineWidthRange supportedLineWidths,
        uint maxMeshWorkGroupCountX,
        uint maxMeshWorkGroupCountY,
        uint maxMeshWorkGroupCountZ,
        uint maxMeshWorkGroupTotalCount,
        uint maxTaskWorkGroupCountX,
        uint maxTaskWorkGroupCountY,
        uint maxTaskWorkGroupCountZ,
        uint maxTaskWorkGroupTotalCount)
    {
        static bool AtLeast(int major, int minor, int requiredMajor, int requiredMinor) =>
            major > requiredMajor || (major == requiredMajor && minor >= requiredMinor);

        bool hasCompute = AtLeast(major, minor, 4, 3);
        bool hasTessellation = AtLeast(major, minor, 4, 0);
        bool hasGeometry = AtLeast(major, minor, 3, 2);
        bool hasAnisotropy = gl.IsExtensionPresent("GL_EXT_texture_filter_anisotropic");

        GraphicsFeature features = GraphicsFeature.None;
        // The backend's required OpenGL baseline has core texture views and base-instance drawing.
        features |= GraphicsFeature.TextureViews |
                    GraphicsFeature.BaseVertexDrawing |
                    GraphicsFeature.BaseInstanceDrawing;
        if (hasCompute) features |= GraphicsFeature.ComputeShaders;
        if (AtLeast(major, minor, 4, 0)) features |= GraphicsFeature.IndirectDrawing;
        if (AtLeast(major, minor, 4, 3)) features |= GraphicsFeature.MultiDrawIndirect;
        if (hasTessellation) features |= GraphicsFeature.TessellationShaders;
        if (hasGeometry) features |= GraphicsFeature.GeometryShaders;
        if (hasAnisotropy) features |= GraphicsFeature.SamplerAnisotropy;
        if (supportsIndependentBlend) features |= GraphicsFeature.IndependentBlend;
        if (supportsDepthBounds) features |= GraphicsFeature.DepthBoundsTest;
        if (supportsDepthBiasClamp) features |= GraphicsFeature.DepthBiasClamp;
        if (supportsSpirV) features |= GraphicsFeature.SpirVShaders;
        if (supportsMeshShaders) features |= GraphicsFeature.MeshShaders;
        if (supportsTaskShaders) features |= GraphicsFeature.TaskShaders;
        if (supportedLineWidths.SupportsWideLines) features |= GraphicsFeature.WideLines;
        // Timestamp queries, sparse resources, and multi-viewport are intentionally not
        // advertised until the corresponding execution paths are implemented.

        features |= GraphicsFeature.LogicOperations | GraphicsFeature.FillModeNonSolid;
        if (AtLeast(major, minor, 3, 2))
            features |= GraphicsFeature.DepthClamp;

        uint maxTexture = ToUInt(gl.GetInteger(GLEnum.MaxTextureSize));
        uint maxTexture3D = ToUInt(gl.GetInteger(GLEnum.Max3DTextureSize));
        uint maxArrayLayers = ToUInt(gl.GetInteger(GLEnum.MaxArrayTextureLayers));
        uint maxVertexAttributes = ToUInt(gl.GetInteger(GLEnum.MaxVertexAttribs));
        uint maxVertexBindings = ToUInt(gl.GetInteger(GLEnum.MaxVertexAttribBindings));
        uint maxUniformBindings = ToUInt(gl.GetInteger(GLEnum.MaxUniformBufferBindings));
        uint maxComputeInvocations = hasCompute
            ? ToUInt(gl.GetInteger(GLEnum.MaxComputeWorkGroupInvocations))
            : 0;
        uint maxComputeX = 0;
        uint maxComputeY = 0;
        uint maxComputeZ = 0;
        if (hasCompute)
        {
            uniformBufferObjectApi.GetInteger(GetPName.MaxComputeWorkGroupSize, 0, out int sizeX);
            uniformBufferObjectApi.GetInteger(GetPName.MaxComputeWorkGroupSize, 1, out int sizeY);
            uniformBufferObjectApi.GetInteger(GetPName.MaxComputeWorkGroupSize, 2, out int sizeZ);
            maxComputeX = ToUInt(sizeX);
            maxComputeY = ToUInt(sizeY);
            maxComputeZ = ToUInt(sizeZ);
        }
        uint maxPatchSize = hasTessellation ? ToUInt(gl.GetInteger(GLEnum.MaxPatchVertices)) : 0;
        float maxAnisotropy = 0;
        if (hasAnisotropy)
            gl.GetFloat(GLEnum.MaxTextureMaxAnisotropy, out maxAnisotropy);

        GraphicsDeviceLimits limits = new(
            0, maxTexture, maxTexture, maxTexture3D, maxArrayLayers,
            maxVertexAttributes, maxVertexBindings, 0,
            maxComputeInvocations, maxComputeX, maxComputeY, maxComputeZ,
            maxAnisotropy, maxPatchSize,
            MaxMeshWorkGroupCountX: maxMeshWorkGroupCountX,
            MaxMeshWorkGroupCountY: maxMeshWorkGroupCountY,
            MaxMeshWorkGroupCountZ: maxMeshWorkGroupCountZ,
            MaxMeshWorkGroupTotalCount: maxMeshWorkGroupTotalCount,
            MaxTaskWorkGroupCountX: maxTaskWorkGroupCountX,
            MaxTaskWorkGroupCountY: maxTaskWorkGroupCountY,
            MaxTaskWorkGroupCountZ: maxTaskWorkGroupCountZ,
            MaxTaskWorkGroupTotalCount: maxTaskWorkGroupTotalCount);

        bool hasInternalFormatQuery2 = AtLeast(major, minor, 4, 3) ||
                                       gl.IsExtensionPresent("GL_ARB_internalformat_query2");
        GraphicsFormatCapabilities[] formats = OpenGLFormatCapabilities.Query(
            gl, internalFormatQuery, hasInternalFormatQuery2);

        GraphicsQueueCapabilities queues = GraphicsQueueCapabilities.Graphics |
                                            GraphicsQueueCapabilities.Copy;
        if (hasCompute)
            queues |= GraphicsQueueCapabilities.Compute;
        if (supportsPresentation)
            queues |= GraphicsQueueCapabilities.Present;

        return new GraphicsDeviceCapabilities(
            new GraphicsFeatureSet(features),
            queues,
            limits,
            formats);
    }

    private static bool HasEntryPoints(
        IOpenGLContextProvider contextProvider,
        params string[] names)
    {
        for (int i = 0; i < names.Length; i++)
            if (contextProvider.GetProcAddress(names[i]) == 0)
                return false;
        return true;
    }

    private static uint ToUInt(int value) => value > 0 ? (uint)value : 0;

    internal IOpenGLContextProvider ContextProvider => _contextProvider;
    internal ArbDirectStateAccess DirectStateAccess => _directStateAccess;
    internal ArbTextureView TextureViewApi => _textureViewApi;
    internal ArbSamplerObjects SamplerObjects => _samplerObjects;
    internal ArbSync SyncApi => _syncApi;
    internal ArbCopyImage CopyImageApi => _copyImageApi;
    internal ArbViewportArray ViewportApi => _viewportApi;
    internal ArbBaseInstance BaseInstanceApi => _baseInstanceApi;
    internal ArbDrawIndirect DrawIndirectApi => _drawIndirectApi;
    internal ArbMultiDrawIndirect MultiDrawIndirectApi => _multiDrawIndirectApi;
    internal ArbShaderImageLoadStore ImageLoadStoreApi => _imageLoadStoreApi;
    internal ArbDrawBuffersBlend DrawBuffersBlendApi => _drawBuffersBlendApi;
    internal ArbComputeShader ComputeShaderApi => _computeShaderApi ??
        throw new GraphicsFeatureNotSupportedException(GraphicsFeature.ComputeShaders, GraphicsFeature.ComputeShaders);

    internal T WithContext<T>(Func<GL, T> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        lock (_contextGate)
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            _contextProvider.MakeCurrent();
            try
            {
                return action(_gl);
            }
            finally
            {
                // Inline scope: activation succeeded before the try, so release runs exactly once
                // without allocating a per-operation OpenGLContextLease object.
                _contextProvider.ReleaseCurrent();
            }
        }
    }

    internal void WithContext(Action<GL> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        lock (_contextGate)
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            _contextProvider.MakeCurrent();
            try
            {
                action(_gl);
            }
            finally
            {
                _contextProvider.ReleaseCurrent();
            }
        }
    }

    /// <summary>Registers a live native resource while the device context gate is held.</summary>
    internal void RegisterUnderContext(IOpenGLNativeResource resource)
    {
        if (!_resources.Add(resource))
            throw new InvalidOperationException("An OpenGL native resource was registered more than once.");
    }

    /// <summary>
    /// Releases a resource under the same context serialization used for commands. If device disposal
    /// already owns the gate, it has responsibility for draining the registered resource set.
    /// </summary>
    internal void ReleaseResource(IOpenGLNativeResource resource)
    {
        // Some resources (currently directly mapped buffers) require user-owned pins to be
        // released before their native storage can be unmapped. Perform that wait before taking
        // the device-wide context gate so other GL operations are not blocked by a leaked pin.
        if (IsDisposed)
            return;
        if (resource is IOpenGLNativeResourcePreparation preparation)
            preparation.PrepareForNativeRelease();

        lock (_contextGate)
        {
            if (IsDisposed)
                return;

            _contextProvider.MakeCurrent();
            try
            {
                resource.ReleaseNative(_gl);
            }
            finally
            {
                _contextProvider.ReleaseCurrent();
            }

            // Keep failed/uncompleted releases registered so device shutdown can retry them.
            _resources.Remove(resource);
        }
    }

    /// <summary>
    /// Waits for an already-started device shutdown to finish. This is used only by exceptional
    /// submission/disposal races after IsDisposed has been published; it never activates GL.
    /// </summary>
    internal void WaitForShutdownCompletion()
    {
        lock (_contextGate)
        {
            // Dispose may temporarily release the gate while waiting for mapped-memory pins.
            // Wait for its final state, not merely for an intermediate gate acquisition.
            while (Volatile.Read(ref _disposeState) == 1)
                Monitor.Wait(_contextGate);
        }
    }

    internal void OnSubmissionQueued() => Interlocked.Increment(ref _pendingSubmissionCount);

    internal void OnSubmissionCompleted()
    {
        int remaining = Interlocked.Decrement(ref _pendingSubmissionCount);
        if (remaining < 0)
            throw new InvalidOperationException("OpenGL submission lifetime accounting became unbalanced.");

        // Native resource reference counts already prove when no recorded or submitted command
        // list retains a resource. Reclamation is therefore independent of unrelated submissions.
        // Device shutdown owns all still-registered resources once IsDisposed is published.
    }

    public IGraphicsCommandQueue GetQueue(GraphicsQueueType queueType, uint index = 0)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        if (index != 0)
            throw new GraphicsValidationException("The OpenGL backend exposes one logical queue per queue role.");
        if (!Enum.IsDefined(queueType))
            throw new ArgumentOutOfRangeException(nameof(queueType));
        lock (_queueGate)
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            if (queueType == GraphicsQueueType.Compute &&
                !Capabilities.QueueCapabilities.HasFlag(GraphicsQueueCapabilities.Compute))
                throw new GraphicsFeatureNotSupportedException(
                    GraphicsFeature.ComputeShaders, GraphicsFeature.ComputeShaders);

            return _queues[(int)queueType] ??= new OpenGLGraphicsCommandQueue(this, queueType);
        }
    }

    public IGraphicsQueue GetPresentationQueue(uint index = 0)
    {
        if (index != 0)
            throw new GraphicsValidationException("The OpenGL backend exposes one presentation-queue candidate.");
        if (!_contextProvider.SupportsPresentation)
            throw new GraphicsBackendUnavailableException(
                GraphicsBackend.OpenGL, "The injected OpenGL context provider has no presentation drawable.");
        return GetQueue(GraphicsQueueType.Graphics, 0);
    }

    public unsafe IGraphicsBuffer CreateBuffer(in GraphicsBufferDescription description)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        GraphicsBufferDescription bufferDescription = description;
        Capabilities.ValidateBufferDescription(bufferDescription);
        ulong sizeInBytes = bufferDescription.SizeInBytes;
        if (sizeInBytes > (ulong)nuint.MaxValue)
            throw new GraphicsValidationException("The requested buffer is larger than the native addressable size.");

        return WithContext(gl =>
        {
            _directStateAccess.CreateBuffers(1, out uint buffer);
            if (buffer == 0)
                throw new GraphicsException("OpenGL failed to create a buffer object.");

            try
            {
                _directStateAccess.NamedBufferStorage(
                    buffer,
                    (nuint)sizeInBytes,
                    null,
                    OpenGLBufferStorageMapping.GetStorageFlags(bufferDescription));
                LabelObject(ObjectIdentifier.Buffer, buffer, "Auralis Buffer");

                OpenGLGraphicsBuffer resource = new(this, bufferDescription, buffer);
                RegisterUnderContext(resource);
                return (IGraphicsBuffer)resource;
            }
            catch
            {
                gl.DeleteBuffer(buffer);
                throw;
            }
        });
    }

    public IGraphicsShader CreateShader(in GraphicsShaderDescription description)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        return OpenGLGraphicsShader.Create(this, description);
    }

    public IGraphicsTexture CreateTexture(in GraphicsTextureDescription description)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        GraphicsTextureDescription textureDescription = description;
        Capabilities.ValidateTextureDescription(textureDescription);
        if ((textureDescription.CreationOptions & GraphicsResourceCreationOptions.AllowAliasing) != 0)
            throw new GraphicsValidationException("OpenGL does not expose resource aliasing through this backend.");

        return WithContext(gl => OpenGLGraphicsTexture.CreateUnderContext(
            this, gl, _directStateAccess, textureDescription));
    }

    public IGraphicsSampler CreateSampler(in GraphicsSamplerDescription description)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        GraphicsSamplerDescription samplerDescription = description;
        Capabilities.ValidateSamplerDescription(samplerDescription);

        return WithContext(gl => OpenGLGraphicsSampler.CreateUnderContext(
            this, _directStateAccess, _samplerObjects, samplerDescription));
    }

    public IGraphicsBindingLayout CreateBindingLayout(GraphicsBindingLayoutDescription description)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        return new OpenGLGraphicsBindingLayout(this, description);
    }

    public IGraphicsPipelineLayout CreatePipelineLayout(ReadOnlySpan<IGraphicsBindingLayout> bindingLayouts)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        return new OpenGLGraphicsPipelineLayout(this, bindingLayouts);
    }

    public IGraphicsBindingSet CreateBindingSet(IGraphicsBindingLayout bindingLayout)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        ArgumentNullException.ThrowIfNull(bindingLayout);
        if (bindingLayout is not OpenGLGraphicsBindingLayout openGlLayout ||
            !ReferenceEquals(openGlLayout.Device, this))
            throw new GraphicsValidationException("The binding layout must be created by this OpenGL device.");
        return new OpenGLGraphicsBindingSet(openGlLayout);
    }

    public IGraphicsRenderPipeline CreateRenderPipeline(GraphicsRenderPipelineDescription description)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        return OpenGLGraphicsRenderPipeline.Create(this, description);
    }

    public IGraphicsComputePipeline CreateComputePipeline(GraphicsComputePipelineDescription description)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        return OpenGLGraphicsComputePipeline.Create(this, description);
    }

    public IGraphicsFence CreateFence(ulong initialValue = 0)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        return WithContext(gl =>
        {
            OpenGLGraphicsFence fence = new(this, _syncApi, initialValue);
            RegisterUnderContext(fence);
            return (IGraphicsFence)fence;
        });
    }

    public IGraphicsPresentationSurface CreatePresentationSurface(IGraphicsPresentationTarget target)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        ArgumentNullException.ThrowIfNull(target);

        if (!_contextProvider.SupportsPresentation)
            throw new GraphicsBackendUnavailableException(
                GraphicsBackend.OpenGL,
                "The injected OpenGL context provider has no presentation drawable.");

        GraphicsPresentationTargetSnapshot snapshot = target.GetSnapshot();
        OpenGLPresentationTargetValidation.ValidateTarget(_contextProvider, snapshot);
        return new OpenGLGraphicsPresentationSurface(this, target, snapshot);
    }


    /// <summary>Enables core KHR_debug output and roots the callback for its native lifetime.</summary>
    private unsafe void InitializeDebugOutput()
    {
        _debugUserData = GCHandle.Alloc(this, GCHandleType.Normal);
        DebugProc callback = OnDebugMessage;
        _debugCallback = callback;

        try
        {
            _gl.Enable(GLEnum.DebugOutput);
            _gl.Enable(GLEnum.DebugOutputSynchronous);
            // Keep actionable diagnostics while suppressing high-volume notification traffic.
            _gl.DebugMessageControl(
                GLEnum.DontCare,
                GLEnum.DontCare,
                GLEnum.DebugSeverityNotification,
                0,
                null,
                false);
            _gl.DebugMessageCallback(
                callback,
                (void*)GCHandle.ToIntPtr(_debugUserData));
            _debugLayerEnabled = true;
        }
        catch
        {
            try
            {
                _gl.DebugMessageCallback(null!, null);
                _gl.Disable(GLEnum.DebugOutputSynchronous);
                _gl.Disable(GLEnum.DebugOutput);
            }
            finally
            {
                if (_debugUserData.IsAllocated)
                    _debugUserData.Free();
                _debugCallback = null;
            }

            throw;
        }
    }

    /// <summary>Unregisters the native callback before releasing its delegate and GCHandle roots.</summary>
    private unsafe void DisableDebugOutput()
    {
        if (!_debugLayerEnabled && !_debugUserData.IsAllocated)
            return;

        try
        {
            _gl.DebugMessageCallback(null!, null);
            _gl.Disable(GLEnum.DebugOutputSynchronous);
            _gl.Disable(GLEnum.DebugOutput);
        }
        finally
        {
            if (_debugUserData.IsAllocated)
                _debugUserData.Free();
            _debugCallback = null;
            _debugLayerEnabled = false;
        }
    }

    private static void OnDebugMessage(
        GLEnum source,
        GLEnum type,
        int id,
        GLEnum severity,
        int length,
        nint message,
        nint userParam)
    {
        // Never let a managed exception cross the unmanaged driver callback boundary.
        try
        {
            if (userParam == 0)
                return;

            GCHandle target = GCHandle.FromIntPtr(userParam);
            if (target.Target is not OpenGLGraphicsDevice)
                return;

            string text = length > 0
                ? Marshal.PtrToStringUTF8(message, length) ?? string.Empty
                : string.Empty;
            string formatted = $"OpenGL [{severity}] {type} #{id} ({source}): {text}";

            if (severity == GLEnum.DebugSeverityHigh)
                Trace.TraceError(formatted);
            else if (severity == GLEnum.DebugSeverityMedium)
                Trace.TraceWarning(formatted);
            else
                Trace.WriteLine(formatted);
        }
        catch (Exception exception)
        {
            try { Trace.TraceError("OpenGL debug callback failed: {0}", exception); }
            catch { }
        }
    }

    /// <summary>Assigns a KHR_debug object label when debug output was requested.</summary>
    internal unsafe void LabelObject(ObjectIdentifier identifier, uint handle, string label)
    {
        if (!_debugLayerEnabled || handle == 0)
            return;

        int byteCount = Encoding.UTF8.GetByteCount(label);
        Span<byte> utf8 = byteCount <= 128
            ? stackalloc byte[byteCount]
            : new byte[byteCount];
        Encoding.UTF8.GetBytes(label.AsSpan(), utf8);
        fixed (byte* pointer = utf8)
            _gl.ObjectLabel((GLEnum)identifier, handle, checked((uint)byteCount), pointer);
    }

    internal unsafe void PushDebugGroup(GLEnum source, uint id, string message)
    {
        if (!_debugLayerEnabled)
            return;

        int byteCount = Encoding.UTF8.GetByteCount(message);
        Span<byte> utf8 = byteCount <= 128
            ? stackalloc byte[byteCount]
            : new byte[byteCount];
        Encoding.UTF8.GetBytes(message.AsSpan(), utf8);
        fixed (byte* pointer = utf8)
            _gl.PushDebugGroup(source, id, checked((uint)byteCount), pointer);
    }

    internal void PopDebugGroup()
    {
        if (_debugLayerEnabled)
            _gl.PopDebugGroup();
    }

    public void Dispose()
    {
        IOpenGLNativeResource[] resourcesToPrepare;
        lock (_contextGate)
        {
            if (Volatile.Read(ref _disposeState) != 0)
            {
                while (Volatile.Read(ref _disposeState) == 1)
                    Monitor.Wait(_contextGate);
                return;
            }

            // Publish shutdown before releasing the gate so no new operation/resource can enter.
            Volatile.Write(ref _disposeState, 1);
            resourcesToPrepare = _resources.ToArray();
        }

        try
        {
            // Invalidate mappings and wait for user pins without owning the shared GL context.
            foreach (IOpenGLNativeResource resource in resourcesToPrepare)
                if (resource is IOpenGLNativeResourcePreparation preparation)
                    preparation.PrepareForNativeRelease();
        }
        catch
        {
            lock (_contextGate)
            {
                Volatile.Write(ref _disposeState, 2);
                Monitor.PulseAll(_contextGate);
            }
            throw;
        }

        Exception? cleanupFailure = null;
        lock (_contextGate)
        {
            try
            {
                using OpenGLContextLease contextLease = OpenGLContextLease.Acquire(_contextProvider);
                _gl.Finish();

                foreach (OpenGLGraphicsCommandQueue? queue in _queues)
                {
                    if (queue is null)
                        continue;

                    try
                    {
                        queue.DrainUnderContext(_gl);
                    }
                    catch (Exception exception)
                    {
                        cleanupFailure ??= exception;
                    }
                }

                foreach (IOpenGLNativeResource resource in _resources)
                {
                    try
                    {
                        resource.ReleaseNative(_gl);
                    }
                    catch (Exception exception)
                    {
                        cleanupFailure ??= exception;
                    }
                }
                _resources.Clear();

                DisposeNativeApi(_uniformBufferObjectApi, ref cleanupFailure);
                DisposeNativeApi(_meshShaderApi, ref cleanupFailure);
                DisposeNativeApi(_computeShaderApi, ref cleanupFailure);
                DisposeNativeApi(_spirvApi, ref cleanupFailure);
                DisposeNativeApi(_shaderBinaryApi, ref cleanupFailure);
                DisposeNativeApi(_polygonOffsetClampApi, ref cleanupFailure);
                DisposeNativeApi(_depthBoundsApi, ref cleanupFailure);
                DisposeNativeApi(_indexedDrawBuffersApi, ref cleanupFailure);
                DisposeNativeApi(_drawBuffersBlendApi, ref cleanupFailure);
                DisposeNativeApi(_imageLoadStoreApi, ref cleanupFailure);
                DisposeNativeApi(_multiDrawIndirectApi, ref cleanupFailure);
                DisposeNativeApi(_drawIndirectApi, ref cleanupFailure);
                DisposeNativeApi(_baseInstanceApi, ref cleanupFailure);
                DisposeNativeApi(_viewportApi, ref cleanupFailure);
                DisposeNativeApi(_copyImageApi, ref cleanupFailure);
                DisposeNativeApi(_syncApi, ref cleanupFailure);
                DisposeNativeApi(_internalFormatQuery, ref cleanupFailure);
                DisposeNativeApi(_samplerObjects, ref cleanupFailure);
                DisposeNativeApi(_textureViewApi, ref cleanupFailure);

                try
                {
                    DisableDebugOutput();
                }
                catch (Exception exception)
                {
                    cleanupFailure ??= exception;
                }

                DisposeNativeApi(_directStateAccess, ref cleanupFailure);
                DisposeNativeApi(_gl, ref cleanupFailure);
            }
            catch (Exception exception)
            {
                cleanupFailure ??= exception;
            }
            finally
            {
                if (_contextProvider.Ownership == OpenGLContextOwnership.Owned)
                {
                    try
                    {
                        _contextProvider.Dispose();
                    }
                    catch (Exception exception)
                    {
                        cleanupFailure ??= exception;
                    }
                }

                Volatile.Write(ref _disposeState, 2);
                Monitor.PulseAll(_contextGate);
            }
        }

        if (cleanupFailure is not null)
            ExceptionDispatchInfo.Capture(cleanupFailure).Throw();
    }

    private static void DisposeNativeApi(IDisposable? api, ref Exception? failure)
    {
        if (api is null)
            return;

        try
        {
            api.Dispose();
        }
        catch (Exception exception)
        {
            failure ??= exception;
        }
    }
}
