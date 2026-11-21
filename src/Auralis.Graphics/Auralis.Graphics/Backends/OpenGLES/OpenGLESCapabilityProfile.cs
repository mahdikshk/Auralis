using System.Globalization;
using Auralis.Graphics.Abstractions;

namespace Auralis.Graphics.Backends.OpenGLES;

/// <summary>A parsed OpenGL ES major/minor version pair.</summary>
public readonly record struct OpenGLESVersion(int Major, int Minor) : IComparable<OpenGLESVersion>
{
    public static OpenGLESVersion RequiredBaseline { get; } = new(3, 1);

    public bool IsAtLeast(int major, int minor) =>
        Major > major || (Major == major && Minor >= minor);

    public int CompareTo(OpenGLESVersion other)
    {
        int majorComparison = Major.CompareTo(other.Major);
        return majorComparison != 0 ? majorComparison : Minor.CompareTo(other.Minor);
    }

    /// <summary>
    /// Parses the API version from a GLES GL_VERSION string, for example
    /// "OpenGL ES 3.2 V@0502.0" or "OpenGL ES 3.1 Mesa 24.0".
    /// Desktop OpenGL and GLSL version strings are intentionally rejected.
    /// </summary>
    public static bool TryParse(string? versionString, out OpenGLESVersion version)
    {
        version = default;
        if (string.IsNullOrWhiteSpace(versionString))
            return false;

        const string marker = "OpenGL ES";
        int markerIndex = versionString.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
            return false;

        int index = markerIndex + marker.Length;

        // Allow the optional legacy profile suffix ("-CM"), but do not scan arbitrary words:
        // otherwise a GLSL version embedded later in the same string could be mistaken for GL_VERSION.
        if (index < versionString.Length && versionString[index] == '-')
        {
            index++;
            int profileStart = index;
            while (index < versionString.Length && char.IsAsciiLetter(versionString[index]))
                index++;
            if (profileStart == index)
                return false;
        }

        while (index < versionString.Length && char.IsWhiteSpace(versionString[index]))
            index++;
        if (index >= versionString.Length || !char.IsAsciiDigit(versionString[index]))
            return false;

        int majorStart = index;
        while (index < versionString.Length && char.IsAsciiDigit(versionString[index]))
            index++;
        if (majorStart == index || index >= versionString.Length || versionString[index] != '.')
            return false;

        if (!int.TryParse(versionString.AsSpan(majorStart, index - majorStart),
                NumberStyles.None, CultureInfo.InvariantCulture, out int major))
            return false;

        index++;
        int minorStart = index;
        while (index < versionString.Length && char.IsAsciiDigit(versionString[index]))
            index++;
        if (minorStart == index ||
            !int.TryParse(versionString.AsSpan(minorStart, index - minorStart),
                NumberStyles.None, CultureInfo.InvariantCulture, out int minor))
            return false;

        if (major < 1 || minor < 0)
            return false;

        version = new OpenGLESVersion(major, minor);
        return true;
    }

    public static OpenGLESVersion Parse(string versionString)
    {
        ArgumentNullException.ThrowIfNull(versionString);
        return TryParse(versionString, out OpenGLESVersion version)
            ? version
            : throw new FormatException($"'{versionString}' is not a recognized OpenGL ES version string.");
    }

    public override string ToString() => $"{Major}.{Minor}";
}

/// <summary>
/// Conservative capability calculation for an already-created GLES context. Version promotion is used
/// only for features guaranteed by the corresponding GLES core specification; extension-only features
/// require the exact extension name. Device-specific limits and format support must still be queried
/// from the active implementation before those capabilities are exposed by a device.
/// </summary>
public sealed class OpenGLESCapabilityProfile
{
    private readonly string[] _extensions;

    public OpenGLESVersion Version { get; }

    public GraphicsFeatureSet Features { get; }

    public GraphicsQueueCapabilities QueueCapabilities { get; }

    public bool MeetsRequiredBaseline => Version.CompareTo(OpenGLESVersion.RequiredBaseline) >= 0;

    private OpenGLESCapabilityProfile(
        OpenGLESVersion version,
        string[] extensions,
        GraphicsFeatureSet features,
        GraphicsQueueCapabilities queueCapabilities)
    {
        Version = version;
        _extensions = extensions;
        Features = features;
        QueueCapabilities = queueCapabilities;
    }

