using System.Runtime.InteropServices;
using Auralis.Graphics.Abstractions;
using Auralis.Graphics.Commands;
using Auralis.Graphics.Devices;
using Auralis.Graphics.Pipelines;
using Auralis.Graphics.Presentation;
using Auralis.Graphics.Resources;
using Auralis.Graphics.Synchronization;
using Silk.NET.OpenGLES;
using Silk.NET.OpenGLES.Extensions.EXT;

namespace Auralis.Graphics.Backends.OpenGLES;

/// <summary>
/// Graphics device over one externally supplied OpenGL ES context. All driver work is serialized
/// because the host context is made current on the calling thread for each operation.
/// </summary>
internal sealed class OpenGLESGraphicsDevice : IGraphicsDevice
{
    private static readonly GraphicsFormat[] s_multisampleFormats =
    [
        GraphicsFormat.R8UNorm,
        GraphicsFormat.RG8UNorm,
        GraphicsFormat.RGBA8UNorm,
        GraphicsFormat.RGBA8UNormSrgb,
        GraphicsFormat.R8UInt,
        GraphicsFormat.R8SInt,
        GraphicsFormat.RG8UInt,
        GraphicsFormat.RG8SInt,
        GraphicsFormat.RGBA8UInt,
        GraphicsFormat.RGBA8SInt,
        GraphicsFormat.D16UNorm,
        GraphicsFormat.D24UNormS8UInt,
        GraphicsFormat.D32Float,
        GraphicsFormat.D32FloatS8UInt,
        GraphicsFormat.R8SNorm,
        GraphicsFormat.RG8SNorm,
        GraphicsFormat.RGBA8SNorm,
        GraphicsFormat.R16Float,
        GraphicsFormat.RG16Float,
        GraphicsFormat.RGBA16Float,
        GraphicsFormat.R32Float,
        GraphicsFormat.RG32Float,
        GraphicsFormat.RGB32Float,
        GraphicsFormat.RGBA32Float,
        GraphicsFormat.R16UInt,
        GraphicsFormat.R16SInt,
        GraphicsFormat.RG16UInt,
        GraphicsFormat.RG16SInt,
        GraphicsFormat.RGBA16UInt,
        GraphicsFormat.RGBA16SInt,
        GraphicsFormat.R32UInt,
        GraphicsFormat.R32SInt,
        GraphicsFormat.RG32UInt,
        GraphicsFormat.RG32SInt,
        GraphicsFormat.RGBA32UInt,
        GraphicsFormat.RGBA32SInt
    ];

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    internal unsafe delegate void MultiDrawArraysIndirectExtDelegate(
        PrimitiveType mode, void* indirect, uint drawCount, uint stride);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    internal unsafe delegate void MultiDrawElementsIndirectExtDelegate(
        PrimitiveType mode, DrawElementsType type, void* indirect, uint drawCount, uint stride);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    internal delegate void CopyImageSubDataOesDelegate(
        uint source, TextureTarget sourceTarget, int sourceLevel,
        int sourceX, int sourceY, int sourceZ,
        uint destination, TextureTarget destinationTarget, int destinationLevel,
        int destinationX, int destinationY, int destinationZ,
        uint width, uint height, uint depth);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    internal delegate void DrawMeshTasksExtDelegate(uint numGroupsX, uint numGroupsY, uint numGroupsZ);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    internal delegate void DrawMeshTasksIndirectExtDelegate(nint indirect);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    internal delegate void MultiDrawMeshTasksIndirectExtDelegate(nint indirect, uint drawCount, uint stride);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    internal delegate void TextureViewExtDelegate(
        uint texture, TextureTarget target, uint originalTexture, SizedInternalFormat internalFormat,
        uint minimumLevel, uint levelCount, uint minimumLayer, uint layerCount);

    private readonly IOpenGLESContextProvider _contextProvider;
    private readonly GL _gl;
    private readonly MultiDrawArraysIndirectExtDelegate? _multiDrawArraysIndirectExt;
    private readonly MultiDrawElementsIndirectExtDelegate? _multiDrawElementsIndirectExt;
    private readonly ExtDrawBuffersIndexed? _drawBuffersIndexedApi;
    private readonly ExtCopyImage? _copyImageApi;
    private readonly CopyImageSubDataOesDelegate? _copyImageSubDataOes;
    private readonly TextureViewExtDelegate? _textureViewExt;
    private readonly ExtDrawElementsBaseVertex? _drawElementsBaseVertexApi;
    private readonly ExtBaseInstance? _baseInstanceApi;
    private readonly DrawMeshTasksExtDelegate? _drawMeshTasksExt;
    private readonly DrawMeshTasksIndirectExtDelegate? _drawMeshTasksIndirectExt;
    private readonly MultiDrawMeshTasksIndirectExtDelegate? _multiDrawMeshTasksIndirectExt;
    private readonly object _contextGate = new();
    private readonly object _resourceGate = new();
    private readonly HashSet<IOpenGLESNativeResource> _resources = new(ReferenceEqualityComparer.Instance);
    private readonly object _queueGate = new();
    private readonly OpenGLESGraphicsCommandQueue?[] _queues = new OpenGLESGraphicsCommandQueue?[3];
    private readonly bool _ownsContextProvider;
    private int _disposed;

