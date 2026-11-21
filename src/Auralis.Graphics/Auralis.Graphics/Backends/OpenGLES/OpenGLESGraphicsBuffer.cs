using System.Buffers;
using System.Runtime.InteropServices;
using Auralis.Graphics.Abstractions;
using Auralis.Graphics.Resources;
using Silk.NET.OpenGLES;

namespace Auralis.Graphics.Backends.OpenGLES;

/// <summary>
/// OpenGL ES buffer using the ES 3.x bind-to-edit API. CPU mappings use transient map/unmap operations;
/// persistent mapping and desktop buffer-storage flags are never assumed.
/// </summary>
internal sealed unsafe class OpenGLESGraphicsBuffer : OpenGLESNativeDeviceObject, IGraphicsBuffer
{
    private readonly OpenGLESGraphicsDevice _device;
    private readonly object _mapGate = new();
    private uint _handle;
    private OpenGLESMappedStagingMemoryManager? _mapped;
    private ulong _mappedOffset;
    private GraphicsMapMode _mappedMode;

    private OpenGLESGraphicsBuffer(
        OpenGLESGraphicsDevice device,
        in GraphicsBufferDescription description,
        uint handle) : base(device)
    {
        _device = device;
        Description = description;
        _handle = handle;
    }

    public GraphicsBufferDescription Description { get; }

    public string? DebugName => null;

    internal uint Handle
    {
        get
        {
            uint value = Volatile.Read(ref _handle);
            return value != 0 ? value : throw new ObjectDisposedException(nameof(OpenGLESGraphicsBuffer));
        }
    }

    internal static OpenGLESGraphicsBuffer CreateUnderContext(
        OpenGLESGraphicsDevice device,
        GL gl,
        in GraphicsBufferDescription description)
    {
        if (description.SizeInBytes > (ulong)nuint.MaxValue)
            throw new GraphicsValidationException("The requested buffer exceeds the native addressable size.");

        uint handle = gl.GenBuffer();
        if (handle == 0)
            throw new GraphicsException("OpenGL ES failed to allocate a buffer name.");

        try
        {
            gl.BindBuffer(BufferTargetARB.CopyWriteBuffer, handle);
            BufferUsageARB usage = description.MemoryAccess switch
            {
                GraphicsMemoryAccess.GpuOnly => BufferUsageARB.StaticDraw,
                GraphicsMemoryAccess.CpuToGpu => BufferUsageARB.DynamicDraw,
                GraphicsMemoryAccess.GpuToCpu => BufferUsageARB.StreamRead,
                GraphicsMemoryAccess.CpuReadWrite => BufferUsageARB.DynamicDraw,
                _ => throw new ArgumentOutOfRangeException(nameof(description))
            };
            gl.BufferData(BufferTargetARB.CopyWriteBuffer, (nuint)description.SizeInBytes, null, usage);
            gl.BindBuffer(BufferTargetARB.CopyWriteBuffer, 0);

            OpenGLESGraphicsBuffer buffer = new(device, description, handle);
            device.RegisterUnderContext(buffer);
            return buffer;
        }
        catch
        {
            gl.BindBuffer(BufferTargetARB.CopyWriteBuffer, 0);
            gl.DeleteBuffer(handle);
            throw;
        }
    }

