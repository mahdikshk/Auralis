using Auralis.Graphics.Abstractions;
using Auralis.Graphics.Backends.OpenGLES;
using Auralis.Graphics.Devices;
using Auralis.Graphics.Presentation;
using Auralis.Graphics.Resources;
using Silk.NET.OpenGLES;
using Xunit;

namespace Auralis.Graphics.Tests;

public sealed class OpenGLESCapabilityProfileTests
{
    [Theory]
    [InlineData("OpenGL ES 3.1 Mesa 24.0", 3, 1)]
    [InlineData("OpenGL ES 3.2 V@0502.0", 3, 2)]
    [InlineData("OpenGL ES-CM 1.1 Vendor", 1, 1)]
    [InlineData("OpenGL ES 3.10 Vendor", 3, 10)]
    public void TryParse_ParsesGlesVersionPrefix(string text, int major, int minor)
    {
        Assert.True(OpenGLESVersion.TryParse(text, out OpenGLESVersion parsed));
        Assert.Equal(new OpenGLESVersion(major, minor), parsed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("OpenGL 4.5 Core Profile")]
    [InlineData("OpenGL ES GLSL ES 3.20")]
    [InlineData("OpenGL ES version unknown")]
    public void TryParse_RejectsInvalidOrDesktopStrings(string? text)
    {
        Assert.False(OpenGLESVersion.TryParse(text, out _));
    }

    [Fact]
    public void Parse_ThrowsForDesktopOpenGlVersion()
    {
        Assert.Throws<FormatException>(() => OpenGLESVersion.Parse("4.5 Core Profile"));
    }

    [Fact]
    public void Es31_ExposesOnlyCore31Capabilities()
    {
        OpenGLESCapabilityProfile profile = OpenGLESCapabilityProfile.Create(
            new OpenGLESVersion(3, 1),
            ReadOnlySpan<string>.Empty,
            supportsPresentation: false);

        Assert.True(profile.MeetsRequiredBaseline);
        Assert.True(profile.Features.Supports(GraphicsFeature.ComputeShaders));
        Assert.True(profile.Features.Supports(GraphicsFeature.IndirectDrawing));
        Assert.False(profile.Features.Supports(GraphicsFeature.GeometryShaders));
        Assert.False(profile.Features.Supports(GraphicsFeature.TessellationShaders));
        Assert.False(profile.Features.Supports(GraphicsFeature.MeshShaders));
        Assert.False(profile.Features.Supports(GraphicsFeature.TaskShaders));
        Assert.False(profile.Features.Supports(GraphicsFeature.SpirVShaders));
        Assert.Equal(
            GraphicsQueueCapabilities.Graphics | GraphicsQueueCapabilities.Compute | GraphicsQueueCapabilities.Copy,
            profile.QueueCapabilities);
    }

    [Fact]
    public void Es32_EnablesGeometryAndTessellationCoreFeatures()
    {
        OpenGLESCapabilityProfile profile = OpenGLESCapabilityProfile.Create(
            new OpenGLESVersion(3, 2),
            ReadOnlySpan<string>.Empty,
            supportsPresentation: true);

        Assert.True(profile.Features.Supports(GraphicsFeature.GeometryShaders));
        Assert.True(profile.Features.Supports(GraphicsFeature.TessellationShaders));
        Assert.True(profile.QueueCapabilities.HasFlag(GraphicsQueueCapabilities.Present));
    }

    [Fact]
    public void ExtensionFeatures_RequireExactExtensionNames()
    {
        string[] extensions =
        [
            "GL_EXT_texture_filter_anisotropic",
            "GL_EXT_draw_buffers_indexed",
            "GL_EXT_multi_draw_indirect",
            "GL_EXT_draw_elements_base_vertex",
            "GL_EXT_base_instance",
            "GL_EXT_texture_view"
        ];

        OpenGLESCapabilityProfile profile = OpenGLESCapabilityProfile.Create(
            new OpenGLESVersion(3, 1),
            extensions,
            supportsPresentation: false);

        Assert.True(profile.HasExtension("GL_EXT_texture_filter_anisotropic"));
        Assert.False(profile.HasExtension("GL_EXT_texture_filter"));
        Assert.True(profile.Features.Supports(GraphicsFeature.SamplerAnisotropy));
        Assert.True(profile.Features.Supports(GraphicsFeature.IndependentBlend));
        Assert.True(profile.Features.Supports(GraphicsFeature.MultiDrawIndirect));
        Assert.True(profile.Features.Supports(GraphicsFeature.BaseVertexDrawing));
        Assert.True(profile.Features.Supports(GraphicsFeature.BaseInstanceDrawing));
        Assert.True(profile.Features.Supports(GraphicsFeature.TextureViews));
        Assert.True(profile.Features.Supports(GraphicsFeature.TimelineSynchronization));
    }

    [Fact]
    public void OptionalFeaturesAreNotReportedWhenNativeEntryPointsAreMissing()
    {
        string[] extensions =
        [
            "GL_EXT_multi_draw_indirect",
            "GL_EXT_draw_buffers_indexed",
            "GL_EXT_draw_elements_base_vertex",
            "GL_EXT_base_instance",
            "GL_EXT_texture_view"
        ];

        OpenGLESCapabilityProfile profile = OpenGLESCapabilityProfile.Create(
            new OpenGLESVersion(3, 1),
            extensions,
            supportsPresentation: false,
            multiDrawIndirectEntryPointsAvailable: false,
            indexedBlendEntryPointsAvailable: false,
            baseVertexEntryPointsAvailable: false,
            baseInstanceEntryPointsAvailable: false,
            textureViewEntryPointAvailable: false);

        Assert.False(profile.Features.Supports(GraphicsFeature.MultiDrawIndirect));
        Assert.False(profile.Features.Supports(GraphicsFeature.IndependentBlend));
        Assert.False(profile.Features.Supports(GraphicsFeature.BaseVertexDrawing));
        Assert.False(profile.Features.Supports(GraphicsFeature.BaseInstanceDrawing));
        Assert.False(profile.Features.Supports(GraphicsFeature.TextureViews));
    }

    [Fact]
    public void FormatCapabilitiesNeverAdvertiseAnUnmappedOrExtensionUnavailableFormat()
    {
        OpenGLESCapabilityProfile baselineProfile = OpenGLESCapabilityProfile.Create(
            new OpenGLESVersion(3, 1),
            ReadOnlySpan<string>.Empty,
            supportsPresentation: false);
        GraphicsDeviceCapabilities baseline = OpenGLESGraphicsDevice.QueryCapabilities(
            baselineProfile,
            default);

        foreach (GraphicsFormatCapabilities format in baseline.Formats)
        {
            Assert.NotEqual(GraphicsFormat.Unknown, format.Format);
            _ = OpenGLESGraphicsTexture.GetInternalFormat(format.Format);
            Assert.True(format.SupportsSampleCount(1));
        }

        Assert.False(baseline.TryGetFormatCapabilities(GraphicsFormat.BGRA8UNorm, out _));
        Assert.False(baseline.TryGetFormatCapabilities(GraphicsFormat.BGRA8UNormSrgb, out _));
        Assert.False(baseline.TryGetFormatCapabilities(GraphicsFormat.D32Float, out _));
        Assert.False(baseline.TryGetFormatCapabilities(GraphicsFormat.D32FloatS8UInt, out _));
        Assert.False(baseline.TryGetFormatCapabilities(GraphicsFormat.BC1UNorm, out _));
        Assert.False(baseline.TryGetFormatCapabilities(GraphicsFormat.ASTC4x4UNorm, out _));

        Assert.Throws<GraphicsValidationException>(() =>
            OpenGLESGraphicsTexture.GetInternalFormat(GraphicsFormat.BGRA8UNorm));
        Assert.Throws<GraphicsValidationException>(() =>
            OpenGLESGraphicsTexture.GetInternalFormat(GraphicsFormat.BGRA8UNormSrgb));
    }

    [Fact]
    public void FormatCapabilitiesExposeExtensionBackedFormatsOnlyWhenTheirRequirementsExist()
    {
        string[] extensions =
        [
            "GL_EXT_color_buffer_half_float",
            "GL_EXT_color_buffer_float",
            "GL_OES_texture_float_linear",
            "GL_EXT_texture_compression_s3tc",
            "GL_EXT_texture_compression_s3tc_srgb",
            "GL_EXT_texture_compression_rgtc",
            "GL_EXT_texture_compression_bptc",
            "GL_KHR_texture_compression_astc_ldr"
        ];
        OpenGLESCapabilityProfile profile = OpenGLESCapabilityProfile.Create(
            new OpenGLESVersion(3, 2),
            extensions,
            supportsPresentation: false);
        GraphicsDeviceCapabilities capabilities = OpenGLESGraphicsDevice.QueryCapabilities(
            profile,
            default,
            supportsImageCopy: true);

        foreach (GraphicsFormatCapabilities format in capabilities.Formats)
        {
            Assert.NotEqual(GraphicsFormat.Unknown, format.Format);
            _ = OpenGLESGraphicsTexture.GetInternalFormat(format.Format);
            Assert.True(format.SupportsSampleCount(1));
        }

        Assert.True(capabilities.TryGetFormatCapabilities(GraphicsFormat.D32Float, out _));
        Assert.True(capabilities.TryGetFormatCapabilities(GraphicsFormat.D32FloatS8UInt, out _));
        Assert.True(capabilities.TryGetFormatCapabilities(GraphicsFormat.BC1UNorm, out _));
        Assert.True(capabilities.TryGetFormatCapabilities(GraphicsFormat.BC1UNormSrgb, out _));
        Assert.True(capabilities.TryGetFormatCapabilities(GraphicsFormat.BC5UNorm, out _));
        Assert.True(capabilities.TryGetFormatCapabilities(GraphicsFormat.BC7UNorm, out _));
        Assert.True(capabilities.TryGetFormatCapabilities(GraphicsFormat.BC7UNormSrgb, out _));
        Assert.True(capabilities.TryGetFormatCapabilities(GraphicsFormat.ASTC4x4UNorm, out _));
        Assert.True(capabilities.TryGetFormatCapabilities(GraphicsFormat.ASTC4x4UNormSrgb, out _));

        Assert.False(capabilities.TryGetFormatCapabilities(GraphicsFormat.BGRA8UNorm, out _));
        Assert.False(capabilities.TryGetFormatCapabilities(GraphicsFormat.BGRA8UNormSrgb, out _));
    }

    [Fact]
    public void ExtensionNames_AreValidatedAndDuplicatesAreRejected()
    {
        Assert.Throws<ArgumentException>(() => OpenGLESCapabilityProfile.Create(
            new OpenGLESVersion(3, 1),
            new[] { "GL_EXT_one", "GL_EXT_one" },
            supportsPresentation: false));

        Assert.Throws<ArgumentException>(() => OpenGLESCapabilityProfile.Create(
            new OpenGLESVersion(3, 1),
            new[] { "GL_EXT_one two" },
            supportsPresentation: false));
    }

    [Fact]
    public void Es30_DoesNotMeetBaseline_AndComputeQueueIsNotAdvertised()
    {
        OpenGLESCapabilityProfile profile = OpenGLESCapabilityProfile.Create(
            new OpenGLESVersion(3, 0),
            ReadOnlySpan<string>.Empty,
            supportsPresentation: false);

        Assert.False(profile.MeetsRequiredBaseline);
        Assert.Throws<NotSupportedException>(profile.EnsureRequiredBaseline);
        Assert.False(profile.QueueCapabilities.HasFlag(GraphicsQueueCapabilities.Compute));
    }

    [Fact]
    public void ExistingDeviceOptions_AcceptOpenGlesBackendSelection()
    {
        GraphicsDeviceOptions options = new()
        {
            Backend = GraphicsBackend.OpenGLES,
            RequiredQueues = GraphicsQueueCapabilities.Graphics | GraphicsQueueCapabilities.Compute
        };

        options.Validate();
        Assert.Equal(GraphicsBackend.OpenGLES, options.Backend);
    }

    [Fact]
    public void BackendIdentifier_IsPartOfExistingGraphicsBackendEnum()
    {
        Assert.True(Enum.IsDefined(GraphicsBackend.OpenGLES));
    }
}

public sealed class OpenGLESResourceReferenceStateTests
{
    [Fact]
    public void OwnerClose_PreventsNewRetains_ButAllowsExistingDependentsToRetain()
    {
        OpenGLESResourceReferenceState state = new();

        Assert.True(state.TryRetain()); // command or texture-view owner
        Assert.False(state.CloseOwner());
        Assert.True(state.IsClosed);
        Assert.False(state.TryRetain());
        Assert.True(state.TryRetainDependency());
        Assert.Equal(2, state.ReferenceCount);

        Assert.False(state.ReleaseReference());
        Assert.True(state.ReleaseReference());
        Assert.Equal(0, state.ReferenceCount);
    }