    private OpenGLESGraphicsDevice(
        IOpenGLESContextProvider contextProvider,
        GL gl,
        MultiDrawArraysIndirectExtDelegate? multiDrawArraysIndirectExt,
        MultiDrawElementsIndirectExtDelegate? multiDrawElementsIndirectExt,
        ExtDrawBuffersIndexed? drawBuffersIndexedApi,
        ExtCopyImage? copyImageApi,
        CopyImageSubDataOesDelegate? copyImageSubDataOes,
        TextureViewExtDelegate? textureViewExt,
        ExtDrawElementsBaseVertex? drawElementsBaseVertexApi,
        ExtBaseInstance? baseInstanceApi,
        DrawMeshTasksExtDelegate? drawMeshTasksExt,
        DrawMeshTasksIndirectExtDelegate? drawMeshTasksIndirectExt,
        MultiDrawMeshTasksIndirectExtDelegate? multiDrawMeshTasksIndirectExt,
        OpenGLESVersion version,
        string versionString,
        string vendor,
        string renderer,
        OpenGLESCapabilityProfile profile,
        GraphicsDeviceCapabilities capabilities,
        in OpenGLESQueriedLimits limits)
    {
        _contextProvider = contextProvider;
        _gl = gl;
        _multiDrawArraysIndirectExt = multiDrawArraysIndirectExt;
        _multiDrawElementsIndirectExt = multiDrawElementsIndirectExt;
        _drawBuffersIndexedApi = drawBuffersIndexedApi;
        _copyImageApi = copyImageApi;
        _copyImageSubDataOes = copyImageSubDataOes;
        _textureViewExt = textureViewExt;
        _drawElementsBaseVertexApi = drawElementsBaseVertexApi;
        _baseInstanceApi = baseInstanceApi;
        _drawMeshTasksExt = drawMeshTasksExt;
        _drawMeshTasksIndirectExt = drawMeshTasksIndirectExt;
        _multiDrawMeshTasksIndirectExt = multiDrawMeshTasksIndirectExt;
        Version = version;
        VersionString = versionString;
        Vendor = vendor;
        Renderer = renderer;
        Profile = profile;
        Capabilities = capabilities;
        MaxUniformBufferBindings = limits.MaxUniformBufferBindings;
        MaxStorageBufferBindings = limits.MaxStorageBufferBindings;
        MaxTextureUnits = limits.MaxTextureUnits;
        MaxImageUnits = limits.MaxImageUnits;
        UniformBufferOffsetAlignment = limits.UniformBufferOffsetAlignment;
        StorageBufferOffsetAlignment = limits.StorageBufferOffsetAlignment;
        MaxComputeWorkGroupCountX = limits.MaxComputeWorkGroupCountX;
        MaxComputeWorkGroupCountY = limits.MaxComputeWorkGroupCountY;
        MaxComputeWorkGroupCountZ = limits.MaxComputeWorkGroupCountZ;
        MaxMeshWorkGroupCountX = limits.MaxMeshWorkGroupCountX;
        MaxMeshWorkGroupCountY = limits.MaxMeshWorkGroupCountY;
        MaxMeshWorkGroupCountZ = limits.MaxMeshWorkGroupCountZ;
        MaxMeshWorkGroupTotalCount = limits.MaxMeshWorkGroupTotalCount;
        MaxTaskWorkGroupCountX = limits.MaxTaskWorkGroupCountX;
        MaxTaskWorkGroupCountY = limits.MaxTaskWorkGroupCountY;
        MaxTaskWorkGroupCountZ = limits.MaxTaskWorkGroupCountZ;
        MaxTaskWorkGroupTotalCount = limits.MaxTaskWorkGroupTotalCount;
        _ownsContextProvider = contextProvider.Ownership == OpenGLESContextOwnership.Owned;

        Backend = GraphicsBackend.OpenGLES;
        Adapter = new GraphicsAdapterInfo(
            default,
            renderer,
            GraphicsAdapterType.Unknown,
            0,
            0,
            0);
        Info = new GraphicsDeviceInfo(
            renderer,
            vendor,
            renderer,
            versionString,
            0,
            0,
            renderer.Contains("llvmpipe", StringComparison.OrdinalIgnoreCase) ||
            renderer.Contains("swiftshader", StringComparison.OrdinalIgnoreCase));
    }

    public GraphicsBackend Backend { get; }

    public GraphicsAdapterInfo Adapter { get; }

    public GraphicsDeviceInfo Info { get; }

    public GraphicsDeviceCapabilities Capabilities { get; }

    public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    public OpenGLESVersion Version { get; }

    public string VersionString { get; }

    public string Vendor { get; }

    public string Renderer { get; }

    public OpenGLESCapabilityProfile Profile { get; }

    internal GL Api => _gl;
    internal MultiDrawArraysIndirectExtDelegate? MultiDrawArraysIndirectExt => _multiDrawArraysIndirectExt;
    internal MultiDrawElementsIndirectExtDelegate? MultiDrawElementsIndirectExt => _multiDrawElementsIndirectExt;
    internal ExtDrawBuffersIndexed? DrawBuffersIndexedApi => _drawBuffersIndexedApi;
    internal ExtCopyImage? CopyImageApi => _copyImageApi;
    internal CopyImageSubDataOesDelegate? CopyImageSubDataOes => _copyImageSubDataOes;
    internal bool HasImageCopy => _copyImageApi is not null || _copyImageSubDataOes is not null;
    internal bool SupportsCubeMapArray =>
        Version.IsAtLeast(3, 2) || Profile.HasExtension("GL_OES_texture_cube_map_array");
    internal TextureViewExtDelegate? TextureViewExt => _textureViewExt;
    internal ExtDrawElementsBaseVertex? DrawElementsBaseVertexApi => _drawElementsBaseVertexApi;
    internal ExtBaseInstance? BaseInstanceApi => _baseInstanceApi;
    internal DrawMeshTasksExtDelegate? DrawMeshTasksExt => _drawMeshTasksExt;
    internal DrawMeshTasksIndirectExtDelegate? DrawMeshTasksIndirectExt => _drawMeshTasksIndirectExt;
    internal MultiDrawMeshTasksIndirectExtDelegate? MultiDrawMeshTasksIndirectExt => _multiDrawMeshTasksIndirectExt;

    internal uint MaxUniformBufferBindings { get; }
    internal uint MaxStorageBufferBindings { get; }
    internal uint MaxTextureUnits { get; }
    internal uint MaxImageUnits { get; }
    internal uint UniformBufferOffsetAlignment { get; }
    internal uint StorageBufferOffsetAlignment { get; }
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

