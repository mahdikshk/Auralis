using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Auralis.Core;
/// <summary>
/// Provides the utility methods for memory management
/// </summary>
public static partial class MemoryUtilities
{
    private static readonly bool _isUnalignedSafe;
    /// <summary>
    /// Determines if the current processor supports unaligned memory manipulation and stores it in an static readonly field
    /// so the jit will treat it as a constant value and optimizes the codes accordingly
    /// </summary>
    static MemoryUtilities()
    {
        _isUnalignedSafe =
        RuntimeInformation.ProcessArchitecture is Architecture.X64 or Architecture.X86 or Architecture.Arm64;
    }
    /// <summary>
    /// Copies bytes from the source to the destination
    /// </summary>
    /// <param name="destination">The destination memory address to get copied</param>
    /// <param name="source">The source memory reference to get copied</param>
    /// <param name="byteCount">The number of bytes to copy</param>
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
    /// Copies from the source address to the destination address
    /// </summary>
    /// <param name="destination">The destination address to get copied</param>
    /// <param name="source">The source address to get copied</param>
    /// <param name="byteCount">The number of bytes to copy</param>
    public static unsafe void CopyWithAlignmentFallback(nint source, nint destination, uint byteCount)
    {
        if (_isUnalignedSafe)
        {
            Buffer.MemoryCopy((void*)source, (void*)destination, byteCount, byteCount);
        }
        Unsafe.CopyBlockUnaligned((void*)destination, (void*)source, byteCount);
    }
    /// <summary>
    /// Clears a block of memory
    /// </summary>
    /// <param name="startAddress">The start of the block of memory pointer to get cleared</param>
    /// <param name="byteCount">The size of the block of memory to get cleared</param>
    public static unsafe void Clear(void* startAddress, uint byteCount)
    {
        new Span<byte>(startAddress, (int)byteCount).Clear();
    }
    /// <summary>
    /// Clears a block of memory
    /// </summary>
    /// <param name="startAddress">The start of the block of memory pointer to get cleared</param>
    /// <param name="byteCount">The size of the block of memory to get cleared</param>
    public static unsafe void Clear(nint startAddress, uint byteCount)
    {
        new Span<byte>((void*)startAddress, (int)byteCount).Clear();
    }
    /// <summary>
    /// Clears a block of memory
    /// </summary>
    /// <param name="startAddress">The start of the block of memory reference to get cleared</param>
    /// <param name="byteCount">The size of the block of memory to get cleared</param>
    public static unsafe void Clear(ref byte startAddress, uint byteCount)
    {
        MemoryMarshal.CreateSpan<byte>(ref startAddress, (int)byteCount).Clear();
    }
    /// <summary>
    /// Allocates an aligned memory block with the requested size
    /// </summary>
    /// <param name="sizeInBytes">The size of memory block in bytes</param>
    /// <param name="alignment">
    /// The memory alignment. It must be a positive power of two value.
    /// Defaults to 16 bytes
    /// </param>
    /// <returns>The memory address where the block of memory with the requested size is allocated</returns>
    public static unsafe IntPtr AllocateAligned(int sizeInBytes, int alignment = 16)
    {
        var ptr = NativeMemory.AlignedAlloc((nuint)sizeInBytes, (nuint)alignment);
        return (IntPtr)ptr;
    }
    /// <summary>
    /// Allocates an aligned memory block with the requested size and zeroes it out
    /// </summary>
    /// <param name="sizeInBytes">The size of the requested memory block</param>
    /// <param name="alignment">
    /// The memory alignment. It must be a positive power of two value.
    /// Defaults to 16 bytes
    /// </param>
    /// <returns>The memory address where the block of memory with the requested size is allocated</returns>
    public static unsafe IntPtr AllocateAlignedZeroed(int sizeInBytes, int alignment = 16)
    {
        var ptr = NativeMemory.AlignedAlloc((nuint)sizeInBytes, (nuint)alignment);
        new Span<byte>(ptr, sizeInBytes).Clear();
        return (IntPtr)ptr;
    }
    /// <summary>
    /// Frees the aligned memory allocated with <see cref="AllocateAligned(int, int)"/> or <see cref="AllocateAlignedZeroed(int, int)"/> />
    /// </summary>
    /// <param name="ptr">The aligned memory block to be freed</param>
    public static unsafe void FreeAligned(IntPtr ptr)
    {
        NativeMemory.AlignedFree((void*)ptr);
    }
    /// <summary>
    /// Allocate a memory block with the requested size
    /// </summary>
    /// <param name="size">The size of the requested memory block in bytes</param>
    /// <returns>The memory address where the block of memory with the requested size is allocated</returns>
    public static unsafe IntPtr Allocate(int size)
    {
        return (IntPtr)NativeMemory.Alloc((nuint)size);
    }
    /// <summary>
    /// Allocates a zeroed memory block with the requested size
    /// </summary>
    /// <param name="size">The size of the memory block to be allocate</param>
    /// <returns>The allocated memory block address</returns>
    public static unsafe IntPtr AllocateZeroed(int size)
    {
        return (IntPtr)NativeMemory.AllocZeroed((nuint)size);
    }
    /// <summary>
    /// Frees an allocated memory block using <see cref="Allocate(int)"/>
    /// </summary>
    /// <param name="ptr">The memory address of the memory block that's going to get freed</param>
    public static unsafe void Free(IntPtr ptr)
    {
        NativeMemory.Free((void*)ptr);
    }
    /// <summary>
    /// Allocates a memory block with the requested size and wraps it in a span
    /// </summary>
    /// <typeparam name="T">The type of the unmanaged struct to be allocated</typeparam>
    /// <param name="count">The numbers of elements that should be allocated</param>
    /// <returns>The allocated memory block span</returns>
    /// <remarks>The span contains an unmananged memory block that should be freed later</remarks>
    public static unsafe Span<T> Allocate<T>(int count) where T : unmanaged
    {
        var ptr = NativeMemory.Alloc((nuint)(count * sizeof(T)));
        return new Span<T>(ptr, count);
    }
    /// <summary>
    /// Allocates a zeroed memory block with the requested size
    /// </summary>
    /// <param name="size">The element counts of the unmanaged memory block</param>
    /// <returns>The allocated memory block wrapped in an span</returns>
    public static unsafe Span<T> AllocatedZeroed<T>(int count) where T : unmanaged
    {
        var ptr = NativeMemory.AllocZeroed((nuint)(sizeof(T) * count));
        return new Span<T>(ptr, count);
    }
    /// <summary>
    /// Frees and allocated memory block using <see cref="Allocate{T}(int)"/>
    /// </summary>
    /// <typeparam name="T">The type of the unmanaged struct to be freed</typeparam>
    /// <param name="span">The memory block that is going to be freed</param>
    public static unsafe void Free<T>(ReadOnlySpan<T> span) where T : unmanaged
    {
        fixed (T* ptr = span)
        {
            NativeMemory.Free(ptr);
        }
    }
    /// <summary>
    /// Allocates an array on the pinned object heap (POH) and copies the contents of the unmanaged block of memory to that array and then frees it
    /// </summary>
    /// <typeparam name="T">The unmanaged type which gets promoted to the POH</typeparam>
    /// <param name="ptr">The memory address of the memory block which is getting promoted</param>
    /// <param name="size">The size of the unmanaged memory block</param>
    /// <returns>An array which is allocated on the pinned object heap</returns>
    /// <remarks>The allocated array is only collected with gen 2 GCs and causes high memory fragmentation if used for short lived allocations so don't abuse this and only promote the long lived memory blocks</remarks>
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
    /// Allocates a POH array and copies the contents of the given span to it
    /// </summary>
    /// <typeparam name="T">The unmanaged type</typeparam>
    /// <param name="span">The unmanaged memory block to be copied into an POH array</param>
    /// <returns>A copy of the given span in the POH</returns>
    /// <remarks>This method should only be used with managed or stack allocated memory buffers and doesn't free the span</remarks>
    public static T[] CreatePinnedObjectHeapCopy<T>(ReadOnlySpan<T> span) where T : unmanaged
    {
        var array = GC.AllocateUninitializedArray<T>(span.Length, pinned: true);
        span.CopyTo(array);
        return array;
    }
    /// <summary>
    /// Allocates a new POH array and copies the span into it and frees the unmanaged memory
    /// </summary>
    /// <typeparam name="T">The unmanaged type</typeparam>
    /// <param name="span">The memory block that is going to be promoted to POH</param>
    /// <param name="aligned">Is the allocated memory block aligned</param>
    /// <returns>A POH array with the contents of the given span</returns>
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
    /// Allocates an uninitialized array on Pinned Object Heap(POH)
    /// </summary>
    /// <typeparam name="T">The type of the elements</typeparam>
    /// <param name="length">The length of the allocated array</param>
    /// <returns>An uninitialized array allocated on the POH</returns>
    /// <remarks>Since this array is allocated on the POH, it should be really long lived otherwise it may cause high memory fragmentation or high memory usage
    /// This array isn't zeroed so be careful when using it
    /// </remarks>
    public static T[] AllocateUninitializedLongLivedPinnedArray<T>(int length) where T : unmanaged
    {
        return GC.AllocateUninitializedArray<T>(length, pinned: true);
    }
    /// <summary>
    /// Allocates a managed array on the Pinned Object Heap (POH)
    /// </summary>
    /// <typeparam name="T">The type of the elements of the array</typeparam>
    /// <param name="length">The length of the array</param>
    /// <returns>An array allocated on the POH</returns>
    /// <remarks>
    /// Since this array is allocated on the POH and because all POH allocations are only collected with Gen2 GCs, all allocations using this method should be long lived
    /// Any short lived allocation may cause high memory fragmentation and high memory usage and will lead to high Gen2 GCs causing performance degradation
    /// </remarks>
    public static T[] AllocateLongLivedPinnedArray<T>(int length) where T : unmanaged
    {
        return GC.AllocateArray<T>(length, pinned: true);
    }

    private static void ThrowWrongSizeExecption([CallerMemberName] string name = "")
    {
        throw new ArgumentException("The specified size is not dividable by the type size", name);
    }
}
