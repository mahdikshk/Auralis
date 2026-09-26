using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Auralis.Core;
/// <summary>
/// Provides a collection of low-level, high-performance helpers for unmanaged memory
/// management, including aligned/unaligned copies, zeroing, allocation (plain and aligned),
/// freeing, and promotion of unmanaged buffers to arrays on the Pinned Object Heap (POH).
/// </summary>
public static partial class MemoryUtilities
{
    private static readonly bool _isUnalignedSafe;
    /// <summary>
    /// Initializes the static state of <see cref="MemoryUtilities"/> by detecting whether the
    /// current processor architecture can safely perform unaligned memory accesses.
    /// </summary>
    /// <remarks>
    /// The result is cached in a <see langword="static readonly"/> field so the JIT compiler can
    /// treat it as a constant and fold away the branch that depends on it, allowing the code paths
    /// below to be specialized per architecture at runtime with no measurable overhead.
    /// x86, x64, and Arm64 all support unaligned accesses natively; other architectures may fault.
    /// </remarks>
    static MemoryUtilities()
    {
        _isUnalignedSafe =
        RuntimeInformation.ProcessArchitecture is Architecture.X64 or Architecture.X86 or Architecture.Arm64;
    }
    /// <summary>
    /// Copies a given number of bytes from a source memory location to a destination memory
    /// location, choosing an implementation that matches the current platform's alignment support.
    /// </summary>
    /// <param name="source">A reference to the first byte of the region to copy from.</param>
    /// <param name="destination">A reference to the first byte of the region to copy into.</param>
    /// <param name="byteCount">The number of bytes to copy.</param>
    /// <remarks>
    /// On architectures that tolerate unaligned accesses (x86/x64/Arm64) this uses
    /// <see cref="Buffer.MemoryCopy(void*, void*, long, long)"/>, which can be vectorized by the
    /// runtime; otherwise it falls back to <see cref="Unsafe.CopyBlockUnaligned(ref byte, ref readonly byte, uint)"/>.
    /// The caller is responsible for ensuring both regions are valid for <paramref name="byteCount"/>
    /// bytes and do not overlap in ways that make a block copy unsafe.
    /// </remarks>
    public static unsafe void CopyWithAlignmentFallback(ref readonly byte source, ref byte destination, uint byteCount)
    {
        if (_isUnalignedSafe)
        {
            fixed (byte* sourcePtr = &source, destinationPtr = &destination)
            {
                Buffer.MemoryCopy(sourcePtr, destinationPtr, byteCount, byteCount);
            }
        }
        else
        {
            Unsafe.CopyBlockUnaligned(ref destination, in source, byteCount);
        }
    }
    /// <summary>
    /// Copies a given number of bytes between two raw memory addresses, taking an
    /// alignment-aware fast path when the current platform supports unaligned accesses.
    /// </summary>
    /// <param name="source">The address of the first byte of the region to copy from.</param>
    /// <param name="destination">The address of the first byte of the region to copy into.</param>
    /// <param name="byteCount">The number of bytes to copy.</param>
    /// <remarks>
    /// This is the pointer-based counterpart of
    /// <see cref="CopyWithAlignmentFallback(ref readonly byte, ref byte, uint)"/>: when running on an
    /// unaligned-safe architecture the copy is performed via <see cref="Buffer.MemoryCopy(void*, void*, long, long)"/>,
    /// which is typically faster because the runtime can optimize it for size and alignment.
    /// As with the reference-based overload, the caller must guarantee the regions are valid and non-overlapping.
    /// </remarks>
    public static unsafe void CopyWithAlignmentFallback(nint source, nint destination, uint byteCount)
    {
        if (_isUnalignedSafe)
        {
            Buffer.MemoryCopy((void*)source, (void*)destination, byteCount, byteCount);
        }
        Unsafe.CopyBlockUnaligned((void*)destination, (void*)source, byteCount);
    }
    /// <summary>
    /// Sets every byte of an unmanaged memory block to zero, starting at the given pointer.
    /// </summary>
    /// <param name="startAddress">A pointer to the first byte of the block to clear.</param>
    /// <param name="byteCount">The size of the block to clear, in bytes.</param>
    /// <remarks>
    /// Implemented by wrapping the region in a <see cref="Span{T}"/> and clearing it, which lets the
    /// runtime use optimized (often vectorized) memset-style writes. The block must be writable and
    /// at least <paramref name="byteCount"/> bytes long.
    /// </remarks>
    public static unsafe void Clear(void* startAddress, uint byteCount)
    {
        new Span<byte>(startAddress, (int)byteCount).Clear();
    }
    /// <summary>
    /// Sets every byte of an unmanaged memory block to zero, starting at the given address.
    /// </summary>
    /// <param name="startAddress">The address of the first byte of the block to clear.</param>
    /// <param name="byteCount">The size of the block to clear, in bytes.</param>
    /// <remarks>
    /// This is the <see cref="nint"/>-based overload of <see cref="Clear(void*, uint)"/>, provided for
    /// convenience when working with handles or addresses stored as integral types.
    /// </remarks>
    public static unsafe void Clear(nint startAddress, uint byteCount)
    {
        new Span<byte>((void*)startAddress, (int)byteCount).Clear();
    }
    /// <summary>
    /// Sets every byte of an unmanaged memory block to zero, starting at the given reference.
    /// </summary>
    /// <param name="startAddress">A reference to the first byte of the block to clear.</param>
    /// <param name="byteCount">The size of the block to clear, in bytes.</param>
    /// <remarks>
    /// The span is created directly over the referenced element with
    /// <see cref="MemoryMarshal.CreateSpan{T}(ref T, int)"/>, so no defensive copying occurs.
    /// The caller must ensure the referenced region is backed by at least <paramref name="byteCount"/>
    /// writable bytes.
    /// </remarks>
    public static unsafe void Clear(ref byte startAddress, uint byteCount)
    {
        MemoryMarshal.CreateSpan<byte>(ref startAddress, (int)byteCount).Clear();
    }
    /// <summary>
    /// Allocates an uninitialized block of unmanaged memory aligned to the requested boundary.
    /// </summary>
    /// <param name="sizeInBytes">The size of the memory block to allocate, in bytes.</param>
    /// <param name="alignment">
    /// The alignment of the allocation, in bytes. Must be a positive power of two. Defaults to 16.
    /// </param>
    /// <returns>The base address of the allocated, aligned memory block.</returns>
    /// <remarks>
    /// Useful for SIMD workloads and interop scenarios that require aligned buffers. The returned
    /// block must be released with <see cref="FreeAligned(IntPtr)"/> (not <see cref="Free(IntPtr)"/>)
    /// to avoid undefined behavior.
    /// </remarks>
    public static unsafe IntPtr AllocateAligned(int sizeInBytes, int alignment = 16)
    {
        var ptr = NativeMemory.AlignedAlloc((nuint)sizeInBytes, (nuint)alignment);
        return (IntPtr)ptr;
    }
    /// <summary>
    /// Allocates an aligned block of unmanaged memory and initializes all of its bytes to zero.
    /// </summary>
    /// <param name="sizeInBytes">The size of the memory block to allocate, in bytes.</param>
    /// <param name="alignment">
    /// The alignment of the allocation, in bytes. Must be a positive power of two. Defaults to 16.
    /// </param>
    /// <returns>The base address of the allocated, aligned, zero-filled memory block.</returns>
    /// <remarks>
    /// Semantically equivalent to calling <see cref="AllocateAligned(int, int)"/> followed by
    /// <see cref="Clear(nint, uint)"/>, but expressed as a single operation for convenience.
    /// The block must be released with <see cref="FreeAligned(IntPtr)"/>.
    /// </remarks>
    public static unsafe IntPtr AllocateAlignedZeroed(int sizeInBytes, int alignment = 16)
    {
        var ptr = NativeMemory.AlignedAlloc((nuint)sizeInBytes, (nuint)alignment);
        new Span<byte>(ptr, sizeInBytes).Clear();
        return (IntPtr)ptr;
    }
    /// <summary>
    /// Releases an aligned memory block previously obtained from
    /// <see cref="AllocateAligned(int, int)"/> or <see cref="AllocateAlignedZeroed(int, int)"/>.
    /// </summary>
    /// <param name="ptr">The base address of the aligned memory block to free.</param>
    /// <remarks>
    /// Only pointers produced by the aligned allocation helpers may be passed here; freeing a block
    /// that was allocated with <see cref="Allocate(int)"/> (or vice versa) results in undefined behavior.
    /// </remarks>
    public static unsafe void FreeAligned(IntPtr ptr)
    {
        NativeMemory.AlignedFree((void*)ptr);
    }
    /// <summary>
    /// Allocates an uninitialized block of unmanaged memory of the requested size.
    /// </summary>
    /// <param name="size">The size of the memory block to allocate, in bytes.</param>
    /// <returns>The base address of the allocated memory block.</returns>
    /// <remarks>
    /// The contents of the returned block are indeterminate. Release it with
    /// <see cref="Free(IntPtr)"/> when it is no longer needed.
    /// </remarks>
    public static unsafe IntPtr Allocate(int size)
    {
        return (IntPtr)NativeMemory.Alloc((nuint)size);
    }
    /// <summary>
    /// Allocates a block of unmanaged memory of the requested size with all bytes set to zero.
    /// </summary>
    /// <param name="size">The size of the memory block to allocate, in bytes.</param>
    /// <returns>The base address of the allocated, zero-initialized memory block.</returns>
    /// <remarks>
    /// Prefer this over <see cref="Allocate(int)"/> plus manual clearing, since the underlying
    /// allocator can often provide zeroed pages without an explicit write pass.
    /// The block must be released with <see cref="Free(IntPtr)"/>.
    /// </remarks>
    public static unsafe IntPtr AllocateZeroed(int size)
    {
        return (IntPtr)NativeMemory.AllocZeroed((nuint)size);
    }
    /// <summary>
    /// Releases an unmanaged memory block previously obtained from <see cref="Allocate(int)"/>
    /// or <see cref="AllocateZeroed(int)"/>.
    /// </summary>
    /// <param name="ptr">The base address of the memory block to free.</param>
    /// <remarks>
    /// Aligned allocations made through <see cref="AllocateAligned(int, int)"/> or
    /// <see cref="AllocateAlignedZeroed(int, int)"/> must instead be released with
    /// <see cref="FreeAligned(IntPtr)"/>.
    /// </remarks>
    public static unsafe void Free(IntPtr ptr)
    {
        NativeMemory.Free((void*)ptr);
    }
    /// <summary>
    /// Allocates an uninitialized block of unmanaged memory large enough to hold a given number of
    /// elements of an unmanaged type and exposes it as a <see cref="Span{T}"/>.
    /// </summary>
    /// <typeparam name="T">The unmanaged element type of the buffer.</typeparam>
    /// <param name="count">The number of elements of type <typeparamref name="T"/> to allocate.</param>
    /// <returns>A span over the newly allocated, uninitialized unmanaged buffer.</returns>
    /// <remarks>
    /// The span is merely a view over native memory: it is not tracked by the garbage collector and
    /// will not be freed automatically. Pass the resulting span to <see cref="Free{T}(ReadOnlySpan{T})"/>
    /// once you are done with it.
    /// </remarks>
    public static unsafe Span<T> Allocate<T>(int count) where T : unmanaged
    {
        var ptr = NativeMemory.Alloc((nuint)(count * sizeof(T)));
        return new Span<T>(ptr, count);
    }
    /// <summary>
    /// Allocates a zero-initialized block of unmanaged memory for a given number of elements of an
    /// unmanaged type and exposes it as a <see cref="Span{T}"/>.
    /// </summary>
    /// <typeparam name="T">The unmanaged element type of the buffer.</typeparam>
    /// <param name="count">The number of elements of type <typeparamref name="T"/> to allocate.</param>
    /// <returns>A span over the newly allocated, zero-filled unmanaged buffer.</returns>
    /// <remarks>
    /// Like <see cref="Allocate{T}(int)"/>, the returned span views unmanaged memory that must be
    /// explicitly released with <see cref="Free{T}(ReadOnlySpan{T})"/>.
    /// </remarks>
    public static unsafe Span<T> AllocatedZeroed<T>(int count) where T : unmanaged
    {
        var ptr = NativeMemory.AllocZeroed((nuint)(sizeof(T) * count));
        return new Span<T>(ptr, count);
    }
    /// <summary>
    /// Releases the unmanaged memory backing a span that was produced by
    /// <see cref="Allocate{T}(int)"/> or <see cref="AllocatedZeroed{T}(int)"/>.
    /// </summary>
    /// <typeparam name="T">The unmanaged element type of the buffer being freed.</typeparam>
    /// <param name="span">The span whose underlying memory block should be freed.</param>
    /// <remarks>
    /// The span must point at the base of a heap allocation owned by the caller. Freeing a span that
    /// views managed, stack, or borrowed memory results in undefined behavior. After this call the
    /// span (and any copies of it) must not be used.
    /// </remarks>
    public static unsafe void Free<T>(ReadOnlySpan<T> span) where T : unmanaged
    {
        fixed (T* ptr = span)
        {
            NativeMemory.Free(ptr);
        }
    }
    /// <summary>
    /// Promotes an unmanaged memory block to a managed array allocated on the Pinned Object Heap
    /// (POH) by copying its contents, then frees the original block.
    /// </summary>
    /// <typeparam name="T">The unmanaged element type stored in the block.</typeparam>
    /// <param name="ptr">The base address of the unmanaged memory block to promote.</param>
    /// <param name="size">The size of the memory block, in bytes. Must be an exact multiple of <c>sizeof(T)</c>.</param>
    /// <param name="aligned">
    /// <see langword="true"/> if the block was produced by an aligned allocator (it will be released
    /// with <see cref="FreeAligned(IntPtr)"/>); <see langword="false"/> for blocks from
    /// <see cref="Allocate(int)"/>/<see cref="AllocateZeroed(int)"/>. Defaults to <see langword="false"/>.
    /// </param>
    /// <returns>A POH-backed array containing a copy of the unmanaged block's data.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="size"/> is not evenly divisible by the size of <typeparamref name="T"/>.
    /// </exception>
    /// <remarks>
    /// POH arrays are pinned and are only reclaimed during generation&nbsp;2 collections, so promoting
    /// short-lived buffers through this method can cause significant memory fragmentation and
    /// pressure. Reserve it for buffers that will live for the lifetime of the process or close to it.
    /// </remarks>
    public static unsafe T[] PromoteToPinnedObjectHeap<T>(IntPtr ptr, int size, bool aligned = false) where T : unmanaged
    {
        if (size % sizeof(T) != 0)
        {
            ThrowWrongSizeExecption();
        }
        var span = new ReadOnlySpan<T>((void*)ptr, size / sizeof(T));
        T[] promotedArray = GC.AllocateUninitializedArray<T>(span.Length, pinned: true);
        span.CopyTo(promotedArray);
        if (aligned)
        {
            FreeAligned(ptr);
        }
        else
        {
            Free(span);
        }
        return promotedArray;
    }
    /// <summary>
    /// Creates a new array on the Pinned Object Heap (POH) containing a copy of the given span.
    /// </summary>
    /// <typeparam name="T">The unmanaged element type of the source data.</typeparam>
    /// <param name="span">The memory region whose contents are copied into the POH array.</param>
    /// <returns>A pinned-array copy of <paramref name="span"/>.</returns>
    /// <remarks>
    /// Unlike the <c>PromoteToPinnedObjectHeap</c> overloads, this method does <b>not</b> free the
    /// source memory, making it appropriate for managed or stack-allocated buffers. The resulting
    /// array lives on the POH, so — as with all POH allocations — it is best suited for long-lived data.
    /// </remarks>
    public static T[] CreatePinnedObjectHeapCopy<T>(ReadOnlySpan<T> span) where T : unmanaged
    {
        var array = GC.AllocateUninitializedArray<T>(span.Length, pinned: true);
        span.CopyTo(array);
        return array;
    }
    /// <summary>
    /// Promotes an unmanaged buffer to a new array on the Pinned Object Heap (POH) by copying its
    /// contents, then frees the underlying memory block.
    /// </summary>
    /// <typeparam name="T">The unmanaged element type of the buffer.</typeparam>
    /// <param name="span">The span over the unmanaged memory block to promote; it is invalid after this call.</param>
    /// <param name="aligned">
    /// <see langword="true"/> if the block was allocated with an aligned allocator (freed via
    /// <see cref="FreeAligned(IntPtr)"/>); <see langword="false"/> to free it via <see cref="Free(IntPtr)"/>.
    /// </param>
    /// <returns>A POH-backed, uninitialized array holding a copy of the span's contents.</returns>
    /// <remarks>
    /// Use this overload when the buffer was obtained as a span (for example from
    /// <see cref="Allocate{T}(int)"/>) rather than as a raw pointer. Because the target array is
    /// allocated uninitialized and fully overwritten by the copy, there is no zeroing cost — but the
    /// POH lifetime caveats (gen-2-only collection, fragmentation risk for short-lived data) still apply.
    /// </remarks>
    public static unsafe T[] PromoteToPinnedObjectHeap<T>(ReadOnlySpan<T> span, bool aligned) where T : unmanaged
    {
        var array = GC.AllocateUninitializedArray<T>(span.Length, pinned: true);
        span.CopyTo(array);
        fixed (T* ptr = span)
        {
            var intPtr = (IntPtr)ptr;
            if (aligned)
            {
                FreeAligned(intPtr);
            }
            else
            {
                Free(intPtr);
            }
        }
        return array;
    }
    /// <summary>
    /// Allocates an uninitialized array directly on the Pinned Object Heap (POH).
    /// </summary>
    /// <typeparam name="T">The element type of the array.</typeparam>
    /// <param name="length">The number of elements in the array.</param>
    /// <returns>A pinned array whose elements contain indeterminate values.</returns>
    /// <remarks>
    /// The array is not zeroed, so every element must be written before it is read. Since POH
    /// objects are only collected during generation&nbsp;2 GCs, this allocation should be long lived;
    /// using it for transient data can lead to heavy fragmentation and elevated memory usage.
    /// </remarks>
    public static T[] AllocateUninitializedLongLivedPinnedArray<T>(int length) where T : unmanaged
    {
        return GC.AllocateUninitializedArray<T>(length, pinned: true);
    }
    /// <summary>
    /// Allocates a zero-initialized array on the Pinned Object Heap (POH).
    /// </summary>
    /// <typeparam name="T">The element type of the array.</typeparam>
    /// <param name="length">The number of elements in the array.</param>
    /// <returns>A pinned array with all elements initialized to their default values.</returns>
    /// <remarks>
    /// All POH allocations are reclaimed only by generation&nbsp;2 collections, so arrays created here
    /// should be long lived. Short-lived POH allocations tend to survive many collections, causing
    /// fragmentation, elevated memory usage, and more frequent/expensive gen-2 GCs that degrade performance.
    /// </remarks>
    public static T[] AllocateLongLivedPinnedArray<T>(int length) where T : unmanaged
    {
        return GC.AllocateArray<T>(length, pinned: true);
    }

    /// <summary>
    /// Throws an <see cref="ArgumentException"/> indicating that a supplied byte size is not a
    /// whole multiple of the element size of the target type.
    /// </summary>
    /// <param name="name">
    /// The name of the calling member, supplied automatically via
    /// <see cref="CallerMemberNameAttribute"/>, used as the offending parameter name in the exception.
    /// </param>
    /// <exception cref="ArgumentException">Always thrown.</exception>
    /// <remarks>
    /// Kept as a separate no-inline-friendly helper so the failure path stays out of the hot path of
    /// callers such as <see cref="PromoteToPinnedObjectHeap{T}(IntPtr, int, bool)"/>.
    /// </remarks>
    private static void ThrowWrongSizeExecption([CallerMemberName] string name = "")
    {
        throw new ArgumentException("The specified size is not dividable by the type size", name);
    }
}