    internal static OpenGLESGraphicsDevice Create(
        IOpenGLESContextProvider contextProvider,
        GraphicsDeviceOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(contextProvider);
        cancellationToken.ThrowIfCancellationRequested();

        GL? gl = null;
        MultiDrawArraysIndirectExtDelegate? multiDrawArraysIndirectExt = null;
        MultiDrawElementsIndirectExtDelegate? multiDrawElementsIndirectExt = null;
        ExtDrawBuffersIndexed? drawBuffersIndexedApi = null;
        ExtCopyImage? copyImageApi = null;
        CopyImageSubDataOesDelegate? copyImageSubDataOes = null;
        TextureViewExtDelegate? textureViewExt = null;
        ExtDrawElementsBaseVertex? drawElementsBaseVertexApi = null;
        ExtBaseInstance? baseInstanceApi = null;
        DrawMeshTasksExtDelegate? drawMeshTasksExt = null;
        DrawMeshTasksIndirectExtDelegate? drawMeshTasksIndirectExt = null;
        MultiDrawMeshTasksIndirectExtDelegate? multiDrawMeshTasksIndirectExt = null;
        contextProvider.MakeCurrent();
        try
        {
            gl = GL.GetApi(contextProvider.GetProcAddress);
            string versionString = gl.GetStringS(StringName.Version);
            if (!OpenGLESVersion.TryParse(versionString, out OpenGLESVersion version))
                throw new GraphicsBackendUnavailableException(
                    GraphicsBackend.OpenGLES,
                    $"The supplied context did not report an OpenGL ES version (GL_VERSION='{versionString}').");

            bool hasMultiDrawIndirectExtension = gl.IsExtensionPresent("GL_EXT_multi_draw_indirect");
            if (hasMultiDrawIndirectExtension)
            {
                nint arraysAddress = contextProvider.GetProcAddress("glMultiDrawArraysIndirectEXT");
                nint elementsAddress = contextProvider.GetProcAddress("glMultiDrawElementsIndirectEXT");
                if (arraysAddress != 0 && elementsAddress != 0)
                {
                    multiDrawArraysIndirectExt =
                        Marshal.GetDelegateForFunctionPointer<MultiDrawArraysIndirectExtDelegate>(arraysAddress);
                    multiDrawElementsIndirectExt =
                        Marshal.GetDelegateForFunctionPointer<MultiDrawElementsIndirectExtDelegate>(elementsAddress);
                }
            }

            if (gl.IsExtensionPresent(ExtDrawBuffersIndexed.ExtensionName) &&
                HasEntryPoints(
                    contextProvider,
                    "glEnableiEXT",
                    "glDisableiEXT",
                    "glBlendEquationSeparateiEXT",
                    "glBlendFuncSeparateiEXT",
                    "glColorMaskiEXT"))
            {
                drawBuffersIndexedApi = new ExtDrawBuffersIndexed(gl.Context);
            }

            if (gl.IsExtensionPresent(ExtCopyImage.ExtensionName) &&
                contextProvider.GetProcAddress("glCopyImageSubDataEXT") != 0)
            {
                copyImageApi = new ExtCopyImage(gl.Context);
            }
            if (gl.IsExtensionPresent("GL_OES_copy_image"))
            {
                nint copyImageAddress = contextProvider.GetProcAddress("glCopyImageSubDataOES");
                if (copyImageAddress != 0)
                    copyImageSubDataOes =
                        Marshal.GetDelegateForFunctionPointer<CopyImageSubDataOesDelegate>(copyImageAddress);
            }
            nint textureViewAddress = 0;
            if (gl.IsExtensionPresent("GL_EXT_texture_view"))
                textureViewAddress = contextProvider.GetProcAddress("glTextureViewEXT");
            if (textureViewAddress == 0 && gl.IsExtensionPresent("GL_OES_texture_view"))
                textureViewAddress = contextProvider.GetProcAddress("glTextureViewOES");
            if (textureViewAddress != 0)
                textureViewExt = Marshal.GetDelegateForFunctionPointer<TextureViewExtDelegate>(textureViewAddress);

            bool hasBaseVertexExtension =
                gl.IsExtensionPresent(ExtDrawElementsBaseVertex.ExtensionName) &&
                contextProvider.GetProcAddress("glDrawElementsInstancedBaseVertexEXT") != 0;
            if (hasBaseVertexExtension)
                drawElementsBaseVertexApi = new ExtDrawElementsBaseVertex(gl.Context);

            // EXT_base_instance depends on EXT_draw_elements_base_vertex. Only expose its
            // direct-draw contract after resolving every entry point this command list can call.
            if (hasBaseVertexExtension &&
                gl.IsExtensionPresent(ExtBaseInstance.ExtensionName) &&
                HasEntryPoints(
                    contextProvider,
                    "glDrawArraysInstancedBaseInstanceEXT",
                    "glDrawElementsInstancedBaseInstanceEXT",
                    "glDrawElementsInstancedBaseVertexBaseInstanceEXT"))
            {
                baseInstanceApi = new ExtBaseInstance(gl.Context);
            }

            bool hasMeshShaderExtension = gl.IsExtensionPresent("GL_EXT_mesh_shader");
            if (hasMeshShaderExtension)
            {
                nint meshAddress = contextProvider.GetProcAddress("glDrawMeshTasksEXT");
                nint meshIndirectAddress = contextProvider.GetProcAddress("glDrawMeshTasksIndirectEXT");
                nint multiMeshIndirectAddress = contextProvider.GetProcAddress("glMultiDrawMeshTasksIndirectEXT");

                if (meshAddress != 0 && meshIndirectAddress != 0)
                {
                    drawMeshTasksExt =
                        Marshal.GetDelegateForFunctionPointer<DrawMeshTasksExtDelegate>(meshAddress);
                    drawMeshTasksIndirectExt =
                        Marshal.GetDelegateForFunctionPointer<DrawMeshTasksIndirectExtDelegate>(meshIndirectAddress);
                }

                if (multiMeshIndirectAddress != 0)
                {
                    multiDrawMeshTasksIndirectExt =
                        Marshal.GetDelegateForFunctionPointer<MultiDrawMeshTasksIndirectExtDelegate>(
                            multiMeshIndirectAddress);
                }
            }

            OpenGLESCapabilityProfile profile = QueryProfile(
                gl,
                version,
                contextProvider.SupportsPresentation,
                multiDrawArraysIndirectExt is not null && multiDrawElementsIndirectExt is not null,
                drawBuffersIndexedApi is not null,
                drawElementsBaseVertexApi is not null,
                baseInstanceApi is not null,
                textureViewExt is not null,
                drawMeshTasksExt is not null && drawMeshTasksIndirectExt is not null);
            profile.EnsureRequiredBaseline();

            string vendor = gl.GetStringS(StringName.Vendor);
            string renderer = gl.GetStringS(StringName.Renderer);
            OpenGLESQueriedLimits queriedLimits = QueryLimits(gl, profile);
            GraphicsDeviceCapabilities capabilities = QueryCapabilities(
                profile, queriedLimits,
                supportsImageCopy: copyImageApi is not null || copyImageSubDataOes is not null);

            cancellationToken.ThrowIfCancellationRequested();

            OpenGLESGraphicsDevice device = new(
                contextProvider, gl, multiDrawArraysIndirectExt, multiDrawElementsIndirectExt,
                drawBuffersIndexedApi, copyImageApi, copyImageSubDataOes, textureViewExt,
                drawElementsBaseVertexApi, baseInstanceApi,
                drawMeshTasksExt, drawMeshTasksIndirectExt, multiDrawMeshTasksIndirectExt,
                version, versionString, vendor, renderer, profile, capabilities, queriedLimits);
            gl = null;
            multiDrawArraysIndirectExt = null;
            multiDrawElementsIndirectExt = null;
            drawBuffersIndexedApi = null;
            copyImageApi = null;
            copyImageSubDataOes = null;
            textureViewExt = null;
            drawElementsBaseVertexApi = null;
            baseInstanceApi = null;
            drawMeshTasksExt = null;
            drawMeshTasksIndirectExt = null;
            multiDrawMeshTasksIndirectExt = null;
            return device;
        }
        catch
        {
            baseInstanceApi?.Dispose();
            drawElementsBaseVertexApi?.Dispose();
            copyImageApi?.Dispose();
            drawBuffersIndexedApi?.Dispose();
            gl?.Dispose();
            throw;
        }
        finally
        {
            contextProvider.ReleaseCurrent();
        }
    }

    private static OpenGLESCapabilityProfile QueryProfile(
        GL gl,
        OpenGLESVersion version,
        bool supportsPresentation,
        bool multiDrawIndirectEntryPointsAvailable,
        bool indexedBlendEntryPointsAvailable,
        bool baseVertexEntryPointsAvailable,
        bool baseInstanceEntryPointsAvailable,
        bool textureViewEntryPointAvailable,
        bool meshShaderEntryPointsAvailable)
    {
        int extensionCount = Math.Max(0, gl.GetInteger(GLEnum.NumExtensions));
        string[] extensions = new string[extensionCount];
        for (int i = 0; i < extensionCount; i++)
            extensions[i] = gl.GetStringS(StringName.Extensions, (uint)i);

        return OpenGLESCapabilityProfile.Create(
            version, extensions, supportsPresentation,
            multiDrawIndirectEntryPointsAvailable,
            indexedBlendEntryPointsAvailable,
            baseVertexEntryPointsAvailable,
            baseInstanceEntryPointsAvailable,
            textureViewEntryPointAvailable,
            meshShaderEntryPointsAvailable);
    }