    public bool HasExtension(string extension)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(extension);
        for (int i = 0; i < _extensions.Length; i++)
        {
            if (string.Equals(_extensions[i], extension, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    public void EnsureRequiredBaseline()
    {
        if (!MeetsRequiredBaseline)
            throw new NotSupportedException(
                $"OpenGL ES {OpenGLESVersion.RequiredBaseline} or newer is required; the context reports {Version}.");
    }

    public static OpenGLESCapabilityProfile Create(
        OpenGLESVersion version,
        ReadOnlySpan<string> extensions,
        bool supportsPresentation,
        bool multiDrawIndirectEntryPointsAvailable = true,
        bool indexedBlendEntryPointsAvailable = true,
        bool baseVertexEntryPointsAvailable = true,
        bool baseInstanceEntryPointsAvailable = true,
        bool textureViewEntryPointAvailable = true,
        bool meshShaderEntryPointsAvailable = true)
    {
        if (version.Major < 1 || version.Minor < 0)
            throw new ArgumentOutOfRangeException(nameof(version));

        string[] extensionCopy = extensions.ToArray();
        for (int i = 0; i < extensionCopy.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(extensionCopy[i]) ||
                extensionCopy[i].IndexOfAny([' ', '\t', '\r', '\n']) >= 0)
                throw new ArgumentException("Each GLES extension must be one non-empty token.", nameof(extensions));
            for (int j = 0; j < i; j++)
            {
                if (string.Equals(extensionCopy[i], extensionCopy[j], StringComparison.Ordinal))
                    throw new ArgumentException($"GLES extension '{extensionCopy[i]}' is duplicated.", nameof(extensions));
            }
        }

        bool es31 = version.IsAtLeast(3, 1);
        bool es32 = version.IsAtLeast(3, 2);
        GraphicsFeature features = GraphicsFeature.None;

        if (version.IsAtLeast(3, 0))
            features |= GraphicsFeature.TimelineSynchronization;

        if (es31)
        {
            features |= GraphicsFeature.ComputeShaders | GraphicsFeature.IndirectDrawing;
        }

        if (es32)
        {
            features |= GraphicsFeature.GeometryShaders | GraphicsFeature.TessellationShaders;
        }

        if (Contains(extensionCopy, "GL_EXT_texture_filter_anisotropic"))
            features |= GraphicsFeature.SamplerAnisotropy;

        // These features are exposed only when the exact GLES extension is advertised.
        if (multiDrawIndirectEntryPointsAvailable &&
            Contains(extensionCopy, "GL_EXT_multi_draw_indirect"))
            features |= GraphicsFeature.MultiDrawIndirect;
        if (indexedBlendEntryPointsAvailable &&
            Contains(extensionCopy, "GL_EXT_draw_buffers_indexed"))
            features |= GraphicsFeature.IndependentBlend;

        if (baseVertexEntryPointsAvailable &&
            Contains(extensionCopy, "GL_EXT_draw_elements_base_vertex"))
            features |= GraphicsFeature.BaseVertexDrawing;
        if (baseInstanceEntryPointsAvailable &&
            Contains(extensionCopy, "GL_EXT_base_instance") &&
            Contains(extensionCopy, "GL_EXT_draw_elements_base_vertex"))
            features |= GraphicsFeature.BaseInstanceDrawing;
        if (textureViewEntryPointAvailable &&
            (Contains(extensionCopy, "GL_EXT_texture_view") || Contains(extensionCopy, "GL_OES_texture_view")))
            features |= GraphicsFeature.TextureViews;

        // EXT_mesh_shader defines both stages for GLES 3.1+ with independent driver limits.
        if (meshShaderEntryPointsAvailable && Contains(extensionCopy, "GL_EXT_mesh_shader"))
            features |= GraphicsFeature.MeshShaders | GraphicsFeature.TaskShaders;

        GraphicsQueueCapabilities queues =
            GraphicsQueueCapabilities.Graphics | GraphicsQueueCapabilities.Copy;
        if (es31)
            queues |= GraphicsQueueCapabilities.Compute;
        if (supportsPresentation)
            queues |= GraphicsQueueCapabilities.Present;

        return new OpenGLESCapabilityProfile(
            version,
            extensionCopy,
            new GraphicsFeatureSet(features),
            queues);
    }

    private static bool Contains(string[] extensions, string target)
    {
        for (int i = 0; i < extensions.Length; i++)
        {
            if (string.Equals(extensions[i], target, StringComparison.Ordinal))
                return true;
        }

        return false;
    }
}
