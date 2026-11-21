using Auralis.Graphics.Presentation;

namespace Auralis.Graphics.Backends.OpenGLES;

/// <summary>Lifetime ownership of a host-created OpenGL ES context.</summary>
public enum OpenGLESContextOwnership
{
    /// <summary>The host owns and disposes the native context and drawable.</summary>
    Borrowed,

    /// <summary>The graphics device assumes responsibility for disposing the context provider.</summary>
    Owned
}

/// <summary>Declares who may mutate GLES state while the context is attached to Auralis.</summary>
public enum OpenGLESContextStateOwnership
{
    /// <summary>The host may also issue GLES calls. This mode requires state save/restore support.</summary>
    Shared,

    /// <summary>Auralis has exclusive use of GLES state while the device is alive.</summary>
    Exclusive
}

/// <summary>
/// Bridges a host-created OpenGL ES context into Auralis. This contract deliberately does not create
/// EGL objects, Android surfaces, windows, or platform contexts. The host must supply a currentable
/// GLES context and resolve entry points for that exact context. Desktop OpenGL contexts are not valid.
/// </summary>
public interface IOpenGLESContextProvider : IDisposable
{
    /// <summary>A stable identity token for this exact native context.</summary>
    object ContextIdentity { get; }

    OpenGLESContextOwnership Ownership { get; }

    /// <summary>
    /// The backend currently requires Exclusive ownership because it does not snapshot arbitrary
    /// host GLES state around command execution.
    /// </summary>
    OpenGLESContextStateOwnership StateOwnership { get; }

    bool IsAvailable { get; }

    string? UnavailableReason { get; }

    /// <summary>True only when this context is associated with a host drawable that can be swapped.</summary>
    bool SupportsPresentation { get; }

    /// <summary>Verifies that a supplied target refers to the drawable associated with this context.</summary>
    bool IsPresentationTargetCompatible(in GraphicsPresentationTargetSnapshot target);

    bool SupportsPresentMode(GraphicsPresentMode presentMode);

    /// <summary>Applies the requested swap policy through the host/platform integration.</summary>
    void SetPresentMode(GraphicsPresentMode presentMode);

    /// <summary>Makes this exact GLES context current on the calling thread.</summary>
    void MakeCurrent();

    /// <summary>Releases this context from the calling thread without destroying it.</summary>
    void ReleaseCurrent();

    /// <summary>Returns a GLES entry-point address, or zero when the host cannot resolve it.</summary>
    nint GetProcAddress(string name);

    /// <summary>Swaps the host drawable. Must throw when SupportsPresentation is false.</summary>
    void SwapBuffers();
}