    [Fact]
    public void CloseOwner_IsIdempotent_AndReleasesOnlyTheOwnerReference()
    {
        OpenGLESResourceReferenceState state = new();

        Assert.True(state.CloseOwner());
        Assert.False(state.CloseOwner());
        Assert.Equal(0, state.ReferenceCount);
        Assert.False(state.TryRetainDependency());
        Assert.Throws<InvalidOperationException>(() => state.ReleaseReference());
    }

    [Fact]
    public void ConcurrentCommandRetains_ReleaseExactlyOneFinalReference()
    {
        OpenGLESResourceReferenceState state = new();
        const int workers = 16;
        const int iterations = 2_000;
        int finalReleases = 0;

        Parallel.For(0, workers, _ =>
        {
            for (int i = 0; i < iterations; i++)
            {
                Assert.True(state.TryRetain());
                if (state.ReleaseReference())
                    Interlocked.Increment(ref finalReleases);
            }
        });

        Assert.Equal(1, state.ReferenceCount);
        Assert.Equal(0, finalReleases);
        Assert.True(state.CloseOwner());
        Assert.Equal(0, state.ReferenceCount);
    }
}

public sealed class OpenGLESBackendProviderTests
{
    [Fact]
    public void ProviderRegistersInTheExistingBackendRegistry()
    {
        FakeOpenGLESContextProvider context = new();
        OpenGLESGraphicsBackendProvider provider = new(context);
        GraphicsBackendProviderRegistry registry = new();

        registry.Register(provider);

        Assert.Equal(1, registry.Count);
        Assert.Same(provider, registry.GetProvider(GraphicsBackend.OpenGLES));
    }

    [Fact]
    public async Task ProviderExposesItsHostSelectedAdapterToTheFactory()
    {
        FakeOpenGLESContextProvider context = new();
        OpenGLESGraphicsBackendProvider provider = new(context);

        ReadOnlyMemory<GraphicsAdapterInfo> adapters = await provider.EnumerateAdaptersAsync();

        Assert.Single(adapters.ToArray());
        Assert.True(adapters.Span[0].Id.IsDefault);
        Assert.Equal("OpenGL ES host context", adapters.Span[0].Name);
    }

    [Fact]
    public async Task FactoryRejectsAnUnavailableGlesProvider()
    {
        FakeOpenGLESContextProvider context = new() { Available = false };
        GraphicsBackendProviderRegistry registry = new();
        registry.Register(new OpenGLESGraphicsBackendProvider(context));

        await Assert.ThrowsAsync<GraphicsBackendUnavailableException>(async () =>
            await GraphicsDeviceFactory.CreateAsync(
                registry,
                new GraphicsDeviceOptions { Backend = GraphicsBackend.OpenGLES }));
    }

