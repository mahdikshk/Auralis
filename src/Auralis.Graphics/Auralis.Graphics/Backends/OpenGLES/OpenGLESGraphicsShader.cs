using System.Text;
using Auralis.Graphics.Abstractions;
using Auralis.Graphics.Resources;
using Silk.NET.OpenGLES;

namespace Auralis.Graphics.Backends.OpenGLES;

/// <summary>Compiled GLSL ES shader. Desktop GLSL and SPIR-V are deliberately rejected.</summary>
internal sealed class OpenGLESGraphicsShader : OpenGLESNativeDeviceObject, IGraphicsShader
{
    private readonly OpenGLESGraphicsDevice _device;
    private uint _handle;

    private OpenGLESGraphicsShader(
        OpenGLESGraphicsDevice device,
        in GraphicsShaderDescription description,
        uint handle) : base(device)
    {
        _device = device;
        Description = description;
        _handle = handle;
    }

    public GraphicsShaderDescription Description { get; }

    public string? DebugName => null;

    internal uint Handle
    {
        get
        {
            uint value = Volatile.Read(ref _handle);
            return value != 0 ? value : throw new ObjectDisposedException(nameof(OpenGLESGraphicsShader));
        }
    }

    internal static OpenGLESGraphicsShader CreateUnderContext(
        OpenGLESGraphicsDevice device,
        GL gl,
        in GraphicsShaderDescription description)
    {
        GraphicsShaderDescription copy = description;
        copy.Validate();

        if (copy.Format != GraphicsShaderFormat.GlslSource)
            throw new GraphicsValidationException(
                $"OpenGL ES accepts GLSL ES source only; shader format '{copy.Format.Identifier}' is unsupported. SPIR-V ingestion is not assumed.");

        if (!string.Equals(copy.EntryPoint, "main", StringComparison.Ordinal))
            throw new GraphicsValidationException("GLSL ES shaders must use the 'main' entry point.");

        if (!copy.SpecializationConstants.IsEmpty)
            throw new GraphicsValidationException("GLSL ES source does not support SPIR-V specialization constants.");

        ShaderType stage = GetShaderType(device, copy.Stage);
        string source;
        try
        {
            source = new UTF8Encoding(false, true).GetString(copy.Code.Span);
        }
        catch (DecoderFallbackException exception)
        {
            throw new GraphicsValidationException($"GLSL ES source is not valid UTF-8: {exception.Message}");
        }

        if (source.IndexOf('\0') >= 0)
            throw new GraphicsValidationException("GLSL ES source cannot contain embedded null characters.");

        ValidateGlslEsVersion(device, copy.Stage, source);

        uint handle = gl.CreateShader(stage);
        if (handle == 0)
            throw new GraphicsException("OpenGL ES failed to create a shader object.");

        try
        {
            gl.ShaderSource(handle, source);
            gl.CompileShader(handle);
            gl.GetShader(handle, GLEnum.CompileStatus, out int status);
            if (status == 0)
            {
                string diagnostic = gl.GetShaderInfoLog(handle);
                throw new GraphicsException(
                    $"OpenGL ES {device.Version} {copy.Stage} shader compilation failed:{Environment.NewLine}{diagnostic}");
            }

            OpenGLESGraphicsShader shader = new(device, copy, handle);
            device.RegisterUnderContext(shader);
            return shader;
        }
        catch
        {
            gl.DeleteShader(handle);
            throw;
        }
    }

    internal static ShaderType GetShaderType(OpenGLESGraphicsDevice device, GraphicsShaderStage stage) =>
        OpenGLESShaderCompatibility.GetNativeStage(device, stage);

    private static void ValidateGlslEsVersion(
        OpenGLESGraphicsDevice device,
        GraphicsShaderStage stage,
        string source) =>
        OpenGLESShaderCompatibility.ValidateSourceVersion(device.Version, stage, source);

    protected override void DisposeCore() => ReleaseOwnerReference();

    public override void ReleaseNative(GL gl)
    {
        uint handle = Interlocked.Exchange(ref _handle, 0);
        if (handle != 0)
            gl.DeleteShader(handle);
    }
}