    private static OpenGLESQueriedLimits QueryLimits(GL gl, OpenGLESCapabilityProfile profile)
    {
        uint uniformBindings = ToUInt(gl.GetInteger(GLEnum.MaxUniformBufferBindings));
        uint storageBindings = profile.Version.IsAtLeast(3, 1)
            ? ToUInt(gl.GetInteger(GLEnum.MaxShaderStorageBufferBindings))
            : 0;
        uint imageUnits = profile.Version.IsAtLeast(3, 1)
            ? ToUInt(gl.GetInteger(GLEnum.MaxImageUnits))
            : 0;
        uint textureUnits = ToUInt(gl.GetInteger(GLEnum.MaxCombinedTextureImageUnits));
        uint uniformAlignment = ToUInt(gl.GetInteger(GLEnum.UniformBufferOffsetAlignment));
        uint storageAlignment = profile.Version.IsAtLeast(3, 1)
            ? ToUInt(gl.GetInteger(GLEnum.ShaderStorageBufferOffsetAlignment))
            : 1;
        uint maxTextureSize = ToUInt(gl.GetInteger(GLEnum.MaxTextureSize));
        uint max3DTextureSize = ToUInt(gl.GetInteger(GLEnum.Max3DTextureSize));
        uint maxArrayLayers = ToUInt(gl.GetInteger(GLEnum.MaxArrayTextureLayers));
        uint maxVertexAttributes = ToUInt(gl.GetInteger(GLEnum.MaxVertexAttribs));
        uint maxColorAttachments = ToUInt(gl.GetInteger(GLEnum.MaxColorAttachments));
        uint maxColorDrawBuffers = ToUInt(gl.GetInteger(GLEnum.MaxDrawBuffers));
        GraphicsSampleCountFlags[] formatSampleCounts =
            new GraphicsSampleCountFlags[Enum.GetValues<GraphicsFormat>().Length];
        Array.Fill(formatSampleCounts, GraphicsSampleCountFlags.One);
        if (profile.Version.IsAtLeast(3, 1))
        {
            for (int i = 0; i < s_multisampleFormats.Length; i++)
            {
                GraphicsFormat format = s_multisampleFormats[i];
                if ((format is GraphicsFormat.D32Float or GraphicsFormat.D32FloatS8UInt) &&
                    !profile.Version.IsAtLeast(3, 2))
                    continue;

                formatSampleCounts[(int)format] = QueryTextureSampleCounts(gl, format);
            }
        }
        if (maxColorAttachments == 0 || maxColorDrawBuffers == 0)
            throw new GraphicsBackendUnavailableException(GraphicsBackend.OpenGLES,
                "The GLES context did not report valid color-attachment and draw-buffer limits.");
        uint maxComputeInvocations = 0;
        uint maxComputeWorkGroupSizeX = 0;
        uint maxComputeWorkGroupSizeY = 0;
        uint maxComputeWorkGroupSizeZ = 0;
        uint maxComputeWorkGroupCountX = 0;
        uint maxComputeWorkGroupCountY = 0;
        uint maxComputeWorkGroupCountZ = 0;
        if (profile.Version.IsAtLeast(3, 1))
        {
            maxComputeInvocations = ToUInt(gl.GetInteger(GLEnum.MaxComputeWorkGroupInvocations));

            gl.GetInteger(GLEnum.MaxComputeWorkGroupSize, 0, out int workGroupSizeX);
            gl.GetInteger(GLEnum.MaxComputeWorkGroupSize, 1, out int workGroupSizeY);
            gl.GetInteger(GLEnum.MaxComputeWorkGroupSize, 2, out int workGroupSizeZ);
            maxComputeWorkGroupSizeX = ToUInt(workGroupSizeX);
            maxComputeWorkGroupSizeY = ToUInt(workGroupSizeY);
            maxComputeWorkGroupSizeZ = ToUInt(workGroupSizeZ);

            gl.GetInteger(GLEnum.MaxComputeWorkGroupCount, 0, out int workGroupCountX);
            gl.GetInteger(GLEnum.MaxComputeWorkGroupCount, 1, out int workGroupCountY);
            gl.GetInteger(GLEnum.MaxComputeWorkGroupCount, 2, out int workGroupCountZ);
            maxComputeWorkGroupCountX = ToUInt(workGroupCountX);
            maxComputeWorkGroupCountY = ToUInt(workGroupCountY);
            maxComputeWorkGroupCountZ = ToUInt(workGroupCountZ);

            if (maxComputeInvocations == 0 ||
                maxComputeWorkGroupSizeX == 0 ||
                maxComputeWorkGroupSizeY == 0 ||
                maxComputeWorkGroupSizeZ == 0 ||
                maxComputeWorkGroupCountX == 0 ||
                maxComputeWorkGroupCountY == 0 ||
                maxComputeWorkGroupCountZ == 0)
            {
                throw new GraphicsBackendUnavailableException(
                    GraphicsBackend.OpenGLES,
                    "The GLES 3.1+ context did not return valid compute work-group size and dispatch-count limits.");
            }
        }

        uint maxTessellationPatchSize = profile.Version.IsAtLeast(3, 2)
            ? ToUInt(gl.GetInteger(GLEnum.MaxPatchVertices))
            : 0;

        uint maxMeshWorkGroupCountX = 0;
        uint maxMeshWorkGroupCountY = 0;
        uint maxMeshWorkGroupCountZ = 0;
        uint maxMeshWorkGroupTotalCount = 0;
        uint maxTaskWorkGroupCountX = 0;
        uint maxTaskWorkGroupCountY = 0;
        uint maxTaskWorkGroupCountZ = 0;
        uint maxTaskWorkGroupTotalCount = 0;
        if (profile.Features.Supports(GraphicsFeature.MeshShaders))
        {
            // GL_EXT_mesh_shader limits use indexed GL_MAX_*_WORK_GROUP_COUNT_EXT queries.
            gl.GetInteger((GLEnum)0x9752, 0, out int meshCountX); // GL_MAX_MESH_WORK_GROUP_COUNT_EXT
            gl.GetInteger((GLEnum)0x9752, 1, out int meshCountY);
            gl.GetInteger((GLEnum)0x9752, 2, out int meshCountZ);
            gl.GetInteger((GLEnum)0x9751, 0, out int taskCountX); // GL_MAX_TASK_WORK_GROUP_COUNT_EXT
            gl.GetInteger((GLEnum)0x9751, 1, out int taskCountY);
            gl.GetInteger((GLEnum)0x9751, 2, out int taskCountZ);
            maxMeshWorkGroupCountX = ToUInt(meshCountX);
            maxMeshWorkGroupCountY = ToUInt(meshCountY);
            maxMeshWorkGroupCountZ = ToUInt(meshCountZ);
            maxTaskWorkGroupCountX = ToUInt(taskCountX);
            maxTaskWorkGroupCountY = ToUInt(taskCountY);
            maxTaskWorkGroupCountZ = ToUInt(taskCountZ);
            maxMeshWorkGroupTotalCount = ToUInt(gl.GetInteger((GLEnum)0x9741)); // GL_MAX_MESH_WORK_GROUP_TOTAL_COUNT_EXT
            maxTaskWorkGroupTotalCount = ToUInt(gl.GetInteger((GLEnum)0x9740)); // GL_MAX_TASK_WORK_GROUP_TOTAL_COUNT_EXT

            if (maxMeshWorkGroupCountX == 0 || maxMeshWorkGroupCountY == 0 ||
                maxMeshWorkGroupCountZ == 0 || maxMeshWorkGroupTotalCount == 0 ||
                maxTaskWorkGroupCountX == 0 || maxTaskWorkGroupCountY == 0 ||
                maxTaskWorkGroupCountZ == 0 || maxTaskWorkGroupTotalCount == 0)
            {
                throw new GraphicsBackendUnavailableException(
                    GraphicsBackend.OpenGLES,
                    "GL_EXT_mesh_shader was advertised but returned zero mesh/task work-group limits.");
            }
        }

        float maxAnisotropy = 1;
        if (profile.Features.Supports(GraphicsFeature.SamplerAnisotropy))
        {
            gl.GetFloat((GLEnum)0x84FF, out maxAnisotropy);
            maxAnisotropy = Math.Max(1, maxAnisotropy);
        }

        return new OpenGLESQueriedLimits(
            maxTextureSize,
            max3DTextureSize,
            maxArrayLayers,
            maxVertexAttributes,
            uniformBindings,
            storageBindings,
            textureUnits,
            imageUnits,
            uniformAlignment,
            storageAlignment,
            maxComputeInvocations,
            maxComputeWorkGroupSizeX,
            maxComputeWorkGroupSizeY,
            maxComputeWorkGroupSizeZ,
            maxComputeWorkGroupCountX,
            maxComputeWorkGroupCountY,
            maxComputeWorkGroupCountZ,
            maxTessellationPatchSize,
            maxAnisotropy,
            maxColorAttachments,
            maxColorDrawBuffers,
            formatSampleCounts,
            maxMeshWorkGroupCountX,
            maxMeshWorkGroupCountY,
            maxMeshWorkGroupCountZ,
            maxMeshWorkGroupTotalCount,
            maxTaskWorkGroupCountX,
            maxTaskWorkGroupCountY,
            maxTaskWorkGroupCountZ,
            maxTaskWorkGroupTotalCount);
    }