    [Fact]
    public async Task OwnedContextProviderIsDisposedWhenDeviceInitializationFails()
    {
        FakeOpenGLESContextProvider context = new()
        {
            Ownership = OpenGLESContextOwnership.Owned,
            ThrowFromMakeCurrent = true
        };
        OpenGLESGraphicsBackendProvider provider = new(context);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await provider.CreateDeviceAsync(new GraphicsDeviceOptions
            {
                Backend = GraphicsBackend.OpenGLES,
                RequiredQueues = GraphicsQueueCapabilities.Graphics
            }));

        Assert.True(context.IsDisposed);
        Assert.Equal(0, context.ReleaseCurrentCount);
    }

    private sealed class FakeOpenGLESContextProvider : IOpenGLESContextProvider
    {
        public object ContextIdentity => this;
        public OpenGLESContextOwnership Ownership { get; set; } = OpenGLESContextOwnership.Borrowed;
        public OpenGLESContextStateOwnership StateOwnership => OpenGLESContextStateOwnership.Exclusive;
        public bool Available { get; set; } = true;
        public bool IsAvailable => Available;
        public string? UnavailableReason => IsAvailable ? null : "Disabled in test.";
        public bool SupportsPresentation => false;
        public bool ThrowFromMakeCurrent { get; set; }
        public bool IsDisposed { get; private set; }
        public int ReleaseCurrentCount { get; private set; }

        public bool IsPresentationTargetCompatible(in GraphicsPresentationTargetSnapshot target) => false;
        public bool SupportsPresentMode(GraphicsPresentMode presentMode) => false;
        public void SetPresentMode(GraphicsPresentMode presentMode) => throw new NotSupportedException();
        public void MakeCurrent()
        {
            if (ThrowFromMakeCurrent)
                throw new InvalidOperationException("Simulated GLES context activation failure.");
        }
        public void ReleaseCurrent() => ReleaseCurrentCount++;
        public nint GetProcAddress(string name) => 0;
        public void SwapBuffers() => throw new NotSupportedException();
        public void Dispose() => IsDisposed = true;
    }
}

public sealed class OpenGLESShaderCompatibilityTests
{
    [Fact]
    public void Es31_AllowsVertexFragmentAndComputeStages()
    {
        OpenGLESVersion version = new(3, 1);

        Assert.Equal(Silk.NET.OpenGLES.ShaderType.VertexShader,
            OpenGLESShaderCompatibility.GetNativeStage(version, GraphicsShaderStage.Vertex));
        Assert.Equal(Silk.NET.OpenGLES.ShaderType.FragmentShader,
            OpenGLESShaderCompatibility.GetNativeStage(version, GraphicsShaderStage.Fragment));
        Assert.Equal(Silk.NET.OpenGLES.ShaderType.ComputeShader,
            OpenGLESShaderCompatibility.GetNativeStage(version, GraphicsShaderStage.Compute));
        Assert.Throws<GraphicsFeatureNotSupportedException>(() =>
            OpenGLESShaderCompatibility.GetNativeStage(version, GraphicsShaderStage.Geometry));
        Assert.Throws<GraphicsFeatureNotSupportedException>(() =>
            OpenGLESShaderCompatibility.GetNativeStage(version, GraphicsShaderStage.TessellationControl));
    }

    [Fact]
    public void Es32_AddsGeometryAndTessellationShaderStages()
    {
        OpenGLESVersion version = new(3, 2);

        Assert.Equal(Silk.NET.OpenGLES.ShaderType.GeometryShader,
            OpenGLESShaderCompatibility.GetNativeStage(version, GraphicsShaderStage.Geometry));
        Assert.Equal(Silk.NET.OpenGLES.ShaderType.TessControlShader,
            OpenGLESShaderCompatibility.GetNativeStage(version, GraphicsShaderStage.TessellationControl));
        Assert.Equal(Silk.NET.OpenGLES.ShaderType.TessEvaluationShader,
            OpenGLESShaderCompatibility.GetNativeStage(version, GraphicsShaderStage.TessellationEvaluation));
    }

    [Theory]
    [InlineData(GraphicsShaderStage.Task)]
    [InlineData(GraphicsShaderStage.Mesh)]
    public void MeshAndTaskStagesAreNeverClaimedAsGlesCore(GraphicsShaderStage stage)
    {
        Assert.Throws<GraphicsFeatureNotSupportedException>(() =>
            OpenGLESShaderCompatibility.GetNativeStage(new OpenGLESVersion(3, 2), stage));
    }

    [Fact]
    public void ValidatesGlslEsLanguageAndContextCompatibility()
    {
        OpenGLESShaderCompatibility.ValidateSourceVersion(
            new OpenGLESVersion(3, 1),
            GraphicsShaderStage.Vertex,
            "#version 300 es\nvoid main(){}");
        OpenGLESShaderCompatibility.ValidateSourceVersion(
            new OpenGLESVersion(3, 1),
            GraphicsShaderStage.Compute,
            "#version 310 es\nlayout(local_size_x=1) in; void main(){}");
        OpenGLESShaderCompatibility.ValidateSourceVersion(
            new OpenGLESVersion(3, 2),
            GraphicsShaderStage.Geometry,
            "#version 320 es\nlayout(points) in; layout(points, max_vertices=1) out; void main(){}");
    }

    [Theory]
    [InlineData("#version 450 core\nvoid main(){}")]
    [InlineData("#version 100 es\nvoid main(){}")]
    [InlineData("void main(){}")]
    public void RejectsDesktopLegacyOrMissingVersionDirectives(string source)
    {
        Assert.Throws<GraphicsValidationException>(() =>
            OpenGLESShaderCompatibility.ValidateSourceVersion(
                new OpenGLESVersion(3, 2), GraphicsShaderStage.Vertex, source));
    }

    [Fact]
    public void RejectsComputeAndGeometryStagesWithOlderGlslEsVersions()
    {
        Assert.Throws<GraphicsValidationException>(() =>
            OpenGLESShaderCompatibility.ValidateSourceVersion(
                new OpenGLESVersion(3, 1), GraphicsShaderStage.Compute, "#version 300 es\nvoid main(){}"));
        Assert.Throws<GraphicsValidationException>(() =>
            OpenGLESShaderCompatibility.ValidateSourceVersion(
                new OpenGLESVersion(3, 2), GraphicsShaderStage.Geometry, "#version 310 es\nvoid main(){}"));
    }

    [Fact]
    public void RejectsGlslEsVersionHigherThanContext()
    {
        Assert.Throws<GraphicsValidationException>(() =>
            OpenGLESShaderCompatibility.ValidateSourceVersion(
                new OpenGLESVersion(3, 1), GraphicsShaderStage.Vertex, "#version 320 es\nvoid main(){}"));
    }
}

public sealed class OpenGLESTextureCompatibilityTests
{
    [Theory]
    [InlineData(GraphicsFormat.R8UNorm, Silk.NET.OpenGLES.SizedInternalFormat.R8)]
    [InlineData(GraphicsFormat.RG8UNorm, Silk.NET.OpenGLES.SizedInternalFormat.RG8)]
    [InlineData(GraphicsFormat.RGBA8UNorm, Silk.NET.OpenGLES.SizedInternalFormat.Rgba8)]
    [InlineData(GraphicsFormat.RGBA8UNormSrgb, Silk.NET.OpenGLES.SizedInternalFormat.Srgb8Alpha8)]
    public void SupportedFormatsHaveAnExplicitGlesInternalFormat(
        GraphicsFormat format,
        Silk.NET.OpenGLES.SizedInternalFormat expected)
    {
        Assert.Equal(expected, OpenGLESGraphicsTexture.GetInternalFormat(format));
    }

