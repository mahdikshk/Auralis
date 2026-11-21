using System.Runtime.ExceptionServices;

namespace Auralis.Graphics.Backends.OpenGL;

/// <summary>
/// Owns one native reference per distinct resource used by a recording. The collection is
/// recording-thread-owned; resource reference transitions themselves are atomic.
/// </summary>
internal sealed class OpenGLResourceRetention
{
    private readonly List<IOpenGLNativeResource> _resources = new(16);
    private readonly HashSet<IOpenGLNativeResource> _uniqueResources =
        new(ReferenceEqualityComparer.Instance);

    internal int Count => _resources.Count;

    internal void Retain(IOpenGLNativeResource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        if (!_uniqueResources.Add(resource))
            return;

        if (!resource.TryRetain())
        {
            _uniqueResources.Remove(resource);
            throw new ObjectDisposedException(resource.GetType().Name);
        }

        try
        {
            _resources.Add(resource);
        }
        catch
        {
            _uniqueResources.Remove(resource);
            resource.ReleaseReference();
            throw;
        }
    }

    internal void ReleaseAll()
    {
        Exception? failure = null;
        while (_resources.Count != 0)
        {
            int index = _resources.Count - 1;
            IOpenGLNativeResource resource = _resources[index];
            _resources.RemoveAt(index);
            _uniqueResources.Remove(resource);

            try
            {
                resource.ReleaseReference();
            }
            catch (Exception exception)
            {
                failure ??= exception;
            }
        }

        if (failure is not null)
            ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
