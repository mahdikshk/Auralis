using System.Buffers.Binary;
using Auralis.Graphics.Abstractions;
using Auralis.Graphics.Resources;

namespace Auralis.Graphics.Backends.OpenGL;

/// <summary>Backend-independent validation of the SPIR-V binary contract used by GL_ARB_gl_spirv.</summary>
internal static class OpenGLSpirVValidation
{
    internal const uint MagicNumber = 0x07230203;
    internal const int MinimumHeaderSize = 20;

    internal static void Validate(in GraphicsShaderDescription description, bool supported)
    {
        if (!supported)
            throw new GraphicsFeatureNotSupportedException(
                GraphicsFeature.SpirVShaders,
                GraphicsFeature.SpirVShaders);

        ReadOnlySpan<byte> code = description.Code.Span;
        if (code.Length < MinimumHeaderSize || (code.Length & 3) != 0)
            throw new GraphicsValidationException(
                "SPIR-V shader binaries must contain a complete 20-byte header and be four-byte aligned.");

        uint magic = BinaryPrimitives.ReadUInt32LittleEndian(code);
        if (magic != MagicNumber)
            throw new GraphicsValidationException(
                $"The supplied shader binary has an invalid SPIR-V magic number (0x{magic:X8}).");
    }
}
