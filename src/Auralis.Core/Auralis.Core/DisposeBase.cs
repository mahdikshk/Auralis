using System;
using System.Collections.Generic;
using System.Text;

namespace Auralis.Core;

internal class DisposeBase : IDisposable,IReferenceCountable
{
    private int _refCount = 1;
    public bool IsDisposed { get; private set; }

    public int ReferenceCount => _refCount;

    protected virtual void Dispose(bool disposing)
    {
        if (!IsDisposed)
        {
            if (disposing)
            {
                this.ReleaseReference();
            }
            IsDisposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
    protected virtual void Destroy() { }

    public int AddReference()
    {
        OnAddReference();
        var newCount = Interlocked.Increment(ref _refCount);
        if (newCount <= 1)
        {
            throw new InvalidOperationException("Can't add a reference to an already released object");
        }
        return newCount;
    }

    public int ReleaseReference()
    {
        OnReleaseReference();

        var newCount = Interlocked.Decrement(ref _refCount);
        if (newCount == 0)
        {
            Destroy();
            IsDisposed = true;
        }
        else if (newCount < 0)
        {
            throw new InvalidOperationException("An object without an active reference can't be released");
        }
        return newCount;
    }

    protected virtual void OnAddReference() { }
    protected virtual void OnReleaseReference() { }
}
