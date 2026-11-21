using System.Buffers;
using System.Runtime.InteropServices;

namespace Auralis.Graphics.Backends.OpenGL;

/// <summary>
/// Memory over an OpenGL mapped range. The view is invalidated immediately after Unmap or resource
/// disposal. Active MemoryHandle pins are tracked so the native mapping is not released underneath them.
/// </summary>
internal sealed unsafe class OpenGLMappedMemoryManager : MemoryManager<byte>
{
    private readonly object _gate = new();
    private nint _pointer;
    private readonly int _length;
    private int _pinCount;

    internal OpenGLMappedMemoryManager(void* pointer, int length)
        : this((nint)pointer, length)
    {
    }

    internal OpenGLMappedMemoryManager(nint pointer, int length)
    {
        if (pointer == 0)
            throw new ArgumentNullException(nameof(pointer));
        if (length <= 0)
            throw new ArgumentOutOfRangeException(nameof(length));

        _pointer = pointer;
        _length = length;
    }

    public override Span<byte> GetSpan()
    {
        lock (_gate)
        {
            if (_pointer == 0)
                throw new ObjectDisposedException(nameof(OpenGLMappedMemoryManager));

            return new Span<byte>((void*)_pointer, _length);
        }
    }

    public override MemoryHandle Pin(int elementIndex = 0)
    {
        if ((uint)elementIndex > (uint)_length)
            throw new ArgumentOutOfRangeException(nameof(elementIndex));

        lock (_gate)
        {
            if (_pointer == 0)
                throw new ObjectDisposedException(nameof(OpenGLMappedMemoryManager));

            _pinCount++;
            return new MemoryHandle((byte*)_pointer + elementIndex, default, this);
        }
    }

    public override void Unpin()
    {
        lock (_gate)
        {
            if (_pinCount <= 0)
                throw new InvalidOperationException("Mapped-memory pin accounting became unbalanced.");

            _pinCount--;
            if (_pinCount == 0)
                Monitor.PulseAll(_gate);
        }
    }

    internal void ValidateCanUnmap()
    {
        lock (_gate)
        {
            if (_pinCount != 0)
                throw new InvalidOperationException(
                    "The mapped buffer cannot be unmapped while one or more MemoryHandle pins are active.");
            if (_pointer == 0)
                throw new ObjectDisposedException(nameof(OpenGLMappedMemoryManager));
        }
    }

    /// <summary>Invalidates future Memory/Span/Pin operations but leaves existing native pins valid.</summary>
    internal void Invalidate() => Dispose(disposing: true);

    internal void ValidateNoPins()
    {
        lock (_gate)
        {
            if (_pinCount != 0)
                throw new InvalidOperationException(
                    "The mapped buffer cannot be released while one or more MemoryHandle pins are active.");
        }
    }

    /// <summary>Waits until previously issued MemoryHandle pins have been released.</summary>
    internal void WaitForPins()
    {
        lock (_gate)
        {
            while (_pinCount != 0)
                Monitor.Wait(_gate);
        }
    }

    protected override void Dispose(bool disposing)
    {
        lock (_gate)
            _pointer = 0;
    }
}