    internal static GraphicsDeviceCapabilities QueryCapabilities(
        OpenGLESCapabilityProfile profile,
        in OpenGLESQueriedLimits queried,
        bool supportsImageCopy = false)
    {
        GraphicsDeviceLimits limits = new(
            MaxBufferSizeInBytes: 0,
            MaxTextureDimension1D: 0,
            MaxTextureDimension2D: queried.MaxTextureSize,
            MaxTextureDimension3D: queried.MaxTexture3DSize,
            MaxTextureArrayLayers: queried.MaxArrayLayers,
            MaxVertexInputAttributes: queried.MaxVertexAttributes,
            MaxVertexInputBindings: queried.MaxVertexAttributes,
            MaxBoundDescriptorSets: 0,
            MaxComputeWorkGroupInvocations: queried.MaxComputeInvocations,
            MaxComputeWorkGroupSizeX: queried.MaxComputeWorkGroupSizeX,
            MaxComputeWorkGroupSizeY: queried.MaxComputeWorkGroupSizeY,
            MaxComputeWorkGroupSizeZ: queried.MaxComputeWorkGroupSizeZ,
            MaxSamplerAnisotropy: queried.MaxAnisotropy,
            MaxTessellationPatchSize: queried.MaxTessellationPatchSize,
            MaxColorAttachments: queried.MaxColorAttachments,
            MaxColorDrawBuffers: queried.MaxColorDrawBuffers,
            MaxMeshWorkGroupCountX: queried.MaxMeshWorkGroupCountX,
            MaxMeshWorkGroupCountY: queried.MaxMeshWorkGroupCountY,
            MaxMeshWorkGroupCountZ: queried.MaxMeshWorkGroupCountZ,
            MaxMeshWorkGroupTotalCount: queried.MaxMeshWorkGroupTotalCount,
            MaxTaskWorkGroupCountX: queried.MaxTaskWorkGroupCountX,
            MaxTaskWorkGroupCountY: queried.MaxTaskWorkGroupCountY,
            MaxTaskWorkGroupCountZ: queried.MaxTaskWorkGroupCountZ,
            MaxTaskWorkGroupTotalCount: queried.MaxTaskWorkGroupTotalCount);

        GraphicsFormatSupport colorSupport =
            GraphicsFormatSupport.Sampled |
            GraphicsFormatSupport.ColorAttachment |
            GraphicsFormatSupport.Blend |
            GraphicsFormatSupport.VertexBuffer |
            GraphicsFormatSupport.CopySource |
            GraphicsFormatSupport.CopyDestination |
            GraphicsFormatSupport.LinearFiltering;

        GraphicsFormatSupport normalizedSingleSupport =
            GraphicsFormatSupport.Sampled |
            GraphicsFormatSupport.ColorAttachment |
            GraphicsFormatSupport.VertexBuffer |
            GraphicsFormatSupport.LinearFiltering;

        GraphicsFormatSupport signedNormalizedSupport =
            GraphicsFormatSupport.Sampled | GraphicsFormatSupport.LinearFiltering;
        GraphicsFormatSupport halfFloatSampledSupport =
            GraphicsFormatSupport.Sampled | GraphicsFormatSupport.LinearFiltering;
        GraphicsFormatSupport floatingSampledSupport = GraphicsFormatSupport.Sampled;
        if (profile.HasExtension("GL_OES_texture_float_linear"))
            floatingSampledSupport |= GraphicsFormatSupport.LinearFiltering;

        GraphicsFormatSupport halfFloatColorSupport = halfFloatSampledSupport;
        if (profile.HasExtension("GL_EXT_color_buffer_half_float") ||
            profile.HasExtension("GL_EXT_color_buffer_float"))
            halfFloatColorSupport |= GraphicsFormatSupport.ColorAttachment;

        GraphicsFormatSupport fullFloatColorSupport = floatingSampledSupport;
        if (profile.HasExtension("GL_EXT_color_buffer_float"))
            fullFloatColorSupport |= GraphicsFormatSupport.ColorAttachment;

        GraphicsFormatSupport integerStorageSupport = GraphicsFormatSupport.Sampled;
        if (profile.Version.IsAtLeast(3, 1))
            integerStorageSupport |= GraphicsFormatSupport.Storage;

        GraphicsFormatSupport floatStorageSupport = floatingSampledSupport;
        if (profile.Version.IsAtLeast(3, 1))
            floatStorageSupport |= GraphicsFormatSupport.Storage;

        if (supportsImageCopy)
        {
            normalizedSingleSupport |= GraphicsFormatSupport.CopySource | GraphicsFormatSupport.CopyDestination;
            signedNormalizedSupport |= GraphicsFormatSupport.CopySource | GraphicsFormatSupport.CopyDestination;
            halfFloatSampledSupport |= GraphicsFormatSupport.CopySource | GraphicsFormatSupport.CopyDestination;
            floatingSampledSupport |= GraphicsFormatSupport.CopySource | GraphicsFormatSupport.CopyDestination;
            halfFloatColorSupport |= GraphicsFormatSupport.CopySource | GraphicsFormatSupport.CopyDestination;
            fullFloatColorSupport |= GraphicsFormatSupport.CopySource | GraphicsFormatSupport.CopyDestination;
            integerStorageSupport |= GraphicsFormatSupport.CopySource | GraphicsFormatSupport.CopyDestination;
        }

        // Integer color formats are sampleable and color-renderable, but do not support
        // linear filtering or fixed-function blending.
        GraphicsFormatSupport integerColorSupport =
            GraphicsFormatSupport.Sampled |
            GraphicsFormatSupport.ColorAttachment;
        if (profile.Version.IsAtLeast(3, 1))
            integerColorSupport |= GraphicsFormatSupport.Storage;

        if (supportsImageCopy)
            integerColorSupport |= GraphicsFormatSupport.CopySource | GraphicsFormatSupport.CopyDestination;

        GraphicsFormatSupport depthSupport =
            GraphicsFormatSupport.Sampled |
            GraphicsFormatSupport.DepthStencilAttachment;

        if (supportsImageCopy)
            depthSupport |= GraphicsFormatSupport.CopySource | GraphicsFormatSupport.CopyDestination;

        GraphicsSampleCountFlags[]? formatSampleCounts = queried.FormatSampleCounts;
        GraphicsSampleCountFlags SampleCounts(GraphicsFormat format)
        {
            int index = (int)format;
            return formatSampleCounts is not null &&
                (uint)index < (uint)formatSampleCounts.Length &&
                formatSampleCounts[index] != GraphicsSampleCountFlags.None
                ? formatSampleCounts[index]
                : GraphicsSampleCountFlags.One;
        }

        GraphicsFormatSupport rgba8Support = colorSupport;
        if (profile.Version.IsAtLeast(3, 1))
            rgba8Support |= GraphicsFormatSupport.Storage;

        GraphicsFormatSupport halfFloatStorageSupport = halfFloatColorSupport;
        GraphicsFormatSupport fullFloatStorageSupport = fullFloatColorSupport;
        if (profile.Version.IsAtLeast(3, 1))
        {
            halfFloatStorageSupport |= GraphicsFormatSupport.Storage;
            fullFloatStorageSupport |= GraphicsFormatSupport.Storage;
        }
        if (supportsImageCopy)
        {
            halfFloatStorageSupport |= GraphicsFormatSupport.CopySource | GraphicsFormatSupport.CopyDestination;
            fullFloatStorageSupport |= GraphicsFormatSupport.CopySource | GraphicsFormatSupport.CopyDestination;
        }

        List<GraphicsFormatCapabilities> formats =
        [
            new(GraphicsFormat.R8UNorm, normalizedSingleSupport, SampleCounts(GraphicsFormat.R8UNorm)),
            new(GraphicsFormat.RG8UNorm, normalizedSingleSupport, SampleCounts(GraphicsFormat.RG8UNorm)),
            new(GraphicsFormat.RGBA8UNorm, rgba8Support, SampleCounts(GraphicsFormat.RGBA8UNorm)),
            new(GraphicsFormat.R8UInt, integerColorSupport, SampleCounts(GraphicsFormat.R8UInt)),
            new(GraphicsFormat.R8SInt, integerColorSupport, SampleCounts(GraphicsFormat.R8SInt)),
            new(GraphicsFormat.RG8UInt, integerColorSupport, SampleCounts(GraphicsFormat.RG8UInt)),
            new(GraphicsFormat.RG8SInt, integerColorSupport, SampleCounts(GraphicsFormat.RG8SInt)),
            new(GraphicsFormat.RGBA8UInt, integerColorSupport, SampleCounts(GraphicsFormat.RGBA8UInt)),
            new(GraphicsFormat.RGBA8SInt, integerColorSupport, SampleCounts(GraphicsFormat.RGBA8SInt)),
            new(GraphicsFormat.RGBA8UNormSrgb,
                GraphicsFormatSupport.Sampled | GraphicsFormatSupport.ColorAttachment |
                GraphicsFormatSupport.Blend | GraphicsFormatSupport.CopySource |
                GraphicsFormatSupport.CopyDestination | GraphicsFormatSupport.LinearFiltering,
                SampleCounts(GraphicsFormat.RGBA8UNormSrgb)),
            new(GraphicsFormat.R8SNorm, signedNormalizedSupport, SampleCounts(GraphicsFormat.R8SNorm)),
            new(GraphicsFormat.RG8SNorm, signedNormalizedSupport, SampleCounts(GraphicsFormat.RG8SNorm)),
            new(GraphicsFormat.RGBA8SNorm, signedNormalizedSupport, SampleCounts(GraphicsFormat.RGBA8SNorm)),
            new(GraphicsFormat.R16Float, halfFloatSampledSupport, SampleCounts(GraphicsFormat.R16Float)),
            new(GraphicsFormat.RG16Float, halfFloatSampledSupport, SampleCounts(GraphicsFormat.RG16Float)),
            new(GraphicsFormat.RGBA16Float, halfFloatStorageSupport, SampleCounts(GraphicsFormat.RGBA16Float)),
            new(GraphicsFormat.R32Float, floatStorageSupport, SampleCounts(GraphicsFormat.R32Float)),
            new(GraphicsFormat.RG32Float, floatStorageSupport, SampleCounts(GraphicsFormat.RG32Float)),
            new(GraphicsFormat.RGB32Float, floatingSampledSupport, SampleCounts(GraphicsFormat.RGB32Float)),
            new(GraphicsFormat.RGBA32Float, fullFloatStorageSupport, SampleCounts(GraphicsFormat.RGBA32Float)),
            new(GraphicsFormat.R16UInt, integerStorageSupport, SampleCounts(GraphicsFormat.R16UInt)),
            new(GraphicsFormat.R16SInt, integerStorageSupport, SampleCounts(GraphicsFormat.R16SInt)),
            new(GraphicsFormat.RG16UInt, integerStorageSupport, SampleCounts(GraphicsFormat.RG16UInt)),
            new(GraphicsFormat.RG16SInt, integerStorageSupport, SampleCounts(GraphicsFormat.RG16SInt)),
            new(GraphicsFormat.RGBA16UInt, integerStorageSupport, SampleCounts(GraphicsFormat.RGBA16UInt)),
            new(GraphicsFormat.RGBA16SInt, integerStorageSupport, SampleCounts(GraphicsFormat.RGBA16SInt)),
            new(GraphicsFormat.R32UInt, integerStorageSupport, SampleCounts(GraphicsFormat.R32UInt)),
            new(GraphicsFormat.R32SInt, integerStorageSupport, SampleCounts(GraphicsFormat.R32SInt)),
            new(GraphicsFormat.RG32UInt, integerStorageSupport, SampleCounts(GraphicsFormat.RG32UInt)),
            new(GraphicsFormat.RG32SInt, integerStorageSupport, SampleCounts(GraphicsFormat.RG32SInt)),
            new(GraphicsFormat.RGBA32UInt, integerStorageSupport, SampleCounts(GraphicsFormat.RGBA32UInt)),
            new(GraphicsFormat.RGBA32SInt, integerStorageSupport, SampleCounts(GraphicsFormat.RGBA32SInt)),
            new(GraphicsFormat.D16UNorm, depthSupport, SampleCounts(GraphicsFormat.D16UNorm)),
            new(GraphicsFormat.D24UNormS8UInt, depthSupport, SampleCounts(GraphicsFormat.D24UNormS8UInt))
        ];

        // DEPTH_COMPONENT32F and DEPTH32F_STENCIL8 are required sized depth formats in
        // GLES 3.2. Do not expose their tokens or query them on GLES 3.1 contexts.
        if (profile.Version.IsAtLeast(3, 2))
        {
            formats.Add(new(GraphicsFormat.D32Float, depthSupport, SampleCounts(GraphicsFormat.D32Float)));
            formats.Add(new(GraphicsFormat.D32FloatS8UInt, depthSupport, SampleCounts(GraphicsFormat.D32FloatS8UInt)));
        }

        GraphicsFormatSupport compressedSupport = GraphicsFormatSupport.Sampled | GraphicsFormatSupport.LinearFiltering;
        if (supportsImageCopy)
            compressedSupport |= GraphicsFormatSupport.CopySource | GraphicsFormatSupport.CopyDestination;

        if (profile.HasExtension("GL_EXT_texture_compression_s3tc"))
        {
            formats.Add(new(GraphicsFormat.BC1UNorm, compressedSupport, GraphicsSampleCountFlags.One));
            formats.Add(new(GraphicsFormat.BC3UNorm, compressedSupport, GraphicsSampleCountFlags.One));
        }
        if (profile.HasExtension("GL_EXT_texture_compression_s3tc_srgb"))
        {
            formats.Add(new(GraphicsFormat.BC1UNormSrgb, compressedSupport, GraphicsSampleCountFlags.One));
            formats.Add(new(GraphicsFormat.BC3UNormSrgb, compressedSupport, GraphicsSampleCountFlags.One));
        }
        if (profile.HasExtension("GL_EXT_texture_compression_rgtc"))
            formats.Add(new(GraphicsFormat.BC5UNorm, compressedSupport, GraphicsSampleCountFlags.One));
        if (profile.HasExtension("GL_EXT_texture_compression_bptc"))
        {
            formats.Add(new(GraphicsFormat.BC7UNorm, compressedSupport, GraphicsSampleCountFlags.One));
            formats.Add(new(GraphicsFormat.BC7UNormSrgb, compressedSupport, GraphicsSampleCountFlags.One));
        }
        if (profile.HasExtension("GL_KHR_texture_compression_astc_ldr") ||
            profile.HasExtension("GL_OES_texture_compression_astc"))
        {
            formats.Add(new(GraphicsFormat.ASTC4x4UNorm, compressedSupport, GraphicsSampleCountFlags.One));
            formats.Add(new(GraphicsFormat.ASTC4x4UNormSrgb, compressedSupport, GraphicsSampleCountFlags.One));
        }

        return new GraphicsDeviceCapabilities(
            profile.Features,
            profile.QueueCapabilities,
            limits,
            formats.ToArray());
    }

