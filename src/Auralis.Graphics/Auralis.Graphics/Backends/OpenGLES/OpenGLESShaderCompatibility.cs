using System.Globalization;
using System.Text.RegularExpressions;
using Auralis.Graphics.Abstractions;
using Auralis.Graphics.Resources;
using Silk.NET.OpenGLES;

namespace Auralis.Graphics.Backends.OpenGLES;

/// <summary>Pure OpenGL ES shader stage and GLSL ES language validation.</summary>
internal static class OpenGLESShaderCompatibility
{
    internal static ShaderType GetNativeStage(OpenGLESGraphicsDevice device, GraphicsShaderStage stage)
    {
        if (stage is GraphicsShaderStage.Mesh or GraphicsShaderStage.Task)
        {
            bool deviceMeshShadersAvailable =
                device.Capabilities.Features.Supports(GraphicsFeature.MeshShaders) &&
                (stage != GraphicsShaderStage.Task ||
                    device.Capabilities.Features.Supports(GraphicsFeature.TaskShaders));
            return GetMeshNativeStage(deviceMeshShadersAvailable, stage);
        }

        return GetNativeStage(device.Version, stage);
    }

    private static ShaderType GetMeshNativeStage(bool deviceMeshShadersAvailable, GraphicsShaderStage stage) =>
        stage switch
        {
            GraphicsShaderStage.Mesh when deviceMeshShadersAvailable => (ShaderType)0x9559,
            GraphicsShaderStage.Task when deviceMeshShadersAvailable => (ShaderType)0x955A,
            GraphicsShaderStage.Mesh =>
                throw new GraphicsFeatureNotSupportedException(GraphicsFeature.MeshShaders, GraphicsFeature.MeshShaders),
            GraphicsShaderStage.Task =>
                throw new GraphicsFeatureNotSupportedException(GraphicsFeature.TaskShaders, GraphicsFeature.TaskShaders),
            _ => throw new ArgumentOutOfRangeException(nameof(stage))
        };

    internal static ShaderType GetNativeStage(OpenGLESVersion version, GraphicsShaderStage stage) =>
        stage switch
        {
            GraphicsShaderStage.Vertex => ShaderType.VertexShader,
            GraphicsShaderStage.Fragment => ShaderType.FragmentShader,
            GraphicsShaderStage.Compute when version.IsAtLeast(3, 1) => ShaderType.ComputeShader,
            GraphicsShaderStage.Geometry when version.IsAtLeast(3, 2) => ShaderType.GeometryShader,
            GraphicsShaderStage.TessellationControl when version.IsAtLeast(3, 2) => ShaderType.TessControlShader,
            GraphicsShaderStage.TessellationEvaluation when version.IsAtLeast(3, 2) => ShaderType.TessEvaluationShader,
            GraphicsShaderStage.Compute =>
                throw new GraphicsFeatureNotSupportedException(GraphicsFeature.ComputeShaders, GraphicsFeature.ComputeShaders),
            GraphicsShaderStage.Geometry =>
                throw new GraphicsFeatureNotSupportedException(GraphicsFeature.GeometryShaders, GraphicsFeature.GeometryShaders),
            GraphicsShaderStage.TessellationControl or GraphicsShaderStage.TessellationEvaluation =>
                throw new GraphicsFeatureNotSupportedException(GraphicsFeature.TessellationShaders, GraphicsFeature.TessellationShaders),
            GraphicsShaderStage.Mesh or GraphicsShaderStage.Task =>
                throw new GraphicsFeatureNotSupportedException(
                    GraphicsFeature.MeshShaders | GraphicsFeature.TaskShaders,
                    GraphicsFeature.MeshShaders | GraphicsFeature.TaskShaders),
            _ => throw new GraphicsValidationException($"Unsupported OpenGL ES shader stage: {stage}.")
        };

    internal static void ValidateSourceVersion(OpenGLESVersion contextVersion, GraphicsShaderStage stage, string source)
    {
        Match match = Regex.Match(
            source,
            @"^\s*#\s*version\s+(?<version>100|300|310|320)\s+es\b",
            RegexOptions.CultureInvariant);

        if (!match.Success)
            throw new GraphicsValidationException(
                "GLSL ES source must begin with an explicit '#version 300 es', '#version 310 es', or '#version 320 es' directive.");

        int languageVersion = int.Parse(match.Groups["version"].Value, CultureInfo.InvariantCulture);
        if (languageVersion == 100)
            throw new GraphicsValidationException("GLSL ES 1.00 is not supported; the backend requires GLSL ES 3.00 or newer.");

        if (languageVersion >= 310 && !contextVersion.IsAtLeast(3, 1))
            throw new GraphicsValidationException("GLSL ES 3.10 requires an OpenGL ES 3.1 context.");
        if (languageVersion >= 320 && !contextVersion.IsAtLeast(3, 2))
            throw new GraphicsValidationException("GLSL ES 3.20 requires an OpenGL ES 3.2 context.");

        if (stage == GraphicsShaderStage.Compute && languageVersion < 310)
            throw new GraphicsValidationException("Compute shaders require '#version 310 es' or newer.");
        if (stage is GraphicsShaderStage.Geometry or GraphicsShaderStage.TessellationControl or GraphicsShaderStage.TessellationEvaluation &&
            languageVersion < 320)
            throw new GraphicsValidationException("Geometry and tessellation shaders require '#version 320 es'.");

        if (stage is GraphicsShaderStage.Mesh or GraphicsShaderStage.Task)
        {
            if (languageVersion < 310)
                throw new GraphicsValidationException("GLES EXT mesh/task shaders require '#version 310 es' or newer.");
            if (!Regex.IsMatch(
                    source,
                    @"^\s*#\s*extension\s+GL_EXT_mesh_shader\s*:\s*require\b",
                    RegexOptions.Multiline | RegexOptions.CultureInvariant))
            {
                throw new GraphicsValidationException(
                    "GLES mesh/task shader source must enable GL_EXT_mesh_shader with '#extension GL_EXT_mesh_shader : require'.");
            }
        }
    }
}