    public Memory<byte> Map(GraphicsMapMode mode, ulong offset = 0, ulong size = ulong.MaxValue)
    {
        ThrowIfDisposed();
        if (!Enum.IsDefined(mode))
            throw new ArgumentOutOfRangeException(nameof(mode));

        bool cpuReadable = Description.MemoryAccess is GraphicsMemoryAccess.GpuToCpu or GraphicsMemoryAccess.CpuReadWrite;
        bool cpuWritable = Description.MemoryAccess is GraphicsMemoryAccess.CpuToGpu or GraphicsMemoryAccess.CpuReadWrite;
        if (mode is GraphicsMapMode.Read or GraphicsMapMode.ReadWrite && !cpuReadable)
            throw new GraphicsValidationException("The buffer was not created with CPU-readable memory access.");
        if (mode is GraphicsMapMode.Write or GraphicsMapMode.ReadWrite && !cpuWritable)
            throw new GraphicsValidationException("The buffer was not created with CPU-writable memory access.");

        if (size == ulong.MaxValue)
        {
            if (offset > Description.SizeInBytes)
                throw new ArgumentOutOfRangeException(nameof(offset));
            size = Description.SizeInBytes - offset;
        }

        if (size == 0 || offset > Description.SizeInBytes ||
            size > Description.SizeInBytes - offset || size > int.MaxValue ||
            offset > (ulong)nint.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(size), "The mapped range is empty or outside the buffer.");

        return _device.WithContext(gl =>
        {
            lock (_mapGate)
            {
                ThrowIfDisposed();
                if (_mapped is not null)
                    throw new InvalidOperationException("The buffer is already mapped.");

                OpenGLESMappedStagingMemoryManager manager = new((int)size);
                if (mode is GraphicsMapMode.Read or GraphicsMapMode.ReadWrite)
                {
                    gl.BindBuffer(BufferTargetARB.CopyReadBuffer, Handle);
                    void* pointer = gl.MapBufferRange(
                        BufferTargetARB.CopyReadBuffer,
                        (nint)offset,
                        (nuint)size,
                        MapBufferAccessMask.ReadBit);
                    if (pointer == null)
                    {
                        gl.BindBuffer(BufferTargetARB.CopyReadBuffer, 0);
                        manager.Invalidate();
                        throw new GraphicsException("OpenGL ES failed to map the requested buffer range for reading.");
                    }

                    new ReadOnlySpan<byte>(pointer, (int)size).CopyTo(manager.GetSpan());
                    bool valid = gl.UnmapBuffer(BufferTargetARB.CopyReadBuffer);
                    gl.BindBuffer(BufferTargetARB.CopyReadBuffer, 0);
                    if (!valid)
                    {
                        manager.Invalidate();
                        throw new GraphicsException("OpenGL ES reported corrupted buffer contents while unmapping a read range.");
                    }
                }

                _mapped = manager;
                _mappedOffset = offset;
                _mappedMode = mode;
                return manager.Memory;
            }
        });
    }

    public void Unmap()
    {
        ThrowIfDisposed();
        _device.WithContext(gl =>
        {
            lock (_mapGate)
            {
                ThrowIfDisposed();
                OpenGLESMappedStagingMemoryManager manager = _mapped ??
                    throw new InvalidOperationException("The buffer is not mapped.");

                if (_mappedMode is GraphicsMapMode.Write or GraphicsMapMode.ReadWrite)
                {
                    ReadOnlySpan<byte> data = manager.GetSpan();
                    gl.BindBuffer(BufferTargetARB.CopyWriteBuffer, Handle);
                    fixed (byte* pointer = data)
                        gl.BufferSubData(
                            BufferTargetARB.CopyWriteBuffer,
                            (nint)_mappedOffset,
                            (nuint)data.Length,
                            pointer);
                    gl.BindBuffer(BufferTargetARB.CopyWriteBuffer, 0);
                }

                _mapped = null;
                _mappedOffset = 0;
                manager.Invalidate();
            }
        });
    }

    protected override void DisposeCore()
    {
        OpenGLESMappedStagingMemoryManager? mapped;
        lock (_mapGate)
        {
            mapped = _mapped;
            _mapped = null;
        }
        mapped?.Invalidate();
        ReleaseOwnerReference();
    }

    public override void ReleaseNative(GL gl)
    {
        lock (_mapGate)
        {
            uint handle = Interlocked.Exchange(ref _handle, 0);
            if (handle == 0)
                return;
            _mapped?.Invalidate();
            _mapped = null;
            gl.DeleteBuffer(handle);
        }
    }
}

/// <summary>Managed staging memory whose views throw after Unmap or resource disposal.</summary>
internal sealed unsafe class OpenGLESMappedStagingMemoryManager : MemoryManager<byte>
{
    private byte[]? _buffer;

    internal OpenGLESMappedStagingMemoryManager(int length) => _buffer = GC.AllocateUninitializedArray<byte>(length);

    public override Span<byte> GetSpan() =>
        Volatile.Read(ref _buffer) is { } buffer
            ? buffer.AsSpan()
            : throw new ObjectDisposedException(nameof(OpenGLESMappedStagingMemoryManager));

    public override MemoryHandle Pin(int elementIndex = 0)
    {
        byte[] buffer = Volatile.Read(ref _buffer) ??
            throw new ObjectDisposedException(nameof(OpenGLESMappedStagingMemoryManager));
        if ((uint)elementIndex > (uint)buffer.Length)
            throw new ArgumentOutOfRangeException(nameof(elementIndex));
        GCHandle handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        return new MemoryHandle((byte*)handle.AddrOfPinnedObject() + elementIndex, handle, this);
    }

    public override void Unpin() { }

    internal void Invalidate() => Dispose(true);

    protected override void Dispose(bool disposing) => Interlocked.Exchange(ref _buffer, null);
}