    private static GraphicsSampleCountFlags QueryTextureSampleCounts(GL gl, GraphicsFormat format)
    {
        SizedInternalFormat internalFormat = OpenGLESGraphicsTexture.GetInternalFormat(format);
        int count = gl.GetInternalformat(
            TextureTarget.Texture2DMultisample,
            (GLEnum)internalFormat,
            InternalFormatPName.NumSampleCounts);
        if (count <= 0)
            return GraphicsSampleCountFlags.One;

        Span<int> nativeCounts = count <= 64 ? stackalloc int[count] : new int[count];
        gl.GetInternalformat(
            TextureTarget.Texture2DMultisample,
            (GLEnum)internalFormat,
            InternalFormatPName.Samples,
            nativeCounts);

        GraphicsSampleCountFlags supported = GraphicsSampleCountFlags.One;
        for (int i = 0; i < nativeCounts.Length; i++)
        {
            supported |= nativeCounts[i] switch
            {
                2 => GraphicsSampleCountFlags.Two,
                4 => GraphicsSampleCountFlags.Four,
                8 => GraphicsSampleCountFlags.Eight,
                16 => GraphicsSampleCountFlags.Sixteen,
                32 => GraphicsSampleCountFlags.ThirtyTwo,
                64 => GraphicsSampleCountFlags.SixtyFour,
                _ => GraphicsSampleCountFlags.None
            };
        }

        return supported;
    }