    [Fact]
    public void UnsupportedTextureFormatIsRejected()
    {
        Assert.Throws<GraphicsValidationException>(() =>
            OpenGLESGraphicsTexture.GetInternalFormat(GraphicsFormat.Unknown));
        Assert.Throws<GraphicsValidationException>(() =>
            OpenGLESGraphicsTexture.GetInternalFormat(GraphicsFormat.BGRA8UNorm));
    }

    [Fact]
    public void ValidGlesTextureDescriptionMapsToExpectedTarget()
    {
        GraphicsTextureDescription description = new(
            GraphicsTextureDimension.Texture2D,
            GraphicsFormat.RGBA8UNorm,
            128, 64, 1, 8, 1, 1,
            GraphicsTextureUsage.Sampled | GraphicsTextureUsage.Storage);

        Assert.Equal(
            Silk.NET.OpenGLES.TextureTarget.Texture2D,
            OpenGLESTextureCompatibility.ValidateAndGetTarget(description));
    }

    [Fact]
    public void TwoDimensionalArrayUsesGlesArrayTarget()
    {
        GraphicsTextureDescription description = new(
            GraphicsTextureDimension.Texture2D,
            GraphicsFormat.RGBA8UNorm,
            128, 64, 1, 8, 4, 1,
            GraphicsTextureUsage.Sampled);

        Assert.Equal(
            Silk.NET.OpenGLES.TextureTarget.Texture2DArray,
            OpenGLESTextureCompatibility.ValidateAndGetTarget(description));
    }

    [Fact]
    public void CubeMapArrayRequiresEs32OrItsOptionalExtension()
    {
        GraphicsTextureDescription description = new(
            GraphicsTextureDimension.Cube,
            GraphicsFormat.RGBA8UNorm,
            64, 64, 1, 1, 12, 1,
            GraphicsTextureUsage.Sampled);

        Assert.Throws<GraphicsValidationException>(() =>
            OpenGLESTextureCompatibility.ValidateAndGetTarget(
                description, new OpenGLESVersion(3, 1)));
        Assert.Equal(
            Silk.NET.OpenGLES.TextureTarget.TextureCubeMapArray,
            OpenGLESTextureCompatibility.ValidateAndGetTarget(
                description, new OpenGLESVersion(3, 1), supportsCubeMapArray: true));
        Assert.Equal(
            Silk.NET.OpenGLES.TextureTarget.TextureCubeMapArray,
            OpenGLESTextureCompatibility.ValidateAndGetTarget(
                description, new OpenGLESVersion(3, 2)));
    }

    [Fact]
    public void Es31MultisampleTextureUsesTheNativeMultisampleTarget()
    {
        GraphicsTextureDescription description = new(
            GraphicsTextureDimension.Texture2D,
            GraphicsFormat.RGBA8UNorm,
            128, 64, 1, 1, 1, 4,
            GraphicsTextureUsage.Sampled | GraphicsTextureUsage.ColorAttachment);

        Assert.Equal(
            Silk.NET.OpenGLES.TextureTarget.Texture2DMultisample,
            OpenGLESTextureCompatibility.ValidateAndGetTarget(description, new OpenGLESVersion(3, 1)));
    }

    [Theory]
    [InlineData(GraphicsShaderStage.Mesh)]
    [InlineData(GraphicsShaderStage.Task)]
    public void MeshTaskShaderSourceRequiresGlesExtensionDirective(GraphicsShaderStage stage)
    {
        const string source = """
            #version 310 es
            #extension GL_EXT_mesh_shader : require
            layout(local_size_x = 1) in;
            void main() {}
            """;
        OpenGLESShaderCompatibility.ValidateSourceVersion(
            new OpenGLESVersion(3, 1), stage, source);
    }

    [Theory]
    [InlineData(GraphicsShaderStage.Mesh)]
    [InlineData(GraphicsShaderStage.Task)]
    public void MeshTaskShaderSourceRejectsMissingExtensionDirective(GraphicsShaderStage stage)
    {
        const string source = """
            #version 310 es
            layout(local_size_x = 1) in;
            void main() {}
            """;
        Assert.Throws<GraphicsValidationException>(() =>
            OpenGLESShaderCompatibility.ValidateSourceVersion(
                new OpenGLESVersion(3, 1), stage, source));
    }

    [Fact]
    public void MultisampleTextureRequiresEs31AndRejectsAliasing()
    {
        GraphicsTextureDescription description = new(
            GraphicsTextureDimension.Texture2D,
            GraphicsFormat.RGBA8UNorm,
            128, 64, 1, 1, 1, 4,
            GraphicsTextureUsage.Sampled);

        Assert.Throws<GraphicsValidationException>(() =>
            OpenGLESTextureCompatibility.ValidateAndGetTarget(description, new OpenGLESVersion(3, 0)));

        GraphicsTextureDescription aliased = description with
        {
            CreationOptions = GraphicsResourceCreationOptions.AllowAliasing
        };
        Assert.Throws<GraphicsValidationException>(() =>
            OpenGLESTextureCompatibility.ValidateAndGetTarget(aliased, new OpenGLESVersion(3, 1)));
    }

    [Fact]
    public void RejectsUnsupportedGlesTextureKindsWithoutAContext()
    {
        GraphicsTextureDescription oneDimensional = new(
            GraphicsTextureDimension.Texture1D,
            GraphicsFormat.RGBA8UNorm,
            128, 1, 1, 8, 1, 1,
            GraphicsTextureUsage.Sampled);
        GraphicsTextureDescription multisampled = new(
            GraphicsTextureDimension.Texture2D,
            GraphicsFormat.RGBA8UNorm,
            128, 64, 1, 1, 1, 4,
            GraphicsTextureUsage.Sampled);
        GraphicsTextureDescription cubeArray = new(
            GraphicsTextureDimension.Cube,
            GraphicsFormat.RGBA8UNorm,
            128, 128, 1, 8, 12, 1,
            GraphicsTextureUsage.Sampled);

        Assert.Throws<GraphicsValidationException>(() =>
            OpenGLESTextureCompatibility.ValidateAndGetTarget(oneDimensional));
        Assert.Throws<GraphicsValidationException>(() =>
            OpenGLESTextureCompatibility.ValidateAndGetTarget(multisampled));
        Assert.Throws<GraphicsValidationException>(() =>
            OpenGLESTextureCompatibility.ValidateAndGetTarget(cubeArray));
    }
}


public sealed class OpenGLESComputeWorkGroupValidationTests
{
    private static readonly GraphicsDeviceLimits s_limits = new(
        MaxBufferSizeInBytes: 0,
        MaxTextureDimension1D: 0,
        MaxTextureDimension2D: 0,
        MaxTextureDimension3D: 0,
        MaxTextureArrayLayers: 0,
        MaxVertexInputAttributes: 0,
        MaxVertexInputBindings: 0,
        MaxBoundDescriptorSets: 0,
        MaxComputeWorkGroupInvocations: 256,
        MaxComputeWorkGroupSizeX: 128,
        MaxComputeWorkGroupSizeY: 128,
        MaxComputeWorkGroupSizeZ: 64,
        MaxSamplerAnisotropy: 1);

    [Theory]
    [InlineData(0u, 1u, 1u)]
    [InlineData(1u, 0u, 1u)]
    [InlineData(1u, 1u, 0u)]
    public void Dispatch_ZeroCountOnAnyAxisRecordsNoGpuCommand(uint x, uint y, uint z)
    {
        // Exercise the same command factory used by OpenGLESGraphicsCommandList.Dispatch.
        Assert.False(OpenGLESGraphicsCommandList.TryCreateDispatchCommand(
            x, y, z, 0, 0, 0, out _));
    }

