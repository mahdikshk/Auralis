using Auralis.Graphics.Backends.OpenGLES;
using Auralis.Graphics.Presentation;
using Silk.NET.GLFW;

namespace Auralis.Graphics.OpenGL.TestSupport;

/// <summary>
/// Creates a hidden GLFW OpenGL ES 3.1 context for opt-in driver tests only. This helper lives in test
/// support; the graphics backend itself receives the already-created context and owns no EGL/Android code.
/// </summary>
public sealed unsafe class GlfwOpenGLESContextProvider : IOpenGLESContextProvider
{
    private readonly Glfw _glfw;
    private readonly WindowHandle* _window;
    private readonly object _identity = new();
    private int _disposed;

    private GlfwOpenGLESContextProvider(Glfw glfw, WindowHandle* window)
    {
        _glfw = glfw;
        _window = window;
    }

    public static GlfwOpenGLESContextProvider Create()
    {
        Glfw glfw = Glfw.GetApi();
        WindowHandle* window = null;
        bool initialized = false;
        try
        {
            if (!glfw.Init())
                throw new InvalidOperationException("GLFW could not initialize. A usable display/session and GLFW native library are required.");

            initialized = true;
            glfw.DefaultWindowHints();
            glfw.WindowHint(WindowHintClientApi.ClientApi, ClientApi.OpenGLES);
            glfw.WindowHint(WindowHintInt.ContextVersionMajor, 3);
            glfw.WindowHint(WindowHintInt.ContextVersionMinor, 1);
            glfw.WindowHint(WindowHintBool.Visible, false);
            glfw.WindowHint(WindowHintBool.Resizable, false);

            window = glfw.CreateWindow(64, 64, "Auralis OpenGL ES Hardware Tests", null, null);
            if (window is null)
                throw new InvalidOperationException("GLFW could not create a hidden OpenGL ES 3.1 context.");

            glfw.MakeContextCurrent(window);
            if (glfw.GetCurrentContext() != window)
                throw new InvalidOperationException("GLFW failed to make the OpenGL ES context current.");

            return new GlfwOpenGLESContextProvider(glfw, window);
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

    public object ContextIdentity => _identity;
    public OpenGLESContextOwnership Ownership => OpenGLESContextOwnership.Owned;
    public OpenGLESContextStateOwnership StateOwnership => OpenGLESContextStateOwnership.Exclusive;
    public bool IsAvailable => Volatile.Read(ref _disposed) == 0;
    public string? UnavailableReason => IsAvailable ? null : "The GLFW OpenGL ES context has been disposed.";
    public bool SupportsPresentation => false;

    public bool IsPresentationTargetCompatible(in GraphicsPresentationTargetSnapshot target) => false;
    public bool SupportsPresentMode(GraphicsPresentMode presentMode) => false;

    public void SetPresentMode(GraphicsPresentMode presentMode) =>
        throw new NotSupportedException("The hidden GLES test drawable does not support presentation.");

    public void MakeCurrent()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        _glfw.MakeContextCurrent(_window);
        if (_glfw.GetCurrentContext() != _window)
            throw new InvalidOperationException("GLFW failed to activate the OpenGL ES test context.");
    }

    public void ReleaseCurrent()
    {
        if (Volatile.Read(ref _disposed) == 0)
            _glfw.MakeContextCurrent(null);
    }

    public nint GetProcAddress(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        return _glfw.GetProcAddress(name);
    }

    public void SwapBuffers() =>
        throw new NotSupportedException("The hidden GLES test drawable does not support presentation.");

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