    private static bool HasEntryPoints(IOpenGLESContextProvider contextProvider, params string[] entryPoints)
    {
        for (int i = 0; i < entryPoints.Length; i++)
        {
            if (contextProvider.GetProcAddress(entryPoints[i]) == 0)
                return false;
        }

        return true;
    }

    private static uint ToUInt(int value) => value <= 0 ? 0u : (uint)value;

    internal readonly record struct OpenGLESQueriedLimits(
        uint MaxTextureSize,
        uint MaxTexture3DSize,
        uint MaxArrayLayers,
        uint MaxVertexAttributes,
        uint MaxUniformBufferBindings,
        uint MaxStorageBufferBindings,
        uint MaxTextureUnits,
        uint MaxImageUnits,
        uint UniformBufferOffsetAlignment,
        uint StorageBufferOffsetAlignment,
        uint MaxComputeInvocations,
        uint MaxComputeWorkGroupSizeX,
        uint MaxComputeWorkGroupSizeY,
        uint MaxComputeWorkGroupSizeZ,
        uint MaxComputeWorkGroupCountX,
        uint MaxComputeWorkGroupCountY,
        uint MaxComputeWorkGroupCountZ,
        uint MaxTessellationPatchSize,
        float MaxAnisotropy,
        uint MaxColorAttachments,
        uint MaxColorDrawBuffers,
        GraphicsSampleCountFlags[]? FormatSampleCounts = null,
        uint MaxMeshWorkGroupCountX = 0,
        uint MaxMeshWorkGroupCountY = 0,
        uint MaxMeshWorkGroupCountZ = 0,
        uint MaxMeshWorkGroupTotalCount = 0,
        uint MaxTaskWorkGroupCountX = 0,
        uint MaxTaskWorkGroupCountY = 0,
        uint MaxTaskWorkGroupCountZ = 0,
        uint MaxTaskWorkGroupTotalCount = 0);

    public IGraphicsCommandQueue GetQueue(GraphicsQueueType queueType, uint index = 0)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        if (!Enum.IsDefined(queueType))
            throw new ArgumentOutOfRangeException(nameof(queueType));
        if (index != 0)
            throw new GraphicsValidationException("The OpenGL ES backend exposes one logical queue per role.");
        if (queueType == GraphicsQueueType.Compute &&
            !Capabilities.QueueCapabilities.HasFlag(GraphicsQueueCapabilities.Compute))
            throw new GraphicsFeatureNotSupportedException(
                GraphicsFeature.ComputeShaders, GraphicsFeature.ComputeShaders);