    [Theory]
    [InlineData(65_535u, 1u, 1u)]
    [InlineData(1u, 65_535u, 1u)]
    [InlineData(1u, 1u, 65_535u)]
    public void Dispatch_CountEqualToEachAxisMaximumIsAccepted(uint x, uint y, uint z)
    {
        Assert.True(OpenGLESGraphicsCommandList.TryCreateDispatchCommand(
            x, y, z, 65_535, 65_535, 65_535, out OpenGLESCmd command));
        Assert.Equal(OpenGLESCmdKind.Dispatch, command.Kind);
        Assert.Equal(x, command.GroupsX);
        Assert.Equal(y, command.GroupsY);
        Assert.Equal(z, command.GroupsZ);
    }

    [Theory]
    [InlineData(65_536u, 1u, 1u)]
    [InlineData(1u, 65_536u, 1u)]
    [InlineData(1u, 1u, 65_536u)]
    public void Dispatch_CountOneAboveEachAxisMaximumIsRejected(uint x, uint y, uint z)
    {
        Assert.Throws<GraphicsValidationException>(() =>
            OpenGLESGraphicsCommandList.TryCreateDispatchCommand(
                x, y, z, 65_535, 65_535, 65_535, out _));
    }

    [Theory]
    [InlineData(0u, 65_535u, 65_535u)]
    [InlineData(65_535u, 0u, 65_535u)]
    [InlineData(65_535u, 65_535u, 0u)]
    public void Dispatch_NonZeroCountsRequireEveryQueriedAxisLimit(uint maxX, uint maxY, uint maxZ)
    {
        Assert.Throws<GraphicsValidationException>(() =>
            OpenGLESGraphicsCommandList.TryCreateDispatchCommand(
                1, 1, 1, maxX, maxY, maxZ, out _));
    }


    [Fact]
    public void LocalSize_AllowsAxisAndInvocationValuesAtLimits() =>
        OpenGLESComputeWorkGroupValidation.ValidateLocalSize(16, 16, 1, s_limits);

    [Theory]
    [InlineData(129, 1, 1)]
    [InlineData(1, 129, 1)]
    [InlineData(1, 1, 65)]
    public void LocalSize_RejectsPerAxisLimitViolations(int x, int y, int z) =>
        Assert.Throws<GraphicsValidationException>(() =>
            OpenGLESComputeWorkGroupValidation.ValidateLocalSize(x, y, z, s_limits));

    [Fact]
    public void LocalSize_RejectsProductAboveInvocationLimit() =>
        Assert.Throws<GraphicsValidationException>(() =>
            OpenGLESComputeWorkGroupValidation.ValidateLocalSize(17, 16, 1, s_limits));

    [Theory]
    [InlineData(0, 1, 1)]
    [InlineData(1, -1, 1)]
    [InlineData(1, 1, 0)]
    public void LocalSize_RejectsNonPositiveDimensions(int x, int y, int z) =>
        Assert.Throws<GraphicsValidationException>(() =>
            OpenGLESComputeWorkGroupValidation.ValidateLocalSize(x, y, z, s_limits));
}

public sealed class OpenGLESFormatCapabilityTests
{
    private static GraphicsDeviceCapabilities CreateCapabilities(int major = 3, int minor = 1)
    {
        OpenGLESCapabilityProfile profile = OpenGLESCapabilityProfile.Create(
            new OpenGLESVersion(major, minor),
            ReadOnlySpan<string>.Empty,
            supportsPresentation: false);

        OpenGLESGraphicsDevice.OpenGLESQueriedLimits limits = new(
            MaxTextureSize: 4096,
            MaxTexture3DSize: 256,
            MaxArrayLayers: 256,
            MaxVertexAttributes: 16,
            MaxUniformBufferBindings: 24,
            MaxStorageBufferBindings: 8,
            MaxTextureUnits: 16,
            MaxImageUnits: major > 3 || (major == 3 && minor >= 1) ? 8u : 0u,
            UniformBufferOffsetAlignment: 256,
            StorageBufferOffsetAlignment: 16,
            MaxComputeInvocations: major > 3 || (major == 3 && minor >= 1) ? 256u : 0u,
            MaxComputeWorkGroupSizeX: major > 3 || (major == 3 && minor >= 1) ? 128u : 0u,
            MaxComputeWorkGroupSizeY: major > 3 || (major == 3 && minor >= 1) ? 128u : 0u,
            MaxComputeWorkGroupSizeZ: major > 3 || (major == 3 && minor >= 1) ? 64u : 0u,
            MaxComputeWorkGroupCountX: major > 3 || (major == 3 && minor >= 1) ? 65_535u : 0u,
            MaxComputeWorkGroupCountY: major > 3 || (major == 3 && minor >= 1) ? 65_535u : 0u,
            MaxComputeWorkGroupCountZ: major > 3 || (major == 3 && minor >= 1) ? 65_535u : 0u,
            MaxTessellationPatchSize: minor >= 2 ? 32u : 0u,
            MaxAnisotropy: 1,
            MaxColorAttachments: 4,
            MaxColorDrawBuffers: 4);

        return OpenGLESGraphicsDevice.QueryCapabilities(profile, limits);
    }

    [Theory]
    [InlineData(GraphicsFormat.R8UNorm)]
    [InlineData(GraphicsFormat.RG8UNorm)]
    [InlineData(GraphicsFormat.D16UNorm)]
    [InlineData(GraphicsFormat.D24UNormS8UInt)]
    public void FormatsOutsideImplementedColorBlitPath_DoNotAdvertiseTextureCopy(GraphicsFormat format)
    {
        GraphicsDeviceCapabilities capabilities = CreateCapabilities();

        Assert.True(capabilities.TryGetFormatCapabilities(format, out GraphicsFormatCapabilities support));
        Assert.False(support.Supports(GraphicsFormatSupport.CopySource));
        Assert.False(support.Supports(GraphicsFormatSupport.CopyDestination));
    }

    [Theory]
    [InlineData(GraphicsFormat.RGBA8UNorm)]
    [InlineData(GraphicsFormat.RGBA8UNormSrgb)]
    public void ImplementedRgbaColorBlitFormats_AdvertiseTextureCopy(GraphicsFormat format)
    {
        GraphicsDeviceCapabilities capabilities = CreateCapabilities();

        Assert.True(capabilities.TryGetFormatCapabilities(format, out GraphicsFormatCapabilities support));
        Assert.True(support.Supports(GraphicsFormatSupport.CopySource));
        Assert.True(support.Supports(GraphicsFormatSupport.CopyDestination));
        Assert.True(support.Supports(GraphicsFormatSupport.ColorAttachment));
    }

    [Fact]
    public void Rgba8StorageSupport_IsReportedOnlyForGles31AndNewer()
    {
        GraphicsDeviceCapabilities es30 = CreateCapabilities(3, 0);
        GraphicsDeviceCapabilities es31 = CreateCapabilities(3, 1);

        Assert.True(es30.TryGetFormatCapabilities(GraphicsFormat.RGBA8UNorm, out GraphicsFormatCapabilities es30Support));
        Assert.True(es31.TryGetFormatCapabilities(GraphicsFormat.RGBA8UNorm, out GraphicsFormatCapabilities es31Support));
        Assert.False(es30Support.Supports(GraphicsFormatSupport.Storage));
        Assert.True(es31Support.Supports(GraphicsFormatSupport.Storage));
    }

