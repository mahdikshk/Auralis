using Auralis.Graphics.Abstractions;
using Auralis.Graphics.Pipelines;
using Auralis.Graphics.Resources;
using Silk.NET.OpenGL;

namespace Auralis.Graphics.Backends.OpenGL;

/// <summary>Links backend-owned shader objects into a single OpenGL program.</summary>
internal static class OpenGLProgramLinker
{
    internal static uint Link(GL gl, ReadOnlySpan<IGraphicsShader> shaders)
    {
        uint program = gl.CreateProgram();
        if (program == 0)
            throw new GraphicsException("OpenGL failed to create a shader program.");

        try
        {
            for (int i = 0; i < shaders.Length; i++)
            {
                if (shaders[i] is not OpenGLGraphicsShader shader || shader.IsDisposed)
                    throw new GraphicsValidationException("All program shaders must be live objects from this OpenGL device.");
                gl.AttachShader(program, shader.Handle);
            }

            gl.LinkProgram(program);
            gl.GetProgram(program, ProgramPropertyARB.LinkStatus, out int linked);
            if (linked == 0)
                throw new GraphicsException($"OpenGL shader program link failed:\n{gl.GetProgramInfoLog(program)}");

            for (int i = 0; i < shaders.Length; i++)
                gl.DetachShader(program, ((OpenGLGraphicsShader)shaders[i]).Handle);

            return program;
        }
        catch
        {
            gl.DeleteProgram(program);
            throw;
        }
    }
}
