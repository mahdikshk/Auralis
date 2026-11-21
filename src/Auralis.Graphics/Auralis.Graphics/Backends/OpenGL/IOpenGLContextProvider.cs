using Auralis.Graphics.Abstractions;
using Auralis.Graphics.Presentation;

namespace Auralis.Graphics.Backends.OpenGL;

/// <summary>Ownership of the native context represented by an OpenGL context provider.</summary>
public enum OpenGLContextOwnership
{
    /// <summary>The host retains responsibility for disposing the native context and its drawable.</summary>
    Borrowed,

    /// <summary>The provider transferred context lifetime responsibility to the graphics device.</summary>
    Owned
}

/// <summary>Declares whether Auralis may own and mutate all OpenGL state in the supplied context.</summary>
public enum OpenGLContextStateOwnership
{
    /// <summary>The host and Auralis share GL state; the initial backend cannot attach in this mode.</summary>
    Shared,

    /// <summary>The host lends exclusive GL-state control to Auralis while the device is alive.</summary>
    Exclusive
}

/// <summary>
/// Bridges a host/platform-created desktop OpenGL context into Auralis. Implementations must make
/// the represented context current on the calling thread and resolve function pointers for that context.
/// They must not silently substitute an OpenGL ES context.
/// </summary>
public interface IOpenGLContextProvider : IDisposable
{
    /// <summary>A stable identity token for this exact context. It must not change during its lifetime.</summary>
    object ContextIdentity { get; }

    OpenGLContextOwnership Ownership { get; }

    /// <summary>
    /// Must be Exclusive for the initial backend. GL state is not saved and restored around submissions;
    /// the host must not issue GL calls on this context while it is attached to Auralis.
    /// </summary>
    OpenGLContextStateOwnership StateOwnership { get; }

    bool IsAvailable { get; }

    string? UnavailableReason { get; }

    /// <summary>Whether the context has a drawable that this provider can swap.</summary>
    bool SupportsPresentation { get; }

    /// <summary>
    /// Verifies that the supplied platform target identifies the exact drawable associated with this context.
    /// The default framebuffer must never be presented to an unrelated HWND, X11 Window, layer, or surface.
    /// </summary>
    bool IsPresentationTargetCompatible(in GraphicsPresentationTargetSnapshot target);

    /// <summary>Whether the provider can honor a requested swap interval/presentation policy.</summary>
    bool SupportsPresentMode(GraphicsPresentMode presentMode);

    /// <summary>
    /// Applies the requested presentation policy to the context's drawable. Must throw when unsupported.
    /// Platform-specific swap-interval APIs remain the provider's responsibility.
    /// </summary>
    void SetPresentMode(GraphicsPresentMode presentMode);

    /// <summary>Makes this context current on the calling thread, throwing if activation fails.</summary>
    void MakeCurrent();

    /// <summary>Releases this context from the calling thread without destroying it.</summary>
    void ReleaseCurrent();

    /// <summary>Returns a function pointer for the named OpenGL entry point, or zero when unavailable.</summary>
    nint GetProcAddress(string name);

    /// <summary>Swaps the associated drawable. Must throw when SupportsPresentation is false.</summary>
    void SwapBuffers();
}