    [Fact]
    public void NormalizedAndDepthFormats_RetainTheirImplementedUsages()
    {
        GraphicsDeviceCapabilities capabilities = CreateCapabilities();

        Assert.True(capabilities.SupportsFormat(
            GraphicsFormat.R8UNorm,
            GraphicsFormatSupport.Sampled | GraphicsFormatSupport.ColorAttachment |
            GraphicsFormatSupport.VertexBuffer | GraphicsFormatSupport.LinearFiltering));
        Assert.True(capabilities.SupportsFormat(
            GraphicsFormat.RG8UNorm,
            GraphicsFormatSupport.Sampled | GraphicsFormatSupport.ColorAttachment |
            GraphicsFormatSupport.VertexBuffer | GraphicsFormatSupport.LinearFiltering));
        Assert.True(capabilities.SupportsFormat(
            GraphicsFormat.D16UNorm,
            GraphicsFormatSupport.Sampled | GraphicsFormatSupport.DepthStencilAttachment));
        Assert.True(capabilities.SupportsFormat(
            GraphicsFormat.D24UNormS8UInt,
            GraphicsFormatSupport.Sampled | GraphicsFormatSupport.DepthStencilAttachment));
    }
}


public sealed class OpenGLESTextureCopyCompatibilityTests
{
    private static GraphicsTextureDescription CreateSource(
        GraphicsFormat format = GraphicsFormat.RGBA8UNorm,
        uint width = 64,
        uint height = 32,
        uint mipLevels = 1,
        uint arrayLayers = 1,
        uint sampleCount = 1) =>
        new(
            GraphicsTextureDimension.Texture2D,
            format,
            width,
            height,
            1,
            mipLevels,
            arrayLayers,
            sampleCount,
            GraphicsTextureUsage.CopySource | GraphicsTextureUsage.ColorAttachment);

    private static GraphicsTextureDescription CreateDestination(
        GraphicsFormat format = GraphicsFormat.RGBA8UNorm,
        uint width = 64,
        uint height = 32,
        uint mipLevels = 1,
        uint arrayLayers = 1,
        uint sampleCount = 1) =>
        new(
            GraphicsTextureDimension.Texture2D,
            format,
            width,
            height,
            1,
            mipLevels,
            arrayLayers,
            sampleCount,
            GraphicsTextureUsage.CopyDestination | GraphicsTextureUsage.ColorAttachment);

    [Fact]
    public void ValidateCopy_AllowsDifferentSourceAndDestinationUsageFlags()
    {
        OpenGLESTextureCompatibility.ValidateCopy(CreateSource(), CreateDestination());
    }

    [Theory]
    [InlineData(63u, 32u)]
    [InlineData(64u, 31u)]
    public void ValidateCopy_RejectsMismatchedExtents(uint width, uint height)
    {
        Assert.Throws<GraphicsValidationException>(() =>
            OpenGLESTextureCompatibility.ValidateCopy(
                CreateSource(), CreateDestination(width: width, height: height)));
    }

    [Fact]
    public void ValidateCopy_RejectsMismatchedFormats()
    {
        Assert.Throws<GraphicsValidationException>(() =>
            OpenGLESTextureCompatibility.ValidateCopy(
                CreateSource(), CreateDestination(GraphicsFormat.RGBA8UNormSrgb)));
    }

    [Theory]
    [InlineData(2u, 1u, 1u)]
    [InlineData(1u, 2u, 1u)]
    [InlineData(1u, 1u, 2u)]
    public void ValidateCopy_RejectsMipArraysAndMultisampling(uint mips, uint layers, uint samples)
    {
        Assert.Throws<GraphicsValidationException>(() =>
            OpenGLESTextureCompatibility.ValidateCopy(
                CreateSource(mipLevels: mips, arrayLayers: layers, sampleCount: samples),
                CreateDestination(mipLevels: mips, arrayLayers: layers, sampleCount: samples)));
    }

    [Theory]
    [InlineData(GraphicsFormat.R8UNorm)]
    [InlineData(GraphicsFormat.RG8UNorm)]
    [InlineData(GraphicsFormat.D16UNorm)]
    [InlineData(GraphicsFormat.D24UNormS8UInt)]
    public void ValidateCopy_RejectsFormatsOutsideTheImplementedBlitPath(GraphicsFormat format)
    {
        Assert.Throws<GraphicsValidationException>(() =>
            OpenGLESTextureCompatibility.ValidateCopy(CreateSource(format), CreateDestination(format)));
    }

    [Fact]
    public void ValidateCopy_ImageCopyPathDoesNotRequireFramebufferAttachmentUsage()
    {
        GraphicsTextureDescription source = CreateSource() with
        {
            Usage = GraphicsTextureUsage.CopySource
        };
        GraphicsTextureDescription destination = CreateDestination() with
        {
            Usage = GraphicsTextureUsage.CopyDestination
        };

        OpenGLESTextureCompatibility.ValidateCopy(source, destination, supportsImageCopy: true);
    }

    [Fact]
    public void ValidateCopy_ImageCopyPathSupportsMatchingArraySubresources()
    {
        GraphicsTextureDescription source = CreateSource() with
        {
            ArrayLayers = 4,
            MipLevels = 3,
            Usage = GraphicsTextureUsage.CopySource
        };
        GraphicsTextureDescription destination = CreateDestination() with
        {
            ArrayLayers = 4,
            MipLevels = 3,
            Usage = GraphicsTextureUsage.CopyDestination
        };

        OpenGLESTextureCompatibility.ValidateCopy(source, destination, supportsImageCopy: true);
    }

    [Fact]
    public void ValidateCopy_ImageCopyPathRejectsMipOrLayerMismatches()
    {
        GraphicsTextureDescription source = CreateSource() with
        {
            ArrayLayers = 4,
            MipLevels = 3
        };
        GraphicsTextureDescription destination = CreateDestination() with
        {
            ArrayLayers = 3,
            MipLevels = 3
        };

        Assert.Throws<GraphicsValidationException>(() =>
            OpenGLESTextureCompatibility.ValidateCopy(source, destination, supportsImageCopy: true));
    }

    [Fact]
    public void ValidateCopy_RequiresSourceAndDestinationUsages()
    {
        GraphicsTextureDescription missingSource = CreateSource() with
        {
            Usage = GraphicsTextureUsage.ColorAttachment
        };
        GraphicsTextureDescription missingDestination = CreateDestination() with
        {
            Usage = GraphicsTextureUsage.ColorAttachment
        };

        Assert.Throws<GraphicsValidationException>(() =>
            OpenGLESTextureCompatibility.ValidateCopy(missingSource, CreateDestination()));
        Assert.Throws<GraphicsValidationException>(() =>
            OpenGLESTextureCompatibility.ValidateCopy(CreateSource(), missingDestination));
    }
}