        lock (_queueGate)
            return _queues[(int)queueType] ??= new OpenGLESGraphicsCommandQueue(this, queueType);
    }

    public IGraphicsQueue GetPresentationQueue(uint index = 0)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        if (index != 0)
            throw new GraphicsValidationException("The OpenGL ES backend exposes one presentation queue candidate.");
        if (!_contextProvider.SupportsPresentation)
            throw new GraphicsBackendUnavailableException(
                Backend, "The host-provided GLES context is not associated with a presentation drawable.");
        return GetQueue(GraphicsQueueType.Graphics);
    }

    public IGraphicsBuffer CreateBuffer(in GraphicsBufferDescription description)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        GraphicsBufferDescription copy = description;
        Capabilities.ValidateBufferDescription(copy);
        return WithContext(gl => OpenGLESGraphicsBuffer.CreateUnderContext(this, gl, copy));
    }

    public IGraphicsTexture CreateTexture(in GraphicsTextureDescription description)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        GraphicsTextureDescription copy = description;
        Capabilities.ValidateTextureDescription(copy);
        return WithContext(gl => OpenGLESGraphicsTexture.CreateUnderContext(this, gl, copy));
    }

    public IGraphicsSampler CreateSampler(in GraphicsSamplerDescription description)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        GraphicsSamplerDescription copy = description;
        Capabilities.ValidateSamplerDescription(copy);
        return WithContext(gl => OpenGLESGraphicsSampler.CreateUnderContext(this, gl, copy));
    }

    public IGraphicsShader CreateShader(in GraphicsShaderDescription description)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        GraphicsShaderDescription copy = description;
        return WithContext(gl => OpenGLESGraphicsShader.CreateUnderContext(this, gl, copy));
    }

    public IGraphicsBindingLayout CreateBindingLayout(GraphicsBindingLayoutDescription description)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        return new OpenGLESGraphicsBindingLayout(this, description);
    }

    public IGraphicsPipelineLayout CreatePipelineLayout(ReadOnlySpan<IGraphicsBindingLayout> bindingLayouts)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        return new OpenGLESGraphicsPipelineLayout(this, bindingLayouts);
    }

    public IGraphicsBindingSet CreateBindingSet(IGraphicsBindingLayout bindingLayout)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        ArgumentNullException.ThrowIfNull(bindingLayout);
        if (bindingLayout is not OpenGLESGraphicsBindingLayout glesLayout ||
            !ReferenceEquals(glesLayout.Device, this) || glesLayout.IsDisposed)
            throw new GraphicsValidationException("The binding layout must belong to this OpenGL ES device.");
        return new OpenGLESGraphicsBindingSet(glesLayout);
    }

    public IGraphicsRenderPipeline CreateRenderPipeline(GraphicsRenderPipelineDescription description)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        return OpenGLESGraphicsRenderPipeline.Create(this, description);
    }

    public IGraphicsComputePipeline CreateComputePipeline(GraphicsComputePipelineDescription description)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        return OpenGLESGraphicsComputePipeline.Create(this, description);
    }

    public IGraphicsFence CreateFence(ulong initialValue = 0)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        return WithContext(gl =>
        {
            OpenGLESGraphicsFence fence = new(this, initialValue);
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
                Backend, "The host-provided GLES context is not associated with a presentation drawable.");

        GraphicsPresentationTargetSnapshot snapshot = target.GetSnapshot();
        snapshot.Validate();
        if (!_contextProvider.IsPresentationTargetCompatible(snapshot))
            throw new GraphicsValidationException(
                "The supplied presentation target is not compatible with the host-provided GLES drawable.");
        return new OpenGLESGraphicsPresentationSurface(this, target, snapshot);
    }

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

    internal void RegisterUnderContext(IOpenGLESNativeResource resource)
    {
        lock (_resourceGate)
        {
            if (!_resources.Add(resource))
                throw new InvalidOperationException("The GLES native resource was registered more than once.");
        }
    }

    internal void ReleaseResource(IOpenGLESNativeResource resource)
    {
        lock (_contextGate)
        {
            if (IsDisposed)
                return;

            _contextProvider.MakeCurrent();
            try
            {
                resource.ReleaseNative(_gl);
                lock (_resourceGate)
                    _resources.Remove(resource);
            }
            finally
            {
                _contextProvider.ReleaseCurrent();
            }
        }
    }

    internal IOpenGLESContextProvider ContextProvider => _contextProvider;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        List<Exception>? failures = null;
        static void RecordFailure(ref List<Exception>? destination, Exception exception)
        {
            destination ??= new List<Exception>(4);
            destination.Add(exception);
        }

        lock (_contextGate)
        {
            bool contextCurrent = false;
            bool gpuIdle = false;
            try
            {
                _contextProvider.MakeCurrent();
                contextCurrent = true;

                try
                {
                    _gl.Finish();
                    gpuIdle = true;
                }
                catch (Exception exception)
                {
                    RecordFailure(ref failures, exception);
                }

                // Attempt every queue even when an individual queue reports a cleanup error.
                for (int i = 0; i < _queues.Length; i++)
                {
                    try
                    {
                        _queues[i]?.DrainUnderContext(_gl);
                    }
                    catch (Exception exception)
                    {
                        RecordFailure(ref failures, exception);
                    }
                }

                IOpenGLESNativeResource[] resources;
                lock (_resourceGate)
                {
                    resources = new IOpenGLESNativeResource[_resources.Count];
                    _resources.CopyTo(resources);
                }

                // If Finish failed, native resources may still be referenced by the GPU. Leave them
                // for host-context destruction rather than deleting them while work might be active.
                if (gpuIdle)
                {
                    for (int i = 0; i < resources.Length; i++)
                    {
                        try
                        {
                            resources[i].ReleaseNative(_gl);
                        }
                        catch (Exception exception)
                        {
                            RecordFailure(ref failures, exception);
                        }
                    }

                    lock (_resourceGate)
                        _resources.Clear();
                }
            }
            catch (Exception exception)
            {
                RecordFailure(ref failures, exception);
            }
            finally
            {
                try
                {
                    _baseInstanceApi?.Dispose();
                }
                catch (Exception exception)
                {
                    RecordFailure(ref failures, exception);
                }

                try
                {
                    _drawElementsBaseVertexApi?.Dispose();
                }
                catch (Exception exception)
                {
                    RecordFailure(ref failures, exception);
                }

                try
                {
                    _copyImageApi?.Dispose();
                }
                catch (Exception exception)
                {
                    RecordFailure(ref failures, exception);
                }

                try
                {
                    _drawBuffersIndexedApi?.Dispose();
                }
                catch (Exception exception)
                {
                    RecordFailure(ref failures, exception);
                }

                if (contextCurrent)
                {
                    try
                    {
                        _contextProvider.ReleaseCurrent();
                    }
                    catch (Exception exception)
                    {
                        RecordFailure(ref failures, exception);
                    }
                }

                try
                {
                    _gl.Dispose();
                }
                catch (Exception exception)
                {
                    RecordFailure(ref failures, exception);
                }
            }
        }

        if (_ownsContextProvider)
        {
            try
            {
                _contextProvider.Dispose();
            }
            catch (Exception exception)
            {
                RecordFailure(ref failures, exception);
            }
        }

        if (failures is { Count: > 0 })
            throw new AggregateException(
                "OpenGL ES device shutdown encountered one or more failures. Native resources are left for context destruction when GPU idleness could not be established.",
                failures);
    }
}
