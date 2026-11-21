using Auralis.Graphics.Abstractions;
using Auralis.Graphics.Backends.OpenGL;
using Auralis.Graphics.Presentation;
using Silk.NET.GLFW;
using Silk.NET.OpenGL;

namespace Auralis.Graphics.OpenGL.TestSupport;

/// <summary>
/// Owns a hidden GLFW window and a desktop OpenGL 4.5 Core context for opt-in hardware tests
/// and hardware benchmarks. This helper is test/benchmark infrastructure, not a backend dependency.
/// </summary>
public sealed unsafe class GlfwOpenGLContextProvider : IOpenGLContextProvider
{
    private readonly Glfw _glfw;
    private readonly WindowHandle* _window;
    private readonly object _contextIdentity = new();
    private int _disposed;

    private GlfwOpenGLContextProvider(Glfw glfw, WindowHandle* window)
    {
        _glfw = glfw;
        _window = window;
    }

    public static GlfwOpenGLContextProvider Create()
    {
        Glfw glfw = Glfw.GetApi();
        WindowHandle* window = null;
        bool initialized = false;

        try
        {
            if (!glfw.Init())
                throw new InvalidOperationException(
                    "GLFW initialization failed. A usable desktop session/display and GLFW native library are required.");

            initialized = true;
            glfw.DefaultWindowHints();
            glfw.WindowHint(WindowHintClientApi.ClientApi, ClientApi.OpenGL);
            glfw.WindowHint(WindowHintInt.ContextVersionMajor, 4);
            glfw.WindowHint(WindowHintInt.ContextVersionMinor, 5);
            glfw.WindowHint(WindowHintOpenGlProfile.OpenGlProfile, OpenGlProfile.Core);
            glfw.WindowHint(WindowHintBool.Visible, false);
            glfw.WindowHint(WindowHintBool.Resizable, false);

            window = glfw.CreateWindow(64, 64, "Auralis OpenGL Hardware Tests", null, null);
            if (window is null)
                throw new InvalidOperationException(
                    "GLFW could not create a hidden OpenGL 4.5 Core window/context. The driver may not expose OpenGL 4.5.");

            glfw.MakeContextCurrent(window);
            if (glfw.GetCurrentContext() != window)
                throw new InvalidOperationException("GLFW did not make the requested OpenGL context current.");

            return new GlfwOpenGLContextProvider(glfw, window);
        }
        catch
        {
            if (window is not null)
            {
                try { glfw.MakeContextCurrent(null); } catch { }
                try { glfw.DestroyWindow(window); } catch { }
            }

            if (initialized)
            {
                try { glfw.Terminate(); } catch { }
            }

            glfw.Dispose();
            throw;
        }
    }

    public object ContextIdentity => _contextIdentity;
    public OpenGLContextOwnership Ownership => OpenGLContextOwnership.Owned;
    public OpenGLContextStateOwnership StateOwnership => OpenGLContextStateOwnership.Exclusive;
    public bool IsAvailable => Volatile.Read(ref _disposed) == 0;
    public string? UnavailableReason => IsAvailable ? null : "The GLFW context provider has been disposed.";
    public bool SupportsPresentation => false;

    public bool IsPresentationTargetCompatible(in GraphicsPresentationTargetSnapshot target) => false;

    public bool SupportsPresentMode(GraphicsPresentMode presentMode) => false;

    public void SetPresentMode(GraphicsPresentMode presentMode) =>
        throw new NotSupportedException("The test context is hidden and has no presentation drawable.");

    public void MakeCurrent()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        _glfw.MakeContextCurrent(_window);
        if (_glfw.GetCurrentContext() != _window)
            throw new InvalidOperationException("GLFW failed to activate the hardware-test context.");
    }

    public void ReleaseCurrent()
    {
        if (Volatile.Read(ref _disposed) != 0)
            return;

        _glfw.MakeContextCurrent(null);
    }

    public nint GetProcAddress(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        return _glfw.GetProcAddress(name);
    }

    /// <summary>
    /// Drains only the GL error flag after a completed test operation. It does not mutate GL state
    /// and is intentionally exposed only by this opt-in test-support provider.
    /// </summary>
    public IReadOnlyList<GLEnum> DrainErrors()
    {
        MakeCurrent();
        try
        {
            using GL gl = GL.GetApi(GetProcAddress);
            List<GLEnum> errors = new(2);
            for (int i = 0; i < 32; i++)
            {
                GLEnum error = gl.GetError();
                if (error == GLEnum.NoError)
                    break;
                errors.Add(error);
            }

            return errors;
        }
        finally
        {
            ReleaseCurrent();
        }
    }

    public void SwapBuffers() =>
        throw new NotSupportedException("The test context is hidden and has no presentation drawable.");

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        try
        {
            if (_glfw.GetCurrentContext() == _window)
                _glfw.MakeContextCurrent(null);
        }
        finally
        {
            try { _glfw.DestroyWindow(_window); }
            finally
            {
                try { _glfw.Terminate(); }
                finally { _glfw.Dispose(); }
            }
        }
    }
}