public sealed class OpenGLESIntegerFormatCapabilityTests
{
    [Fact]
    public void QueryCapabilities_ExposesMrtLimitsAndIntegerColorFormatsWithoutBlendOrFiltering()
    {
        OpenGLESCapabilityProfile profile = OpenGLESCapabilityProfile.Create(
            new OpenGLESVersion(3, 1),
            ReadOnlySpan<string>.Empty,
            supportsPresentation: false);

        OpenGLESGraphicsDevice.OpenGLESQueriedLimits queried = new(
            MaxTextureSize: 4096,
            MaxTexture3DSize: 256,
            MaxArrayLayers: 256,
            MaxVertexAttributes: 16,
            MaxUniformBufferBindings: 24,
            MaxStorageBufferBindings: 8,
            MaxTextureUnits: 16,
            MaxImageUnits: 8,
            UniformBufferOffsetAlignment: 256,
            StorageBufferOffsetAlignment: 256,
            MaxComputeInvocations: 1024,
            MaxComputeWorkGroupSizeX: 1024,
            MaxComputeWorkGroupSizeY: 1024,
            MaxComputeWorkGroupSizeZ: 64,
            MaxComputeWorkGroupCountX: 65535,
            MaxComputeWorkGroupCountY: 65535,
            MaxComputeWorkGroupCountZ: 65535,
            MaxTessellationPatchSize: 0,
            MaxAnisotropy: 1,
            MaxColorAttachments: 4,
            MaxColorDrawBuffers: 4);

        GraphicsSampleCountFlags[] formatSampleCounts =
            new GraphicsSampleCountFlags[Enum.GetValues<GraphicsFormat>().Length];
        Array.Fill(formatSampleCounts, GraphicsSampleCountFlags.One);
        formatSampleCounts[(int)GraphicsFormat.RGBA8UNorm] |= GraphicsSampleCountFlags.Four;
        queried = queried with { FormatSampleCounts = formatSampleCounts };

        GraphicsDeviceCapabilities capabilities =
            OpenGLESGraphicsDevice.QueryCapabilities(profile, queried);

        Assert.True(capabilities.SupportsSampleCount(GraphicsFormat.RGBA8UNorm, 4));
        Assert.False(capabilities.SupportsSampleCount(GraphicsFormat.RGBA8UNorm, 8));
        Assert.Equal(4u, capabilities.Limits.MaxColorAttachments);
        Assert.Equal(4u, capabilities.Limits.MaxColorDrawBuffers);

        Assert.True(capabilities.SupportsFormat(
            GraphicsFormat.RGBA8UInt, GraphicsFormatSupport.Sampled | GraphicsFormatSupport.ColorAttachment));
        Assert.False(capabilities.SupportsFormat(
            GraphicsFormat.RGBA8UInt, GraphicsFormatSupport.Blend));
        Assert.False(capabilities.SupportsFormat(
            GraphicsFormat.RGBA8UInt, GraphicsFormatSupport.LinearFiltering));
        Assert.True(capabilities.SupportsSampleCount(GraphicsFormat.RGBA8UInt, 1));
        Assert.False(capabilities.SupportsSampleCount(GraphicsFormat.RGBA8UInt, 4));
    }

    [Fact]
    public void QueryCapabilities_Exposes32BitDepthFormatsOnlyOnEs32()
    {
        OpenGLESGraphicsDevice.OpenGLESQueriedLimits queried = CreateQueriedLimits();

        GraphicsDeviceCapabilities es31 = OpenGLESGraphicsDevice.QueryCapabilities(
            OpenGLESCapabilityProfile.Create(
                new OpenGLESVersion(3, 1),
                ReadOnlySpan<string>.Empty,
                supportsPresentation: false),
            queried);
        GraphicsDeviceCapabilities es32 = OpenGLESGraphicsDevice.QueryCapabilities(
            OpenGLESCapabilityProfile.Create(
                new OpenGLESVersion(3, 2),
                ReadOnlySpan<string>.Empty,
                supportsPresentation: false),
            queried);

        Assert.False(es31.SupportsFormat(
            GraphicsFormat.D32Float, GraphicsFormatSupport.DepthStencilAttachment));
        Assert.False(es31.SupportsFormat(
            GraphicsFormat.D32FloatS8UInt, GraphicsFormatSupport.DepthStencilAttachment));
        Assert.True(es32.SupportsFormat(
            GraphicsFormat.D32Float, GraphicsFormatSupport.DepthStencilAttachment));
        Assert.True(es32.SupportsFormat(
            GraphicsFormat.D32FloatS8UInt, GraphicsFormatSupport.DepthStencilAttachment));
    }

    [Fact]
    public void QueryCapabilities_UsesDistinctHalfFloatAndFullFloatFilteringRules()
    {
        OpenGLESGraphicsDevice.OpenGLESQueriedLimits queried = CreateQueriedLimits();
        OpenGLESCapabilityProfile withoutFloatLinear = OpenGLESCapabilityProfile.Create(
            new OpenGLESVersion(3, 1),
            ReadOnlySpan<string>.Empty,
            supportsPresentation: false);
        OpenGLESCapabilityProfile withFloatLinear = OpenGLESCapabilityProfile.Create(
            new OpenGLESVersion(3, 1),
            new[] { "GL_OES_texture_float_linear" },
            supportsPresentation: false);

        GraphicsDeviceCapabilities baseline =
            OpenGLESGraphicsDevice.QueryCapabilities(withoutFloatLinear, queried);
        GraphicsDeviceCapabilities filtered =
            OpenGLESGraphicsDevice.QueryCapabilities(withFloatLinear, queried);

        Assert.True(baseline.SupportsFormat(
            GraphicsFormat.R16Float, GraphicsFormatSupport.LinearFiltering));
        Assert.True(baseline.SupportsFormat(
            GraphicsFormat.RGBA16Float, GraphicsFormatSupport.LinearFiltering));
        Assert.False(baseline.SupportsFormat(
            GraphicsFormat.R32Float, GraphicsFormatSupport.LinearFiltering));
        Assert.True(filtered.SupportsFormat(
            GraphicsFormat.R32Float, GraphicsFormatSupport.LinearFiltering));
        Assert.True(filtered.SupportsFormat(
            GraphicsFormat.RGBA32Float, GraphicsFormatSupport.LinearFiltering));
    }

    private static OpenGLESGraphicsDevice.OpenGLESQueriedLimits CreateQueriedLimits() => new(
        MaxTextureSize: 4096,
        MaxTexture3DSize: 256,
        MaxArrayLayers: 256,
        MaxVertexAttributes: 16,
        MaxUniformBufferBindings: 24,
        MaxStorageBufferBindings: 8,
        MaxTextureUnits: 16,
        MaxImageUnits: 8,
        UniformBufferOffsetAlignment: 256,
        StorageBufferOffsetAlignment: 256,
        MaxComputeInvocations: 1024,
        MaxComputeWorkGroupSizeX: 1024,
        MaxComputeWorkGroupSizeY: 1024,
        MaxComputeWorkGroupSizeZ: 64,
        MaxComputeWorkGroupCountX: 65535,
        MaxComputeWorkGroupCountY: 65535,
        MaxComputeWorkGroupCountZ: 65535,
        MaxTessellationPatchSize: 32,
        MaxAnisotropy: 1,
        MaxColorAttachments: 4,
        MaxColorDrawBuffers: 4);

    [Theory]
    [InlineData(GraphicsFormat.R8UInt, SizedInternalFormat.R8ui)]
    [InlineData(GraphicsFormat.R8SInt, SizedInternalFormat.R8i)]
    [InlineData(GraphicsFormat.RG8UInt, SizedInternalFormat.RG8ui)]
    [InlineData(GraphicsFormat.RG8SInt, SizedInternalFormat.RG8i)]
    [InlineData(GraphicsFormat.RGBA8UInt, SizedInternalFormat.Rgba8ui)]
    [InlineData(GraphicsFormat.RGBA8SInt, SizedInternalFormat.Rgba8i)]
    public void GetInternalFormat_MapsEightBitIntegerFormats(
        GraphicsFormat format,
        SizedInternalFormat expected)
    {
        Assert.Equal(expected, OpenGLESGraphicsTexture.GetInternalFormat(format));
    }
}


public sealed class OpenGLESCompressedFormatTests
{
    [Theory]
    [InlineData(GraphicsFormat.BC1UNorm, 0x83F1)]
    [InlineData(GraphicsFormat.BC1UNormSrgb, 0x8C4D)]
    [InlineData(GraphicsFormat.BC3UNorm, 0x83F3)]
    [InlineData(GraphicsFormat.BC3UNormSrgb, 0x8C4F)]
    [InlineData(GraphicsFormat.BC5UNorm, 0x8DBD)]
    [InlineData(GraphicsFormat.BC7UNorm, 0x8E8C)]
    [InlineData(GraphicsFormat.BC7UNormSrgb, 0x8E8D)]
    [InlineData(GraphicsFormat.ASTC4x4UNorm, 0x93B0)]
    [InlineData(GraphicsFormat.ASTC4x4UNormSrgb, 0x93D0)]
    public void GetInternalFormat_MapsExtensionDefinedCompressedFormats(
        GraphicsFormat format,
        int expected)
    {
        Assert.Equal(expected, (int)OpenGLESGraphicsTexture.GetInternalFormat(format));
    }

    [Fact]
    public void QueryCapabilities_ReportsCompressedFormatsOnlyWhenExtensionsArePresent()
    {
        OpenGLESCapabilityProfile profile = OpenGLESCapabilityProfile.Create(
            new OpenGLESVersion(3, 1),
            [
                "GL_EXT_texture_compression_s3tc",
                "GL_EXT_texture_compression_s3tc_srgb",
                "GL_EXT_texture_compression_rgtc",
                "GL_EXT_texture_compression_bptc",
                "GL_KHR_texture_compression_astc_ldr"
            ],
            supportsPresentation: false);

        OpenGLESGraphicsDevice.OpenGLESQueriedLimits queried = new(
            MaxTextureSize: 4096, MaxTexture3DSize: 256, MaxArrayLayers: 256,
            MaxVertexAttributes: 16, MaxUniformBufferBindings: 24, MaxStorageBufferBindings: 8,
            MaxTextureUnits: 16, MaxImageUnits: 8,
            UniformBufferOffsetAlignment: 256, StorageBufferOffsetAlignment: 256,
            MaxComputeInvocations: 1024, MaxComputeWorkGroupSizeX: 1024,
            MaxComputeWorkGroupSizeY: 1024, MaxComputeWorkGroupSizeZ: 64,
            MaxComputeWorkGroupCountX: 65535, MaxComputeWorkGroupCountY: 65535,
            MaxComputeWorkGroupCountZ: 65535, MaxTessellationPatchSize: 0,
            MaxAnisotropy: 1, MaxColorAttachments: 4, MaxColorDrawBuffers: 4);

        GraphicsDeviceCapabilities capabilities =
            OpenGLESGraphicsDevice.QueryCapabilities(profile, queried, supportsImageCopy: true);

        Assert.True(capabilities.SupportsFormat(
            GraphicsFormat.BC1UNorm,
            GraphicsFormatSupport.Sampled | GraphicsFormatSupport.LinearFiltering |
            GraphicsFormatSupport.CopySource | GraphicsFormatSupport.CopyDestination));
        Assert.False(capabilities.SupportsFormat(GraphicsFormat.BC1UNorm, GraphicsFormatSupport.ColorAttachment));
        Assert.False(capabilities.SupportsFormat(GraphicsFormat.BC1UNorm, GraphicsFormatSupport.Storage));
        Assert.True(capabilities.SupportsFormat(GraphicsFormat.ASTC4x4UNormSrgb, GraphicsFormatSupport.Sampled));

        OpenGLESCapabilityProfile noCompression = OpenGLESCapabilityProfile.Create(
            new OpenGLESVersion(3, 1), ReadOnlySpan<string>.Empty, supportsPresentation: false);
        GraphicsDeviceCapabilities limited = OpenGLESGraphicsDevice.QueryCapabilities(noCompression, queried);
        Assert.False(limited.SupportsFormat(GraphicsFormat.BC1UNorm, GraphicsFormatSupport.Sampled));
        Assert.False(limited.SupportsFormat(GraphicsFormat.ASTC4x4UNorm, GraphicsFormatSupport.Sampled));
    }
}


public sealed class OpenGLESMeshTaskCapabilityTests
{
    [Fact]
    public void MeshTaskFeatureRequiresExtensionAndResolvedEntryPoints()
    {
        OpenGLESCapabilityProfile supported = OpenGLESCapabilityProfile.Create(
            new OpenGLESVersion(3, 1),
            ["GL_EXT_mesh_shader"],
            supportsPresentation: false,
            meshShaderEntryPointsAvailable: true);

        Assert.True(supported.Features.Supports(GraphicsFeature.MeshShaders));
        Assert.True(supported.Features.Supports(GraphicsFeature.TaskShaders));

        OpenGLESCapabilityProfile noFunctions = OpenGLESCapabilityProfile.Create(
            new OpenGLESVersion(3, 1),
            ["GL_EXT_mesh_shader"],
            supportsPresentation: false,
            meshShaderEntryPointsAvailable: false);

        Assert.False(noFunctions.Features.Supports(GraphicsFeature.MeshShaders));
        Assert.False(noFunctions.Features.Supports(GraphicsFeature.TaskShaders));

        OpenGLESCapabilityProfile noExtension = OpenGLESCapabilityProfile.Create(
            new OpenGLESVersion(3, 1),
            ReadOnlySpan<string>.Empty,
            supportsPresentation: false,
            meshShaderEntryPointsAvailable: true);

        Assert.False(noExtension.Features.Supports(GraphicsFeature.MeshShaders));
        Assert.False(noExtension.Features.Supports(GraphicsFeature.TaskShaders));
    }

    [Fact]
    public void QueryCapabilities_PublishesMeshAndTaskDispatchLimits()
    {
        OpenGLESCapabilityProfile profile = OpenGLESCapabilityProfile.Create(
            new OpenGLESVersion(3, 1),
            ["GL_EXT_mesh_shader"],
            supportsPresentation: false,
            meshShaderEntryPointsAvailable: true);

        OpenGLESGraphicsDevice.OpenGLESQueriedLimits queried = new(
            MaxTextureSize: 4096, MaxTexture3DSize: 256, MaxArrayLayers: 256,
            MaxVertexAttributes: 16, MaxUniformBufferBindings: 24, MaxStorageBufferBindings: 8,
            MaxTextureUnits: 16, MaxImageUnits: 8,
            UniformBufferOffsetAlignment: 256, StorageBufferOffsetAlignment: 256,
            MaxComputeInvocations: 1024, MaxComputeWorkGroupSizeX: 1024,
            MaxComputeWorkGroupSizeY: 1024, MaxComputeWorkGroupSizeZ: 64,
            MaxComputeWorkGroupCountX: 65535, MaxComputeWorkGroupCountY: 65535,
            MaxComputeWorkGroupCountZ: 65535, MaxTessellationPatchSize: 0,
            MaxAnisotropy: 1, MaxColorAttachments: 4, MaxColorDrawBuffers: 4,
            MaxMeshWorkGroupCountX: 65535, MaxMeshWorkGroupCountY: 65535,
            MaxMeshWorkGroupCountZ: 65535, MaxMeshWorkGroupTotalCount: 4194304,
            MaxTaskWorkGroupCountX: 65535, MaxTaskWorkGroupCountY: 65535,
            MaxTaskWorkGroupCountZ: 65535, MaxTaskWorkGroupTotalCount: 4194304);

        GraphicsDeviceCapabilities capabilities =
            OpenGLESGraphicsDevice.QueryCapabilities(profile, queried);

        Assert.True(capabilities.Features.Supports(GraphicsFeature.MeshShaders));
        Assert.True(capabilities.Features.Supports(GraphicsFeature.TaskShaders));
        Assert.Equal(65535u, capabilities.Limits.MaxMeshWorkGroupCountX);
        Assert.Equal(4194304u, capabilities.Limits.MaxMeshWorkGroupTotalCount);
        Assert.Equal(65535u, capabilities.Limits.MaxTaskWorkGroupCountZ);
        Assert.Equal(4194304u, capabilities.Limits.MaxTaskWorkGroupTotalCount);
    }
}

