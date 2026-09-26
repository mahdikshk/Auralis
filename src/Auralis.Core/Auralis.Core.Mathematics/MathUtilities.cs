using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Text;

namespace Auralis.Core.Mathematics;

public static class MathUtilities
{
    public const float ZeroTolerance = 1e-6f; // Value a 8x higher than 1.19209290E-07F
    public const float PiOverTwo = (float)(Math.PI / 2);
    public const double ZeroToleranceDouble = double.Epsilon * 8;

    /// <summary>
    /// Determines whether the specified value is close to zero (0.0f).
    /// </summary>
    /// <param name="a">The floating value.</param>
    /// <returns><c>true</c> if the specified value is close to zero (0.0f); otherwise, <c>false</c>.</returns>
    public static bool IsZero(double a)
    {
        return Math.Abs(a) < ZeroToleranceDouble;
    }

    /// <summary>
    /// Determines whether the specified value is close to one (1.0f).
    /// </summary>
    /// <param name="a">The floating value.</param>
    /// <returns><c>true</c> if the specified value is close to one (1.0f); otherwise, <c>false</c>.</returns>
    public static bool IsOne(float a)
    {
        return IsZero(a - 1.0f);
    }

    /// <summary>
    /// Performs an element-wise multiplication of integers from two source spans and stores the results in a destination span (destination[i] = first[i] * second[i]). The method automatically selects the widest available SIMD width (Vector512, Vector256 or Vector128) when hardware acceleration is enabled and the input is large enough, falling back to a scalar loop for the remaining elements.
    /// </summary>
    /// <param name="first">The first read-only span of integers (left operand).</param>
    /// <param name="second">The second read-only span of integers (right operand).</param>
    /// <param name="destination">The span that receives the result of the multiplication. Must have the same length as <paramref name="first"/> and <paramref name="second"/>.</param>
    /// <exception cref="ArgumentException">Thrown when the lengths of <paramref name="first"/>, <paramref name="second"/> and <paramref name="destination"/> are not identical.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ElementWiseMultiplication(ReadOnlySpan<int> first, ReadOnlySpan<int> second, Span<int> destination)
    {
        if (first.Length != second.Length || first.Length != destination.Length)
            ThrowIfLengthsAreNotTheSame();
        int length = first.Length;
        ref int currentfirst = ref MemoryMarshal.GetReference(first);
        ref int currentsecond = ref MemoryMarshal.GetReference(second);
        ref int dest = ref MemoryMarshal.GetReference(destination);
        if (Vector512.IsHardwareAccelerated && first.Length >= Vector512<int>.Count)
        {
            var count = Vector512<int>.Count;
            ref int end = ref Unsafe.Add(ref currentfirst, first.Length - (length % count));
            int remaining = length % count;
            do
            {
                var v1 = Vector512.LoadUnsafe(ref currentfirst);
                var v2 = Vector512.LoadUnsafe(ref currentsecond);
                (v1 * v2).StoreUnsafe(ref dest);
                currentfirst = ref Unsafe.Add(ref currentfirst, count);
                currentsecond = ref Unsafe.Add(ref currentsecond, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref currentfirst, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = first[i] * second[i];
            }
            return;
        }
        else if (Vector256.IsHardwareAccelerated && first.Length >= Vector256<int>.Count)
        {
            var count = Vector256<int>.Count;
            ref int end = ref Unsafe.Add(ref currentfirst, first.Length - (length % count));
            int remaining = length % count;
            do
            {
                var v1 = Vector256.LoadUnsafe(ref currentfirst);
                var v2 = Vector256.LoadUnsafe(ref currentsecond);
                (v1 * v2).StoreUnsafe(ref dest);
                currentfirst = ref Unsafe.Add(ref currentfirst, count);
                currentsecond = ref Unsafe.Add(ref currentsecond, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref currentfirst, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = first[i] * second[i];
            }
            return;
        }
        else if (Vector128.IsHardwareAccelerated && length >= Vector128<int>.Count)
        {
            var count = Vector128<int>.Count;
            ref int end = ref Unsafe.Add(ref currentfirst, first.Length - (length % count));
            int remaining = length % count;
            do
            {
                var v1 = Vector128.LoadUnsafe(ref currentfirst);
                var v2 = Vector128.LoadUnsafe(ref currentsecond);
                (v1 * v2).StoreUnsafe(ref dest);
                currentfirst = ref Unsafe.Add(ref currentfirst, count);
                currentsecond = ref Unsafe.Add(ref currentsecond, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref currentfirst, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = first[i] * second[i];
            }
            return;
        }
        for (int i = 0; i < first.Length; i++)
        {
            destination[i] = first[i] * second[i];
        }
    }
    /// <summary>
    /// Performs an element-wise multiplication of 64-bit integers from two source spans and stores the results in a destination span (destination[i] = first[i] * second[i]). The method automatically selects the widest available SIMD width (Vector512, Vector256 or Vector128) when hardware acceleration is enabled and the input is large enough, falling back to a scalar loop for the remaining elements.
    /// </summary>
    /// <param name="first">The first read-only span of 64-bit integers (left operand).</param>
    /// <param name="second">The second read-only span of 64-bit integers (right operand).</param>
    /// <param name="destination">The span that receives the result of the multiplication. Must have the same length as <paramref name="first"/> and <paramref name="second"/>.</param>
    /// <exception cref="ArgumentException">Thrown when the lengths of <paramref name="first"/>, <paramref name="second"/> and <paramref name="destination"/> are not identical.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ElementWiseMultiplication(ReadOnlySpan<long> first, ReadOnlySpan<long> second, Span<long> destination)
    {
        if (first.Length != second.Length || first.Length != destination.Length)
            ThrowIfLengthsAreNotTheSame();
        int length = first.Length;
        ref long currentfirst = ref MemoryMarshal.GetReference(first);
        ref long currentsecond = ref MemoryMarshal.GetReference(second);
        ref long dest = ref MemoryMarshal.GetReference(destination);
        if (Vector512.IsHardwareAccelerated && first.Length >= Vector512<long>.Count)
        {
            var count = Vector512<long>.Count;
            ref long end = ref Unsafe.Add(ref currentfirst, first.Length - (length % count));
            int remaining = length % count;
            do
            {
                var v1 = Vector512.LoadUnsafe(ref currentfirst);
                var v2 = Vector512.LoadUnsafe(ref currentsecond);
                (v1 * v2).StoreUnsafe(ref dest);
                currentfirst = ref Unsafe.Add(ref currentfirst, count);
                currentsecond = ref Unsafe.Add(ref currentsecond, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref currentfirst, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = first[i] * second[i];
            }
            return;
        }
        else if (Vector256.IsHardwareAccelerated && first.Length >= Vector256<long>.Count)
        {
            var count = Vector256<long>.Count;
            ref long end = ref Unsafe.Add(ref currentfirst, first.Length - (length % count));
            int remaining = length % count;
            do
            {
                var v1 = Vector256.LoadUnsafe(ref currentfirst);
                var v2 = Vector256.LoadUnsafe(ref currentsecond);
                (v1 * v2).StoreUnsafe(ref dest);
                currentfirst = ref Unsafe.Add(ref currentfirst, count);
                currentsecond = ref Unsafe.Add(ref currentsecond, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref currentfirst, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = first[i] * second[i];
            }
            return;
        }
        else if (Vector128.IsHardwareAccelerated && length >= Vector128<long>.Count)
        {
            var count = Vector128<long>.Count;
            ref long end = ref Unsafe.Add(ref currentfirst, first.Length - (length % count));
            int remaining = length % count;
            do
            {
                var v1 = Vector128.LoadUnsafe(ref currentfirst);
                var v2 = Vector128.LoadUnsafe(ref currentsecond);
                (v1 * v2).StoreUnsafe(ref dest);
                currentfirst = ref Unsafe.Add(ref currentfirst, count);
                currentsecond = ref Unsafe.Add(ref currentsecond, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref currentfirst, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = first[i] * second[i];
            }
            return;
        }
        for (int i = 0; i < first.Length; i++)
        {
            destination[i] = first[i] * second[i];
        }
    }
    /// <summary>
    /// Performs an element-wise multiplication of 16-bit integers from two source spans and stores the results in a destination span (destination[i] = first[i] * second[i]). The method automatically selects the widest available SIMD width (Vector512, Vector256 or Vector128) when hardware acceleration is enabled and the input is large enough, falling back to a scalar loop for the remaining elements.
    /// </summary>
    /// <param name="first">The first read-only span of 16-bit integers (left operand).</param>
    /// <param name="second">The second read-only span of 16-bit integers (right operand).</param>
    /// <param name="destination">The span that receives the result of the multiplication. Must have the same length as <paramref name="first"/> and <paramref name="second"/>.</param>
    /// <exception cref="ArgumentException">Thrown when the lengths of <paramref name="first"/>, <paramref name="second"/> and <paramref name="destination"/> are not identical.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ElementWiseMultiplication(ReadOnlySpan<short> first, ReadOnlySpan<short> second, Span<short> destination)
    {
        if (first.Length != second.Length || first.Length != destination.Length)
            ThrowIfLengthsAreNotTheSame();
        int length = first.Length;
        ref short currentfirst = ref MemoryMarshal.GetReference(first);
        ref short currentsecond = ref MemoryMarshal.GetReference(second);
        ref short dest = ref MemoryMarshal.GetReference(destination);
        if (Vector512.IsHardwareAccelerated && first.Length >= Vector512<short>.Count)
        {
            var count = Vector512<short>.Count;
            ref short end = ref Unsafe.Add(ref currentfirst, length - (length % count));
            int remaining = length % count;
            do
            {
                var v1 = Vector512.LoadUnsafe(ref currentfirst);
                var v2 = Vector512.LoadUnsafe(ref currentsecond);
                (v1 * v2).StoreUnsafe(ref dest);
                currentfirst = ref Unsafe.Add(ref currentfirst, count);
                currentsecond = ref Unsafe.Add(ref currentsecond, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref currentfirst, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = (short)(first[i] * second[i]);
            }
            return;
        }
        else if (Vector256.IsHardwareAccelerated && first.Length >= Vector256<short>.Count)
        {
            var count = Vector256<short>.Count;
            ref short end = ref Unsafe.Add(ref currentfirst, first.Length - (length % count));
            int remaining = length % count;
            do
            {
                var v1 = Vector256.LoadUnsafe(ref currentfirst);
                var v2 = Vector256.LoadUnsafe(ref currentsecond);
                (v1 * v2).StoreUnsafe(ref dest);
                currentfirst = ref Unsafe.Add(ref currentfirst, count);
                currentsecond = ref Unsafe.Add(ref currentsecond, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref currentfirst, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = (short)(first[i] * second[i]);
            }
            return;
        }
        else if (Vector128.IsHardwareAccelerated && length >= Vector128<short>.Count)
        {
            var count = Vector128<short>.Count;
            ref short end = ref Unsafe.Add(ref currentfirst, length - (length % count));
            int remaining = length % Vector128<short>.Count;
            do
            {
                var v1 = Vector128.LoadUnsafe(ref currentfirst);
                var v2 = Vector128.LoadUnsafe(ref currentsecond);
                (v1 * v2).StoreUnsafe(ref dest);
                currentfirst = ref Unsafe.Add(ref currentfirst, count);
                currentsecond = ref Unsafe.Add(ref currentsecond, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref currentfirst, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = (short)(first[i] * second[i]);
            }
            return;
        }
        for (int i = 0; i < first.Length; i++)
        {
            destination[i] = (short)(first[i] * second[i]);
        }
    }
    /// <summary>
    /// Performs an element-wise multiplication of single-precision floating-point values from two source spans and stores the results in a destination span (destination[i] = first[i] * second[i]). The method automatically selects the widest available SIMD width (Vector512, Vector256 or Vector128) when hardware acceleration is enabled and the input is large enough, falling back to a scalar loop for the remaining elements.
    /// </summary>
    /// <param name="first">The first read-only span of single-precision floating-point values (left operand).</param>
    /// <param name="second">The second read-only span of single-precision floating-point values (right operand).</param>
    /// <param name="destination">The span that receives the result of the multiplication. Must have the same length as <paramref name="first"/> and <paramref name="second"/>.</param>
    /// <exception cref="ArgumentException">Thrown when the lengths of <paramref name="first"/>, <paramref name="second"/> and <paramref name="destination"/> are not identical.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ElementWiseMultiplication(ReadOnlySpan<float> first, ReadOnlySpan<float> second, Span<float> destination)
    {
        if (first.Length != second.Length || first.Length != destination.Length)
            ThrowIfLengthsAreNotTheSame();
        int length = first.Length;
        ref float currentfirst = ref MemoryMarshal.GetReference(first);
        ref float currentsecond = ref MemoryMarshal.GetReference(second);
        ref float dest = ref MemoryMarshal.GetReference(destination);
        if (Vector512.IsHardwareAccelerated && first.Length >= Vector512<float>.Count)
        {
            var count = Vector512<float>.Count;
            ref float end = ref Unsafe.Add(ref currentfirst, length - (length % count));
            int remaining = length % count;
            do
            {
                var v1 = Vector512.LoadUnsafe(ref currentfirst);
                var v2 = Vector512.LoadUnsafe(ref currentsecond);
                (v1 * v2).StoreUnsafe(ref dest);
                currentfirst = ref Unsafe.Add(ref currentfirst, count);
                currentsecond = ref Unsafe.Add(ref currentsecond, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref currentfirst, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = first[i] * second[i];
            }
            return;
        }
        else if (Vector256.IsHardwareAccelerated && length >= Vector256<float>.Count)
        {
            var count = Vector256<float>.Count;
            ref float end = ref Unsafe.Add(ref currentfirst, first.Length - (length % count));
            int remaining = length % count;
            do
            {
                var v1 = Vector256.LoadUnsafe(ref currentfirst);
                var v2 = Vector256.LoadUnsafe(ref currentsecond);
                (v1 * v2).StoreUnsafe(ref dest);
                currentfirst = ref Unsafe.Add(ref currentfirst, count);
                currentsecond = ref Unsafe.Add(ref currentsecond, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref currentfirst, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = first[i] * second[i];
            }
            return;
        }
        else if (Vector128.IsHardwareAccelerated && length >= Vector128<float>.Count)
        {
            var count = Vector128<float>.Count;
            ref float end = ref Unsafe.Add(ref currentfirst, first.Length - (length % count));
            int remaining = length % count;
            do
            {
                var v1 = Vector128.LoadUnsafe(ref currentfirst);
                var v2 = Vector128.LoadUnsafe(ref currentsecond);
                (v1 * v2).StoreUnsafe(ref dest);
                currentfirst = ref Unsafe.Add(ref currentfirst, count);
                currentsecond = ref Unsafe.Add(ref currentsecond, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref currentfirst, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = first[i] * second[i];
            }
            return;
        }
        for (int i = 0; i < first.Length; i++)
        {
            destination[i] = first[i] * second[i];
        }
    }
    /// <summary>
    /// Performs an element-wise multiplication of double-precision floating-point values from two source spans and stores the results in a destination span (destination[i] = first[i] * second[i]). The method automatically selects the widest available SIMD width (Vector512, Vector256 or Vector128) when hardware acceleration is enabled and the input is large enough, falling back to a scalar loop for the remaining elements.
    /// </summary>
    /// <param name="first">The first read-only span of double-precision floating-point values (left operand).</param>
    /// <param name="second">The second read-only span of double-precision floating-point values (right operand).</param>
    /// <param name="destination">The span that receives the result of the multiplication. Must have the same length as <paramref name="first"/> and <paramref name="second"/>.</param>
    /// <exception cref="ArgumentException">Thrown when the lengths of <paramref name="first"/>, <paramref name="second"/> and <paramref name="destination"/> are not identical.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ElementWiseMultiplication(ReadOnlySpan<double> first, ReadOnlySpan<double> second, Span<double> destination)
    {
        if (first.Length != second.Length || first.Length != destination.Length)
            ThrowIfLengthsAreNotTheSame();
        int length = first.Length;
        ref double currentfirst = ref MemoryMarshal.GetReference(first);
        ref double currentsecond = ref MemoryMarshal.GetReference(second);
        ref double dest = ref MemoryMarshal.GetReference(destination);
        if (Vector512.IsHardwareAccelerated && length >= Vector512<double>.Count)
        {
            var count = Vector512<double>.Count;
            ref double end = ref Unsafe.Add(ref currentfirst, length - (length % count));
            int remaining = length % count;
            do
            {
                var v1 = Vector512.LoadUnsafe(ref currentfirst);
                var v2 = Vector512.LoadUnsafe(ref currentsecond);
                (v1 * v2).StoreUnsafe(ref dest);
                currentfirst = ref Unsafe.Add(ref currentfirst, count);
                currentsecond = ref Unsafe.Add(ref currentsecond, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref currentfirst, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = first[i] * second[i];
            }
            return;
        }
        else if (Vector256.IsHardwareAccelerated && length >= Vector256<double>.Count)
        {
            var count = Vector256<double>.Count;
            ref double end = ref Unsafe.Add(ref currentfirst, length - (length % count));
            int remaining = length % count;
            do
            {
                var v1 = Vector256.LoadUnsafe(ref currentfirst);
                var v2 = Vector256.LoadUnsafe(ref currentsecond);
                (v1 * v2).StoreUnsafe(ref dest);
                currentfirst = ref Unsafe.Add(ref currentfirst, count);
                currentsecond = ref Unsafe.Add(ref currentsecond, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref currentfirst, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = first[i] * second[i];
            }
            return;
        }
        else if (Vector128.IsHardwareAccelerated && length >= Vector128<double>.Count)
        {
            var count = Vector128<double>.Count;
            ref double end = ref Unsafe.Add(ref currentfirst, length - (length % count));
            int remaining = length % count;
            do
            {
                var v1 = Vector128.LoadUnsafe(ref currentfirst);
                var v2 = Vector128.LoadUnsafe(ref currentsecond);
                (v1 * v2).StoreUnsafe(ref dest);
                currentfirst = ref Unsafe.Add(ref currentfirst, count);
                currentsecond = ref Unsafe.Add(ref currentsecond, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref currentfirst, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = first[i] * second[i];
            }
            return;
        }
        for (int i = 0; i < first.Length; i++)
        {
            destination[i] = first[i] * second[i];
        }
    }
    /// <summary>
    /// Performs an element-wise addition of integers from two source spans and stores the results in a destination span (destination[i] = first[i] + second[i]). The method automatically selects the widest available SIMD width (Vector512, Vector256 or Vector128) when hardware acceleration is enabled and the input is large enough, falling back to a scalar loop for the remaining elements.
    /// </summary>
    /// <param name="first">The first read-only span of integers (left operand).</param>
    /// <param name="second">The second read-only span of integers (right operand).</param>
    /// <param name="destination">The span that receives the result of the addition. Must have the same length as <paramref name="first"/> and <paramref name="second"/>.</param>
    /// <exception cref="ArgumentException">Thrown when the lengths of <paramref name="first"/>, <paramref name="second"/> and <paramref name="destination"/> are not identical.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ElementWiseAdd(ReadOnlySpan<int> first, ReadOnlySpan<int> second, Span<int> destination)
    {
        if (first.Length != second.Length || first.Length != destination.Length)
            ThrowIfLengthsAreNotTheSame();
        int length = first.Length;
        ref int currentfirst = ref MemoryMarshal.GetReference(first);
        ref int currentsecond = ref MemoryMarshal.GetReference(second);
        ref int dest = ref MemoryMarshal.GetReference(destination);
        if (Vector512.IsHardwareAccelerated && first.Length >= Vector512<int>.Count)
        {
            var count = Vector512<int>.Count;
            ref int end = ref Unsafe.Add(ref currentfirst, length - (length % count));
            int remaining = length % count;
            do
            {
                var v1 = Vector512.LoadUnsafe(ref currentfirst);
                var v2 = Vector512.LoadUnsafe(ref currentsecond);
                (v1 + v2).StoreUnsafe(ref dest);
                currentfirst = ref Unsafe.Add(ref currentfirst, count);
                currentsecond = ref Unsafe.Add(ref currentsecond, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref currentfirst, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = first[i] + second[i];
            }
            return;
        }
        else if (Vector256.IsHardwareAccelerated && first.Length >= Vector256<int>.Count)
        {
            var count = Vector256<int>.Count;
            ref int end = ref Unsafe.Add(ref currentfirst, length - (length % count));
            int remaining = length % count;
            do
            {
                var v1 = Vector256.LoadUnsafe(ref currentfirst);
                var v2 = Vector256.LoadUnsafe(ref currentsecond);
                (v1 + v2).StoreUnsafe(ref dest);
                currentfirst = ref Unsafe.Add(ref currentfirst, count);
                currentsecond = ref Unsafe.Add(ref currentsecond, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref currentfirst, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = first[i] + second[i];
            }
            return;
        }
        else if (Vector128.IsHardwareAccelerated && length >= Vector128<int>.Count)
        {
            var count = Vector128<int>.Count;
            ref int end = ref Unsafe.Add(ref currentfirst, length - (length % count));
            int remaining = length % count;
            do
            {
                var v1 = Vector128.LoadUnsafe(ref currentfirst);
                var v2 = Vector128.LoadUnsafe(ref currentsecond);
                (v1 + v2).StoreUnsafe(ref dest);
                currentfirst = ref Unsafe.Add(ref currentfirst, count);
                currentsecond = ref Unsafe.Add(ref currentsecond, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref currentfirst, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = first[i] + second[i];
            }
            return;
        }
        for (int i = 0; i < first.Length; i++)
        {
            destination[i] = first[i] + second[i];
        }
    }
    /// <summary>
    /// Performs an element-wise addition of 64-bit integers from two source spans and stores the results in a destination span (destination[i] = first[i] + second[i]). The method automatically selects the widest available SIMD width (Vector512, Vector256 or Vector128) when hardware acceleration is enabled and the input is large enough, falling back to a scalar loop for the remaining elements.
    /// </summary>
    /// <param name="first">The first read-only span of 64-bit integers (left operand).</param>
    /// <param name="second">The second read-only span of 64-bit integers (right operand).</param>
    /// <param name="destination">The span that receives the result of the addition. Must have the same length as <paramref name="first"/> and <paramref name="second"/>.</param>
    /// <exception cref="ArgumentException">Thrown when the lengths of <paramref name="first"/>, <paramref name="second"/> and <paramref name="destination"/> are not identical.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ElementWiseAdd(ReadOnlySpan<long> first, ReadOnlySpan<long> second, Span<long> destination)
    {
        if (first.Length != second.Length || first.Length != destination.Length)
            ThrowIfLengthsAreNotTheSame();
        int length = first.Length;
        ref long currentfirst = ref MemoryMarshal.GetReference(first);
        ref long currentsecond = ref MemoryMarshal.GetReference(second);
        ref long dest = ref MemoryMarshal.GetReference(destination);
        if (Vector512.IsHardwareAccelerated && first.Length >= Vector512<long>.Count)
        {
            var count = Vector512<long>.Count;
            ref long end = ref Unsafe.Add(ref currentfirst, length - (length % count));
            int remaining = length % count;
            do
            {
                var v1 = Vector512.LoadUnsafe(ref currentfirst);
                var v2 = Vector512.LoadUnsafe(ref currentsecond);
                (v1 + v2).StoreUnsafe(ref dest);
                currentfirst = ref Unsafe.Add(ref currentfirst, count);
                currentsecond = ref Unsafe.Add(ref currentsecond, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref currentfirst, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = first[i] + second[i];
            }
            return;
        }
        else if (Vector256.IsHardwareAccelerated && first.Length >= Vector256<long>.Count)
        {
            var count = Vector256<long>.Count;
            ref long end = ref Unsafe.Add(ref currentfirst, length - (length % count));
            int remaining = length % count;
            do
            {
                var v1 = Vector256.LoadUnsafe(ref currentfirst);
                var v2 = Vector256.LoadUnsafe(ref currentsecond);
                (v1 + v2).StoreUnsafe(ref dest);
                currentfirst = ref Unsafe.Add(ref currentfirst, count);
                currentsecond = ref Unsafe.Add(ref currentsecond, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref currentfirst, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = first[i] + second[i];
            }
            return;
        }
        else if (Vector128.IsHardwareAccelerated && length >= Vector128<long>.Count)
        {
            var count = Vector128<long>.Count;
            ref long end = ref Unsafe.Add(ref currentfirst, length - (length % count));
            int remaining = length % count;
            do
            {
                var v1 = Vector128.LoadUnsafe(ref currentfirst);
                var v2 = Vector128.LoadUnsafe(ref currentsecond);
                (v1 + v2).StoreUnsafe(ref dest);
                currentfirst = ref Unsafe.Add(ref currentfirst, count);
                currentsecond = ref Unsafe.Add(ref currentsecond, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref currentfirst, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = first[i] + second[i];
            }
            return;
        }
        for (int i = 0; i < first.Length; i++)
        {
            destination[i] = first[i] + second[i];
        }
    }
    /// <summary>
    /// Performs an element-wise addition of 16-bit integers from two source spans and stores the results in a destination span (destination[i] = first[i] + second[i]). The method automatically selects the widest available SIMD width (Vector512, Vector256 or Vector128) when hardware acceleration is enabled and the input is large enough, falling back to a scalar loop for the remaining elements.
    /// </summary>
    /// <param name="first">The first read-only span of 16-bit integers (left operand).</param>
    /// <param name="second">The second read-only span of 16-bit integers (right operand).</param>
    /// <param name="destination">The span that receives the result of the addition. Must have the same length as <paramref name="first"/> and <paramref name="second"/>.</param>
    /// <exception cref="ArgumentException">Thrown when the lengths of <paramref name="first"/>, <paramref name="second"/> and <paramref name="destination"/> are not identical.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ElementWiseAdd(ReadOnlySpan<short> first, ReadOnlySpan<short> second, Span<short> destination)
    {
        if (first.Length != second.Length || first.Length != destination.Length)
            ThrowIfLengthsAreNotTheSame();
        int length = first.Length;
        ref short currentfirst = ref MemoryMarshal.GetReference(first);
        ref short currentsecond = ref MemoryMarshal.GetReference(second);
        ref short dest = ref MemoryMarshal.GetReference(destination);
        if (Vector512.IsHardwareAccelerated && first.Length >= Vector512<short>.Count)
        {
            var count = Vector512<short>.Count;
            ref short end = ref Unsafe.Add(ref currentfirst, length - (length % count));
            int remaining = length % count;
            do
            {
                var v1 = Vector512.LoadUnsafe(ref currentfirst);
                var v2 = Vector512.LoadUnsafe(ref currentsecond);
                (v1 + v2).StoreUnsafe(ref dest);
                currentfirst = ref Unsafe.Add(ref currentfirst, count);
                currentsecond = ref Unsafe.Add(ref currentsecond, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref currentfirst, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = (short)(first[i] + second[i]);
            }
            return;
        }
        else if (Vector256.IsHardwareAccelerated && first.Length >= Vector256<short>.Count)
        {
            var count = Vector256<short>.Count;
            ref short end = ref Unsafe.Add(ref currentfirst, length - (length % count));
            int remaining = length % count;
            do
            {
                var v1 = Vector256.LoadUnsafe(ref currentfirst);
                var v2 = Vector256.LoadUnsafe(ref currentsecond);
                (v1 + v2).StoreUnsafe(ref dest);
                currentfirst = ref Unsafe.Add(ref currentfirst, count);
                currentsecond = ref Unsafe.Add(ref currentsecond, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref currentfirst, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = (short)(first[i] + second[i]);
            }
            return;
        }
        else if (Vector128.IsHardwareAccelerated && length >= Vector128<short>.Count)
        {
            var count = Vector128<short>.Count;
            ref short end = ref Unsafe.Add(ref currentfirst, length - (length % count));
            int remaining = length % count;
            do
            {
                var v1 = Vector128.LoadUnsafe(ref currentfirst);
                var v2 = Vector128.LoadUnsafe(ref currentsecond);
                (v1 + v2).StoreUnsafe(ref dest);
                currentfirst = ref Unsafe.Add(ref currentfirst, count);
                currentsecond = ref Unsafe.Add(ref currentsecond, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref currentfirst, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = (short)(first[i] + second[i]);
            }
            return;
        }
        for (int i = 0; i < first.Length; i++)
        {
            destination[i] = (short)(first[i] + second[i]);
        }
    }
    /// <summary>
    /// Performs an element-wise addition of single-precision floating-point values from two source spans and stores the results in a destination span (destination[i] = first[i] + second[i]). The method automatically selects the widest available SIMD width (Vector512, Vector256 or Vector128) when hardware acceleration is enabled and the input is large enough, falling back to a scalar loop for the remaining elements.
    /// </summary>
    /// <param name="first">The first read-only span of single-precision floating-point values (left operand).</param>
    /// <param name="second">The second read-only span of single-precision floating-point values (right operand).</param>
    /// <param name="destination">The span that receives the result of the addition. Must have the same length as <paramref name="first"/> and <paramref name="second"/>.</param>
    /// <exception cref="ArgumentException">Thrown when the lengths of <paramref name="first"/>, <paramref name="second"/> and <paramref name="destination"/> are not identical.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ElementWiseAdd(ReadOnlySpan<float> first, ReadOnlySpan<float> second, Span<float> destination)
    {
        if (first.Length != second.Length || first.Length != destination.Length)
            ThrowIfLengthsAreNotTheSame();
        int length = first.Length;
        ref float currentfirst = ref MemoryMarshal.GetReference(first);
        ref float currentsecond = ref MemoryMarshal.GetReference(second);
        ref float dest = ref MemoryMarshal.GetReference(destination);
        if (Vector512.IsHardwareAccelerated && first.Length >= Vector512<float>.Count)
        {
            var count = Vector512<float>.Count;
            ref float end = ref Unsafe.Add(ref currentfirst, length - (length % count));
            int remaining = length % count;
            do
            {
                var v1 = Vector512.LoadUnsafe(ref currentfirst);
                var v2 = Vector512.LoadUnsafe(ref currentsecond);
                (v1 + v2).StoreUnsafe(ref dest);
                currentfirst = ref Unsafe.Add(ref currentfirst, count);
                currentsecond = ref Unsafe.Add(ref currentsecond, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref currentfirst, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = first[i] + second[i];
            }
            return;
        }
        else if (Vector256.IsHardwareAccelerated && first.Length >= Vector256<float>.Count)
        {
            var count = Vector256<float>.Count;
            ref float end = ref Unsafe.Add(ref currentfirst, length - (length % count));
            int remaining = length % count;
            do
            {
                var v1 = Vector256.LoadUnsafe(ref currentfirst);
                var v2 = Vector256.LoadUnsafe(ref currentsecond);
                (v1 + v2).StoreUnsafe(ref dest);
                currentfirst = ref Unsafe.Add(ref currentfirst, count);
                currentsecond = ref Unsafe.Add(ref currentsecond, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref currentfirst, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = first[i] + second[i];
            }
            return;
        }
        else if (Vector128.IsHardwareAccelerated && length >= Vector128<float>.Count)
        {
            var count = Vector128<float>.Count;
            ref float end = ref Unsafe.Add(ref currentfirst, length - (length % count));
            int remaining = length % count;
            do
            {
                var v1 = Vector128.LoadUnsafe(ref currentfirst);
                var v2 = Vector128.LoadUnsafe(ref currentsecond);
                (v1 + v2).StoreUnsafe(ref dest);
                currentfirst = ref Unsafe.Add(ref currentfirst, count);
                currentsecond = ref Unsafe.Add(ref currentsecond, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref currentfirst, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = first[i] + second[i];
            }
            return;
        }
        for (int i = 0; i < first.Length; i++)
        {
            destination[i] = first[i] + second[i];
        }
    }
    /// <summary>
    /// Performs an element-wise addition of double-precision floating-point values from two source spans and stores the results in a destination span (destination[i] = first[i] + second[i]). The method automatically selects the widest available SIMD width (Vector512, Vector256 or Vector128) when hardware acceleration is enabled and the input is large enough, falling back to a scalar loop for the remaining elements.
    /// </summary>
    /// <param name="first">The first read-only span of double-precision floating-point values (left operand).</param>
    /// <param name="second">The second read-only span of double-precision floating-point values (right operand).</param>
    /// <param name="destination">The span that receives the result of the addition. Must have the same length as <paramref name="first"/> and <paramref name="second"/>.</param>
    /// <exception cref="ArgumentException">Thrown when the lengths of <paramref name="first"/>, <paramref name="second"/> and <paramref name="destination"/> are not identical.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ElementWiseAdd(ReadOnlySpan<double> first, ReadOnlySpan<double> second, Span<double> destination)
    {
        if (first.Length != second.Length || first.Length != destination.Length)
            ThrowIfLengthsAreNotTheSame();
        int length = first.Length;
        ref double currentfirst = ref MemoryMarshal.GetReference(first);
        ref double currentsecond = ref MemoryMarshal.GetReference(second);
        ref double dest = ref MemoryMarshal.GetReference(destination);
        if (Vector512.IsHardwareAccelerated && first.Length >= Vector512<double>.Count)
        {
            var count = Vector512<double>.Count;
            ref double end = ref Unsafe.Add(ref currentfirst, length - (length % count));
            int remaining = length % count;
            do
            {
                var v1 = Vector512.LoadUnsafe(ref currentfirst);
                var v2 = Vector512.LoadUnsafe(ref currentsecond);
                (v1 + v2).StoreUnsafe(ref dest);
                currentfirst = ref Unsafe.Add(ref currentfirst, count);
                currentsecond = ref Unsafe.Add(ref currentsecond, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref currentfirst, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = first[i] + second[i];
            }
            return;
        }
        else if (Vector256.IsHardwareAccelerated && first.Length >= Vector256<double>.Count)
        {
            var count = Vector256<double>.Count;
            ref double end = ref Unsafe.Add(ref currentfirst, length - (length % count));
            int remaining = length % count;
            do
            {
                var v1 = Vector256.LoadUnsafe(ref currentfirst);
                var v2 = Vector256.LoadUnsafe(ref currentsecond);
                (v1 + v2).StoreUnsafe(ref dest);
                currentfirst = ref Unsafe.Add(ref currentfirst, count);
                currentsecond = ref Unsafe.Add(ref currentsecond, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref currentfirst, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = first[i] + second[i];
            }
            return;
        }
        else if (Vector128.IsHardwareAccelerated && length >= Vector128<double>.Count)
        {
            var count = Vector128<double>.Count;
            ref double end = ref Unsafe.Add(ref currentfirst, length - (length % count));
            int remaining = length % count;
            do
            {
                var v1 = Vector128.LoadUnsafe(ref currentfirst);
                var v2 = Vector128.LoadUnsafe(ref currentsecond);
                (v1 + v2).StoreUnsafe(ref dest);
                currentfirst = ref Unsafe.Add(ref currentfirst, count);
                currentsecond = ref Unsafe.Add(ref currentsecond, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref currentfirst, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = first[i] + second[i];
            }
            return;
        }
        for (int i = 0; i < first.Length; i++)
        {
            destination[i] = first[i] + second[i];
        }
    }
    /// <summary>
    /// Performs an element-wise subtraction of integers from two source spans and stores the results in a destination span (destination[i] = first[i] - second[i]). The method automatically selects the widest available SIMD width (Vector512, Vector256 or Vector128) when hardware acceleration is enabled and the input is large enough, falling back to a scalar loop for the remaining elements.
    /// </summary>
    /// <param name="first">The first read-only span of integers (left operand).</param>
    /// <param name="second">The second read-only span of integers (right operand).</param>
    /// <param name="destination">The span that receives the result of the subtraction. Must have the same length as <paramref name="first"/> and <paramref name="second"/>.</param>
    /// <exception cref="ArgumentException">Thrown when the lengths of <paramref name="first"/>, <paramref name="second"/> and <paramref name="destination"/> are not identical.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ElementWiseSubtract(ReadOnlySpan<int> first, ReadOnlySpan<int> second, Span<int> destination)
    {
        if (first.Length != second.Length || first.Length != destination.Length)
            ThrowIfLengthsAreNotTheSame();
        int length = first.Length;
        ref int currentfirst = ref MemoryMarshal.GetReference(first);
        ref int currentsecond = ref MemoryMarshal.GetReference(second);
        ref int dest = ref MemoryMarshal.GetReference(destination);
        if (Vector512.IsHardwareAccelerated && first.Length >= Vector512<int>.Count)
        {
            var count = Vector512<int>.Count;
            ref int end = ref Unsafe.Add(ref currentfirst, length - (length % count));
            int remaining = length % count;
            do
            {
                var v1 = Vector512.LoadUnsafe(ref currentfirst);
                var v2 = Vector512.LoadUnsafe(ref currentsecond);
                (v1 - v2).StoreUnsafe(ref dest);
                currentfirst = ref Unsafe.Add(ref currentfirst, count);
                currentsecond = ref Unsafe.Add(ref currentsecond, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref currentfirst, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = first[i] - second[i];
            }
            return;
        }
        else if (Vector256.IsHardwareAccelerated && first.Length >= Vector256<int>.Count)
        {
            var count = Vector256<int>.Count;
            ref int end = ref Unsafe.Add(ref currentfirst, first.Length - (length % count));
            int remaining = length % count;
            do
            {
                var v1 = Vector256.LoadUnsafe(ref currentfirst);
                var v2 = Vector256.LoadUnsafe(ref currentsecond);
                (v1 - v2).StoreUnsafe(ref dest);
                currentfirst = ref Unsafe.Add(ref currentfirst, count);
                currentsecond = ref Unsafe.Add(ref currentsecond, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref currentfirst, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = first[i] - second[i];
            }
            return;
        }
        else if (Vector128.IsHardwareAccelerated && length >= Vector128<int>.Count)
        {
            var count = Vector128<int>.Count;
            ref int end = ref Unsafe.Add(ref currentfirst, first.Length - (length % count));
            int remaining = length % count;
            do
            {
                var v1 = Vector128.LoadUnsafe(ref currentfirst);
                var v2 = Vector128.LoadUnsafe(ref currentsecond);
                (v1 - v2).StoreUnsafe(ref dest);
                currentfirst = ref Unsafe.Add(ref currentfirst, count);
                currentsecond = ref Unsafe.Add(ref currentsecond, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref currentfirst, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = first[i] - second[i];
            }
            return;
        }
        for (int i = 0; i < first.Length; i++)
        {
            destination[i] = first[i] - second[i];
        }
    }
    /// <summary>
    /// Performs an element-wise subtraction of 64-bit integers from two source spans and stores the results in a destination span (destination[i] = first[i] - second[i]). The method automatically selects the widest available SIMD width (Vector512, Vector256 or Vector128) when hardware acceleration is enabled and the input is large enough, falling back to a scalar loop for the remaining elements.
    /// </summary>
    /// <param name="first">The first read-only span of 64-bit integers (left operand).</param>
    /// <param name="second">The second read-only span of 64-bit integers (right operand).</param>
    /// <param name="destination">The span that receives the result of the subtraction. Must have the same length as <paramref name="first"/> and <paramref name="second"/>.</param>
    /// <exception cref="ArgumentException">Thrown when the lengths of <paramref name="first"/>, <paramref name="second"/> and <paramref name="destination"/> are not identical.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ElementWiseSubtract(ReadOnlySpan<long> first, ReadOnlySpan<long> second, Span<long> destination)
    {
        if (first.Length != second.Length || first.Length != destination.Length)
            ThrowIfLengthsAreNotTheSame();
        int length = first.Length;
        ref long currentfirst = ref MemoryMarshal.GetReference(first);
        ref long currentsecond = ref MemoryMarshal.GetReference(second);
        ref long dest = ref MemoryMarshal.GetReference(destination);
        if (Vector512.IsHardwareAccelerated && first.Length >= Vector512<long>.Count)
        {
            var count = Vector512<long>.Count;
            ref long end = ref Unsafe.Add(ref currentfirst, first.Length - (length % count));
            int remaining = length % count;
            do
            {
                var v1 = Vector512.LoadUnsafe(ref currentfirst);
                var v2 = Vector512.LoadUnsafe(ref currentsecond);
                (v1 - v2).StoreUnsafe(ref dest);
                currentfirst = ref Unsafe.Add(ref currentfirst, count);
                currentsecond = ref Unsafe.Add(ref currentsecond, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref currentfirst, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = first[i] - second[i];
            }
            return;
        }
        else if (Vector256.IsHardwareAccelerated && first.Length >= Vector256<long>.Count)
        {
            var count = Vector256<long>.Count;
            ref long end = ref Unsafe.Add(ref currentfirst, length - (length % count));
            int remaining = length % count;
            do
            {
                var v1 = Vector256.LoadUnsafe(ref currentfirst);
                var v2 = Vector256.LoadUnsafe(ref currentsecond);
                (v1 - v2).StoreUnsafe(ref dest);
                currentfirst = ref Unsafe.Add(ref currentfirst, count);
                currentsecond = ref Unsafe.Add(ref currentsecond, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref currentfirst, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = first[i] - second[i];
            }
            return;
        }
        else if (Vector128.IsHardwareAccelerated && length >= Vector128<long>.Count)
        {
            var count = Vector128<long>.Count;
            ref long end = ref Unsafe.Add(ref currentfirst, length - (length % count));
            int remaining = length % count;
            do
            {
                var v1 = Vector128.LoadUnsafe(ref currentfirst);
                var v2 = Vector128.LoadUnsafe(ref currentsecond);
                (v1 - v2).StoreUnsafe(ref dest);
                currentfirst = ref Unsafe.Add(ref currentfirst, count);
                currentsecond = ref Unsafe.Add(ref currentsecond, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref currentfirst, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = first[i] - second[i];
            }
            return;
        }
        for (int i = 0; i < first.Length; i++)
        {
            destination[i] = first[i] - second[i];
        }
    }
    /// <summary>
    /// Performs an element-wise subtraction of 16-bit integers from two source spans and stores the results in a destination span (destination[i] = first[i] - second[i]). The method automatically selects the widest available SIMD width (Vector512, Vector256 or Vector128) when hardware acceleration is enabled and the input is large enough, falling back to a scalar loop for the remaining elements.
    /// </summary>
    /// <param name="first">The first read-only span of 16-bit integers (left operand).</param>
    /// <param name="second">The second read-only span of 16-bit integers (right operand).</param>
    /// <param name="destination">The span that receives the result of the subtraction. Must have the same length as <paramref name="first"/> and <paramref name="second"/>.</param>
    /// <exception cref="ArgumentException">Thrown when the lengths of <paramref name="first"/>, <paramref name="second"/> and <paramref name="destination"/> are not identical.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ElementWiseSubtract(ReadOnlySpan<short> first, ReadOnlySpan<short> second, Span<short> destination)
    {
        if (first.Length != second.Length || first.Length != destination.Length)
            ThrowIfLengthsAreNotTheSame();
        int length = first.Length;
        ref short currentfirst = ref MemoryMarshal.GetReference(first);
        ref short currentsecond = ref MemoryMarshal.GetReference(second);
        ref short dest = ref MemoryMarshal.GetReference(destination);
        if (Vector512.IsHardwareAccelerated && first.Length >= Vector512<short>.Count)
        {
            var count = Vector512<short>.Count;
            ref short end = ref Unsafe.Add(ref currentfirst, length - (length % count));
            int remaining = length % count;
            do
            {
                var v1 = Vector512.LoadUnsafe(ref currentfirst);
                var v2 = Vector512.LoadUnsafe(ref currentsecond);
                (v1 - v2).StoreUnsafe(ref dest);
                currentfirst = ref Unsafe.Add(ref currentfirst, count);
                currentsecond = ref Unsafe.Add(ref currentsecond, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref currentfirst, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = (short)(first[i] - second[i]);
            }
            return;
        }
        else if (Vector256.IsHardwareAccelerated && first.Length >= Vector256<short>.Count)
        {
            var count = Vector256<short>.Count;
            ref short end = ref Unsafe.Add(ref currentfirst, length - (length % count));
            int remaining = length % count;
            do
            {
                var v1 = Vector256.LoadUnsafe(ref currentfirst);
                var v2 = Vector256.LoadUnsafe(ref currentsecond);
                (v1 - v2).StoreUnsafe(ref dest);
                currentfirst = ref Unsafe.Add(ref currentfirst, count);
                currentsecond = ref Unsafe.Add(ref currentsecond, count);
            } while (Unsafe.IsAddressLessThan(ref currentfirst, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = (short)(first[i] - second[i]);
            }
            return;
        }
        else if (Vector128.IsHardwareAccelerated && length >= Vector128<short>.Count)
        {
            var count = Vector128<short>.Count;
            ref short end = ref Unsafe.Add(ref currentfirst, first.Length - (length % count));
            int remaining = length % count;
            do
            {
                var v1 = Vector128.LoadUnsafe(ref currentfirst);
                var v2 = Vector128.LoadUnsafe(ref currentsecond);
                (v1 - v2).StoreUnsafe(ref dest);
                currentfirst = ref Unsafe.Add(ref currentfirst, count);
                currentsecond = ref Unsafe.Add(ref currentsecond, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref currentfirst, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = (short)(first[i] - second[i]);
            }
            return;
        }
        for (int i = 0; i < first.Length; i++)
        {
            destination[i] = (short)(first[i] - second[i]);
        }
    }
    /// <summary>
    /// Performs an element-wise subtraction of single-precision floating-point values from two source spans and stores the results in a destination span (destination[i] = first[i] - second[i]). The method automatically selects the widest available SIMD width (Vector512, Vector256 or Vector128) when hardware acceleration is enabled and the input is large enough, falling back to a scalar loop for the remaining elements.
    /// </summary>
    /// <param name="first">The first read-only span of single-precision floating-point values (left operand).</param>
    /// <param name="second">The second read-only span of single-precision floating-point values (right operand).</param>
    /// <param name="destination">The span that receives the result of the subtraction. Must have the same length as <paramref name="first"/> and <paramref name="second"/>.</param>
    /// <exception cref="ArgumentException">Thrown when the lengths of <paramref name="first"/>, <paramref name="second"/> and <paramref name="destination"/> are not identical.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ElementWiseSubtract(ReadOnlySpan<float> first, ReadOnlySpan<float> second, Span<float> destination)
    {
        if (first.Length != second.Length || first.Length != destination.Length)
            ThrowIfLengthsAreNotTheSame();
        int length = first.Length;
        ref float currentfirst = ref MemoryMarshal.GetReference(first);
        ref float currentsecond = ref MemoryMarshal.GetReference(second);
        ref float dest = ref MemoryMarshal.GetReference(destination);
        if (Vector512.IsHardwareAccelerated && first.Length >= Vector512<float>.Count)
        {
            var count = Vector512<float>.Count;
            ref float end = ref Unsafe.Add(ref currentfirst, first.Length - (length % count));
            int remaining = length % count;
            do
            {
                var v1 = Vector512.LoadUnsafe(ref currentfirst);
                var v2 = Vector512.LoadUnsafe(ref currentsecond);
                (v1 - v2).StoreUnsafe(ref dest);
                currentfirst = ref Unsafe.Add(ref currentfirst, count);
                currentsecond = ref Unsafe.Add(ref currentsecond, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref currentfirst, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = first[i] - second[i];
            }
            return;
        }
        else if (Vector256.IsHardwareAccelerated && first.Length >= Vector256<float>.Count)
        {
            var count = Vector256<float>.Count;
            ref float end = ref Unsafe.Add(ref currentfirst, first.Length - (length % count));
            int remaining = length % count;
            do
            {
                var v1 = Vector256.LoadUnsafe(ref currentfirst);
                var v2 = Vector256.LoadUnsafe(ref currentsecond);
                (v1 - v2).StoreUnsafe(ref dest);
                currentfirst = ref Unsafe.Add(ref currentfirst, count);
                currentsecond = ref Unsafe.Add(ref currentsecond, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref currentfirst, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = first[i] - second[i];
            }
            return;
        }
        else if (Vector128.IsHardwareAccelerated && length >= Vector128<float>.Count)
        {
            var count = Vector128<float>.Count;
            ref float end = ref Unsafe.Add(ref currentfirst, first.Length - (length % count));
            int remaining = length % count;
            do
            {
                var v1 = Vector128.LoadUnsafe(ref currentfirst);
                var v2 = Vector128.LoadUnsafe(ref currentsecond);
                (v1 - v2).StoreUnsafe(ref dest);
                currentfirst = ref Unsafe.Add(ref currentfirst, count);
                currentsecond = ref Unsafe.Add(ref currentsecond, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref currentfirst, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = first[i] - second[i];
            }
            return;
        }
        for (int i = 0; i < first.Length; i++)
        {
            destination[i] = first[i] - second[i];
        }
    }
    /// <summary>
    /// Performs an element-wise subtraction of double-precision floating-point values from two source spans and stores the results in a destination span (destination[i] = first[i] - second[i]). The method automatically selects the widest available SIMD width (Vector512, Vector256 or Vector128) when hardware acceleration is enabled and the input is large enough, falling back to a scalar loop for the remaining elements.
    /// </summary>
    /// <param name="first">The first read-only span of double-precision floating-point values (left operand).</param>
    /// <param name="second">The second read-only span of double-precision floating-point values (right operand).</param>
    /// <param name="destination">The span that receives the result of the subtraction. Must have the same length as <paramref name="first"/> and <paramref name="second"/>.</param>
    /// <exception cref="ArgumentException">Thrown when the lengths of <paramref name="first"/>, <paramref name="second"/> and <paramref name="destination"/> are not identical.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ElementWiseSubtract(ReadOnlySpan<double> first, ReadOnlySpan<double> second, Span<double> destination)
    {
        if (first.Length != second.Length || first.Length != destination.Length)
            ThrowIfLengthsAreNotTheSame();
        int length = first.Length;
        ref double currentfirst = ref MemoryMarshal.GetReference(first);
        ref double currentsecond = ref MemoryMarshal.GetReference(second);
        ref double dest = ref MemoryMarshal.GetReference(destination);
        if (Vector512.IsHardwareAccelerated && first.Length >= Vector512<double>.Count)
        {
            var count = Vector512<double>.Count;
            ref double end = ref Unsafe.Add(ref currentfirst, first.Length - (length % count));
            int remaining = length % count;
            do
            {
                var v1 = Vector512.LoadUnsafe(ref currentfirst);
                var v2 = Vector512.LoadUnsafe(ref currentsecond);
                (v1 - v2).StoreUnsafe(ref dest);
                currentfirst = ref Unsafe.Add(ref currentfirst, count);
                currentsecond = ref Unsafe.Add(ref currentsecond, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref currentfirst, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = first[i] - second[i];
            }
            return;
        }
        else if (Vector256.IsHardwareAccelerated && first.Length >= Vector256<double>.Count)
        {
            var count = Vector256<double>.Count;
            ref double end = ref Unsafe.Add(ref currentfirst, first.Length - (length % count));
            int remaining = length % count;
            do
            {
                var v1 = Vector256.LoadUnsafe(ref currentfirst);
                var v2 = Vector256.LoadUnsafe(ref currentsecond);
                (v1 - v2).StoreUnsafe(ref dest);
                currentfirst = ref Unsafe.Add(ref currentfirst, count);
                currentsecond = ref Unsafe.Add(ref currentsecond, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref currentfirst, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = first[i] - second[i];
            }
            return;
        }
        else if (Vector128.IsHardwareAccelerated && length >= Vector128<double>.Count)
        {
            var count = Vector128<double>.Count;
            ref double end = ref Unsafe.Add(ref currentfirst, first.Length - (length % count));
            int remaining = length % count;
            do
            {
                var v1 = Vector128.LoadUnsafe(ref currentfirst);
                var v2 = Vector128.LoadUnsafe(ref currentsecond);
                (v1 - v2).StoreUnsafe(ref dest);
                currentfirst = ref Unsafe.Add(ref currentfirst, count);
                currentsecond = ref Unsafe.Add(ref currentsecond, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref currentfirst, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = first[i] - second[i];
            }
            return;
        }
        for (int i = 0; i < first.Length; i++)
        {
            destination[i] = first[i] - second[i];
        }
    }
    /// <summary>
    /// Performs an element-wise division of integers from two source spans and stores the results in a destination span (destination[i] = first[i] / second[i]). The method automatically selects the widest available SIMD width (Vector512, Vector256 or Vector128) when hardware acceleration is enabled and the input is large enough, falling back to a scalar loop for the remaining elements.
    /// </summary>
    /// <param name="first">The first read-only span of integers (left operand).</param>
    /// <param name="second">The second read-only span of integers (right operand).</param>
    /// <param name="destination">The span that receives the result of the division. Must have the same length as <paramref name="first"/> and <paramref name="second"/>.</param>
    /// <exception cref="ArgumentException">Thrown when the lengths of <paramref name="first"/>, <paramref name="second"/> and <paramref name="destination"/> are not identical.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ElementWiseDivide(ReadOnlySpan<int> first, ReadOnlySpan<int> second, Span<int> destination)
    {
        if (first.Length != second.Length || first.Length != destination.Length)
            ThrowIfLengthsAreNotTheSame();
        int length = first.Length;
        ref int currentfirst = ref MemoryMarshal.GetReference(first);
        ref int currentsecond = ref MemoryMarshal.GetReference(second);
        ref int dest = ref MemoryMarshal.GetReference(destination);
        if (Vector512.IsHardwareAccelerated && first.Length >= Vector512<int>.Count)
        {
            var count = Vector512<int>.Count;
            ref int end = ref Unsafe.Add(ref currentfirst, first.Length - (length % count));
            int remaining = length % count;
            do
            {
                var v1 = Vector512.LoadUnsafe(ref currentfirst);
                var v2 = Vector512.LoadUnsafe(ref currentsecond);
                (v1 / v2).StoreUnsafe(ref dest);
                currentfirst = ref Unsafe.Add(ref currentfirst, count);
                currentsecond = ref Unsafe.Add(ref currentsecond, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref currentfirst, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = first[i] / second[i];
            }
            return;
        }
        else if (Vector256.IsHardwareAccelerated && first.Length >= Vector256<int>.Count)
        {
            var count = Vector256<int>.Count;
            ref int end = ref Unsafe.Add(ref currentfirst, first.Length - (length % count));
            int remaining = length % count;
            do
            {
                var v1 = Vector256.LoadUnsafe(ref currentfirst);
                var v2 = Vector256.LoadUnsafe(ref currentsecond);
                (v1 / v2).StoreUnsafe(ref dest);
                currentfirst = ref Unsafe.Add(ref currentfirst, count);
                currentsecond = ref Unsafe.Add(ref currentsecond, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref currentfirst, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = first[i] / second[i];
            }
            return;
        }
        else if (Vector128.IsHardwareAccelerated && length >= Vector128<int>.Count)
        {
            var count = Vector128<int>.Count;
            ref int end = ref Unsafe.Add(ref currentfirst, first.Length - (length % count));
            int remaining = length % count;
            do
            {
                var v1 = Vector128.LoadUnsafe(ref currentfirst);
                var v2 = Vector128.LoadUnsafe(ref currentsecond);
                (v1 / v2).StoreUnsafe(ref dest);
                currentfirst = ref Unsafe.Add(ref currentfirst, count);
                currentsecond = ref Unsafe.Add(ref currentsecond, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref currentfirst, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = first[i] / second[i];
            }
            return;
        }
        for (int i = 0; i < first.Length; i++)
        {
            destination[i] = first[i] / second[i];
        }
    }
    /// <summary>
    /// Performs an element-wise division of 64-bit integers from two source spans and stores the results in a destination span (destination[i] = first[i] / second[i]). The method automatically selects the widest available SIMD width (Vector512, Vector256 or Vector128) when hardware acceleration is enabled and the input is large enough, falling back to a scalar loop for the remaining elements.
    /// </summary>
    /// <param name="first">The first read-only span of 64-bit integers (left operand).</param>
    /// <param name="second">The second read-only span of 64-bit integers (right operand).</param>
    /// <param name="destination">The span that receives the result of the division. Must have the same length as <paramref name="first"/> and <paramref name="second"/>.</param>
    /// <exception cref="ArgumentException">Thrown when the lengths of <paramref name="first"/>, <paramref name="second"/> and <paramref name="destination"/> are not identical.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ElementWiseDivide(ReadOnlySpan<long> first, ReadOnlySpan<long> second, Span<long> destination)
    {
        if (first.Length != second.Length || first.Length != destination.Length)
            ThrowIfLengthsAreNotTheSame();
        int length = first.Length;
        ref long currentfirst = ref MemoryMarshal.GetReference(first);
        ref long currentsecond = ref MemoryMarshal.GetReference(second);
        ref long dest = ref MemoryMarshal.GetReference(destination);
        if (Vector512.IsHardwareAccelerated && first.Length >= Vector512<long>.Count)
        {
            var count = Vector512<long>.Count;
            ref long end = ref Unsafe.Add(ref currentfirst, first.Length - (length % count));
            int remaining = length % count;
            do
            {
                var v1 = Vector512.LoadUnsafe(ref currentfirst);
                var v2 = Vector512.LoadUnsafe(ref currentsecond);
                (v1 / v2).StoreUnsafe(ref dest);
                currentfirst = ref Unsafe.Add(ref currentfirst, count);
                currentsecond = ref Unsafe.Add(ref currentsecond, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref currentfirst, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = first[i] / second[i];
            }
            return;
        }
        else if (Vector256.IsHardwareAccelerated && first.Length >= Vector256<long>.Count)
        {
            var count = Vector256<long>.Count;
            ref long end = ref Unsafe.Add(ref currentfirst, first.Length - (length % count));
            int remaining = length % count;
            do
            {
                var v1 = Vector256.LoadUnsafe(ref currentfirst);
                var v2 = Vector256.LoadUnsafe(ref currentsecond);
                (v1 / v2).StoreUnsafe(ref dest);
                currentfirst = ref Unsafe.Add(ref currentfirst, count);
                currentsecond = ref Unsafe.Add(ref currentsecond, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref currentfirst, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = first[i] / second[i];
            }
            return;
        }
        else if (Vector128.IsHardwareAccelerated && length >= Vector128<long>.Count)
        {
            var count = Vector128<long>.Count;
            ref long end = ref Unsafe.Add(ref currentfirst, first.Length - (length % count));
            int remaining = length % count;
            do
            {
                var v1 = Vector128.LoadUnsafe(ref currentfirst);
                var v2 = Vector128.LoadUnsafe(ref currentsecond);
                (v1 / v2).StoreUnsafe(ref dest);
                currentfirst = ref Unsafe.Add(ref currentfirst, count);
                currentsecond = ref Unsafe.Add(ref currentsecond, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref currentfirst, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = first[i] / second[i];
            }
            return;
        }
        for (int i = 0; i < first.Length; i++)
        {
            destination[i] = first[i] / second[i];
        }
    }
    /// <summary>
    /// Performs an element-wise division of 16-bit integers from two source spans and stores the results in a destination span (destination[i] = first[i] / second[i]). The method automatically selects the widest available SIMD width (Vector512, Vector256 or Vector128) when hardware acceleration is enabled and the input is large enough, falling back to a scalar loop for the remaining elements.
    /// </summary>
    /// <param name="first">The first read-only span of 16-bit integers (left operand).</param>
    /// <param name="second">The second read-only span of 16-bit integers (right operand).</param>
    /// <param name="destination">The span that receives the result of the division. Must have the same length as <paramref name="first"/> and <paramref name="second"/>.</param>
    /// <exception cref="ArgumentException">Thrown when the lengths of <paramref name="first"/>, <paramref name="second"/> and <paramref name="destination"/> are not identical.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ElementWiseDivide(ReadOnlySpan<short> first, ReadOnlySpan<short> second, Span<short> destination)
    {
        if (first.Length != second.Length || first.Length != destination.Length)
            ThrowIfLengthsAreNotTheSame();
        int length = first.Length;
        ref short currentfirst = ref MemoryMarshal.GetReference(first);
        ref short currentsecond = ref MemoryMarshal.GetReference(second);
        ref short dest = ref MemoryMarshal.GetReference(destination);
        if (Vector512.IsHardwareAccelerated && first.Length >= Vector512<short>.Count)
        {
            var count = Vector512<short>.Count;
            ref short end = ref Unsafe.Add(ref currentfirst, first.Length - (length % count));
            int remaining = length % count;
            do
            {
                var v1 = Vector512.LoadUnsafe(ref currentfirst);
                var v2 = Vector512.LoadUnsafe(ref currentsecond);
                (v1 / v2).StoreUnsafe(ref dest);
                currentfirst = ref Unsafe.Add(ref currentfirst, count);
                currentsecond = ref Unsafe.Add(ref currentsecond, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref currentfirst, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = (short)(first[i] / second[i]);
            }
            return;
        }
        else if (Vector256.IsHardwareAccelerated && first.Length >= Vector256<short>.Count)
        {
            var count = Vector256<short>.Count;
            ref short end = ref Unsafe.Add(ref currentfirst, length - (length % count));
            int remaining = length % count;
            do
            {
                var v1 = Vector256.LoadUnsafe(ref currentfirst);
                var v2 = Vector256.LoadUnsafe(ref currentsecond);
                (v1 / v2).StoreUnsafe(ref dest);
                currentfirst = ref Unsafe.Add(ref currentfirst, count);
                currentsecond = ref Unsafe.Add(ref currentsecond, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref currentfirst, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = (short)(first[i] / second[i]);
            }
            return;
        }
        else if (Vector128.IsHardwareAccelerated && length >= Vector128<short>.Count)
        {
            var count = Vector128<short>.Count;
            ref short end = ref Unsafe.Add(ref currentfirst, length - (length % count));
            int remaining = length % count;
            do
            {
                var v1 = Vector128.LoadUnsafe(ref currentfirst);
                var v2 = Vector128.LoadUnsafe(ref currentsecond);
                (v1 / v2).StoreUnsafe(ref dest);
                currentfirst = ref Unsafe.Add(ref currentfirst, count);
                currentsecond = ref Unsafe.Add(ref currentsecond, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref currentfirst, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = (short)(first[i] / second[i]);
            }
            return;
        }
        for (int i = 0; i < first.Length; i++)
        {
            destination[i] = (short)(first[i] / second[i]);
        }
    }
    /// <summary>
    /// Performs an element-wise division of single-precision floating-point values from two source spans and stores the results in a destination span (destination[i] = first[i] / second[i]). The method automatically selects the widest available SIMD width (Vector512, Vector256 or Vector128) when hardware acceleration is enabled and the input is large enough, falling back to a scalar loop for the remaining elements.
    /// </summary>
    /// <param name="first">The first read-only span of single-precision floating-point values (left operand).</param>
    /// <param name="second">The second read-only span of single-precision floating-point values (right operand).</param>
    /// <param name="destination">The span that receives the result of the division. Must have the same length as <paramref name="first"/> and <paramref name="second"/>.</param>
    /// <exception cref="ArgumentException">Thrown when the lengths of <paramref name="first"/>, <paramref name="second"/> and <paramref name="destination"/> are not identical.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ElementWiseDivide(ReadOnlySpan<float> first, ReadOnlySpan<float> second, Span<float> destination)
    {
        if (first.Length != second.Length || first.Length != destination.Length)
            ThrowIfLengthsAreNotTheSame();
        int length = first.Length;
        ref float currentfirst = ref MemoryMarshal.GetReference(first);
        ref float currentsecond = ref MemoryMarshal.GetReference(second);
        ref float dest = ref MemoryMarshal.GetReference(destination);
        if (Vector512.IsHardwareAccelerated && first.Length >= Vector512<float>.Count)
        {
            var count = Vector512<float>.Count;
            ref float end = ref Unsafe.Add(ref currentfirst, first.Length - (length % count));
            int remaining = length % count;
            do
            {
                var v1 = Vector512.LoadUnsafe(ref currentfirst);
                var v2 = Vector512.LoadUnsafe(ref currentsecond);
                (v1 / v2).StoreUnsafe(ref dest);
                currentfirst = ref Unsafe.Add(ref currentfirst, count);
                currentsecond = ref Unsafe.Add(ref currentsecond, count);
            } while (Unsafe.IsAddressLessThan(ref currentfirst, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = first[i] / second[i];
            }
            return;
        }
        else if (Vector256.IsHardwareAccelerated && first.Length >= Vector256<float>.Count)
        {
            var count = Vector256<float>.Count;
            ref float end = ref Unsafe.Add(ref currentfirst, first.Length - (length % count));
            int remaining = length % count;
            do
            {
                var v1 = Vector256.LoadUnsafe(ref currentfirst);
                var v2 = Vector256.LoadUnsafe(ref currentsecond);
                (v1 / v2).StoreUnsafe(ref dest);
                currentfirst = ref Unsafe.Add(ref currentfirst, count);
                currentsecond = ref Unsafe.Add(ref currentsecond, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref currentfirst, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = first[i] / second[i];
            }
            return;
        }
        else if (Vector128.IsHardwareAccelerated && length >= Vector128<float>.Count)
        {
            var count = Vector128<float>.Count;
            ref float end = ref Unsafe.Add(ref currentfirst, first.Length - (length % count));
            int remaining = length % count;
            do
            {
                var v1 = Vector128.LoadUnsafe(ref currentfirst);
                var v2 = Vector128.LoadUnsafe(ref currentsecond);
                (v1 / v2).StoreUnsafe(ref dest);
                currentfirst = ref Unsafe.Add(ref currentfirst, count);
                currentsecond = ref Unsafe.Add(ref currentsecond, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref currentfirst, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = first[i] / second[i];
            }
            return;
        }
        for (int i = 0; i < first.Length; i++)
        {
            destination[i] = first[i] / second[i];
        }
    }
    /// <summary>
    /// Performs an element-wise division of double-precision floating-point values from two source spans and stores the results in a destination span (destination[i] = first[i] / second[i]). The method automatically selects the widest available SIMD width (Vector512, Vector256 or Vector128) when hardware acceleration is enabled and the input is large enough, falling back to a scalar loop for the remaining elements.
    /// </summary>
    /// <param name="first">The first read-only span of double-precision floating-point values (left operand).</param>
    /// <param name="second">The second read-only span of double-precision floating-point values (right operand).</param>
    /// <param name="destination">The span that receives the result of the division. Must have the same length as <paramref name="first"/> and <paramref name="second"/>.</param>
    /// <exception cref="ArgumentException">Thrown when the lengths of <paramref name="first"/>, <paramref name="second"/> and <paramref name="destination"/> are not identical.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ElementWiseDivide(ReadOnlySpan<double> first, ReadOnlySpan<double> second, Span<double> destination)
    {
        if (first.Length != second.Length || first.Length != destination.Length)
            ThrowIfLengthsAreNotTheSame();
        int length = first.Length;
        ref double currentfirst = ref MemoryMarshal.GetReference(first);
        ref double currentsecond = ref MemoryMarshal.GetReference(second);
        ref double dest = ref MemoryMarshal.GetReference(destination);
        if (Vector512.IsHardwareAccelerated && first.Length >= Vector512<double>.Count)
        {
            var count = Vector512<double>.Count;
            ref double end = ref Unsafe.Add(ref currentfirst, length - (length % count));
            int remaining = length % count;
            do
            {
                var v1 = Vector512.LoadUnsafe(ref currentfirst);
                var v2 = Vector512.LoadUnsafe(ref currentsecond);
                (v1 / v2).StoreUnsafe(ref dest);
                currentfirst = ref Unsafe.Add(ref currentfirst, Vector512<double>.Count);
                currentsecond = ref Unsafe.Add(ref currentsecond, Vector512<double>.Count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref currentfirst, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = first[i] / second[i];
            }
            return;
        }
        else if (Vector256.IsHardwareAccelerated && first.Length >= Vector256<double>.Count)
        {
            var count = Vector256<double>.Count;
            ref double end = ref Unsafe.Add(ref currentfirst, length - (length % count));
            int remaining = length % count;
            do
            {
                var v1 = Vector256.LoadUnsafe(ref currentfirst);
                var v2 = Vector256.LoadUnsafe(ref currentsecond);
                (v1 / v2).StoreUnsafe(ref dest);
                currentfirst = ref Unsafe.Add(ref currentfirst, count);
                currentsecond = ref Unsafe.Add(ref currentsecond, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref currentfirst, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = first[i] / second[i];
            }
            return;
        }
        else if (Vector128.IsHardwareAccelerated && length >= Vector128<double>.Count)
        {
            var count = Vector128<double>.Count;
            ref double end = ref Unsafe.Add(ref currentfirst, length - (length % count));
            int remaining = length % count;
            do
            {
                var v1 = Vector128.LoadUnsafe(ref currentfirst);
                var v2 = Vector128.LoadUnsafe(ref currentsecond);
                (v1 / v2).StoreUnsafe(ref dest);
                currentfirst = ref Unsafe.Add(ref currentfirst, count);
                currentsecond = ref Unsafe.Add(ref currentsecond, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref currentfirst, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = first[i] / second[i];
            }
            return;
        }
        for (int i = 0; i < first.Length; i++)
        {
            destination[i] = first[i] / second[i];
        }
    }
    /// <summary>
    /// Applies an element-wise addition between every element of the source span and a single scalar value, storing the results in a destination span (destination[i] = source[i] + value). The method automatically selects the widest available SIMD width (Vector512, Vector256 or Vector128) when hardware acceleration is enabled and the input is large enough, falling back to a scalar loop for the remaining elements.
    /// </summary>
    /// <param name="source">The read-only span of integers to apply the addition to.</param>
    /// <param name="value">The scalar int value used as the second operand for every element.</param>
    /// <param name="destination">The span that receives the results. Must be at least as long as <paramref name="source"/>.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="destination"/> is shorter than <paramref name="source"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AddAllElementsWithValue(ReadOnlySpan<int> source, int value, Span<int> destination)
    {
        if (destination.Length < source.Length)
            ThrowIfDestinationIsSmallerThanSource();
        int length = source.Length;
        ref int current = ref MemoryMarshal.GetReference(source);
        ref int dest = ref MemoryMarshal.GetReference(destination);
        if (Vector512.IsHardwareAccelerated && length >= Vector512<int>.Count)
        {
            var count = Vector512<int>.Count;
            int remaining = length % count;
            Vector512<int> valueVector = Vector512.Create<int>(value);
            ref int end = ref Unsafe.Add(ref current, length - (remaining));
            do
            {
                var vector = Vector512.LoadUnsafe(ref current);
                (vector + valueVector).StoreUnsafe(ref dest);
                current = ref Unsafe.Add(ref current, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref current, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = source[i] + value;
            }
            return;
        }
        else if (Vector256.IsHardwareAccelerated && length >= Vector256<int>.Count)
        {
            var count = Vector256<int>.Count;
            int remaining = length % count;
            Vector256<int> valueVector = Vector256.Create(value);
            ref int end = ref Unsafe.Add(ref current, length - remaining);
            do
            {
                var vector = Vector256.LoadUnsafe(ref current);
                (vector + valueVector).StoreUnsafe(ref dest);
                current = ref Unsafe.Add(ref current, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref current, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = source[i] + value;
            }
            return;
        }
        else if (Vector128.IsHardwareAccelerated && length >= Vector128<int>.Count)
        {
            var count = Vector128<int>.Count;
            int remaining = length % count;
            ref int end = ref Unsafe.Add(ref current, length - remaining);
            Vector128<int> valueVector = Vector128.Create(value);
            do
            {
                var vector = Vector128.LoadUnsafe(ref current);
                (vector + valueVector).StoreUnsafe(ref dest);
                current = ref Unsafe.Add(ref current, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref current, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = source[i] + value;
            }
            return;
        }
        for (var i = 0; i < length; i++)
        {
            destination[i] = source[i] + value;
        }
    }
    /// <summary>
    /// Applies an element-wise addition between every element of the source span and a single scalar value, storing the results in a destination span (destination[i] = source[i] + value). The method automatically selects the widest available SIMD width (Vector512, Vector256 or Vector128) when hardware acceleration is enabled and the input is large enough, falling back to a scalar loop for the remaining elements.
    /// </summary>
    /// <param name="source">The read-only span of 64-bit integers to apply the addition to.</param>
    /// <param name="value">The scalar 64-bit value used as the second operand for every element.</param>
    /// <param name="destination">The span that receives the results. Must be at least as long as <paramref name="source"/>.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="destination"/> is shorter than <paramref name="source"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AddAllElementsWithValue(ReadOnlySpan<long> source, long value, Span<long> destination)
    {
        if (destination.Length < source.Length)
            ThrowIfDestinationIsSmallerThanSource();
        int length = source.Length;
        ref long current = ref MemoryMarshal.GetReference(source);
        ref long dest = ref MemoryMarshal.GetReference(destination);
        if (Vector512.IsHardwareAccelerated && length >= Vector512<long>.Count)
        {
            var count = Vector512<long>.Count;
            int remaining = length % count;
            Vector512<long> valueVector = Vector512.Create<long>(value);
            ref long end = ref Unsafe.Add(ref current, length - (remaining));
            do
            {
                var vector = Vector512.LoadUnsafe(ref current);
                (vector + valueVector).StoreUnsafe(ref dest);
                current = ref Unsafe.Add(ref current, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref current, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = source[i] + value;
            }
            return;
        }
        else if (Vector256.IsHardwareAccelerated && length >= Vector256<long>.Count)
        {
            var count = Vector256<long>.Count;
            int remaining = length % count;
            Vector256<long> valueVector = Vector256.Create(value);
            ref long end = ref Unsafe.Add(ref current, length - remaining);
            do
            {
                var vector = Vector256.LoadUnsafe(ref current);
                (vector + valueVector).StoreUnsafe(ref dest);
                current = ref Unsafe.Add(ref current, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref current, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = source[i] + value;
            }
            return;
        }
        else if (Vector128.IsHardwareAccelerated && length >= Vector128<long>.Count)
        {
            var count = Vector128<long>.Count;
            int remaining = length % count;
            ref long end = ref Unsafe.Add(ref current, length - remaining);
            Vector128<long> valueVector = Vector128.Create(value);
            do
            {
                var vector = Vector128.LoadUnsafe(ref current);
                (vector + valueVector).StoreUnsafe(ref dest);
                current = ref Unsafe.Add(ref current, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref current, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = source[i] + value;
            }
            return;
        }
        for (var i = 0; i < length; i++)
        {
            destination[i] = source[i] + value;
        }
    }
    /// <summary>
    /// Applies an element-wise addition between every element of the source span and a single scalar value, storing the results in a destination span (destination[i] = source[i] + value). The method automatically selects the widest available SIMD width (Vector512, Vector256 or Vector128) when hardware acceleration is enabled and the input is large enough, falling back to a scalar loop for the remaining elements.
    /// </summary>
    /// <param name="source">The read-only span of 16-bit integers to apply the addition to.</param>
    /// <param name="value">The scalar 16-bit value used as the second operand for every element.</param>
    /// <param name="destination">The span that receives the results. Must be at least as long as <paramref name="source"/>.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="destination"/> is shorter than <paramref name="source"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AddAllElementsWithValue(ReadOnlySpan<short> source, short value, Span<short> destination)
    {
        if (destination.Length < source.Length)
            ThrowIfDestinationIsSmallerThanSource();
        int length = source.Length;
        ref short current = ref MemoryMarshal.GetReference(source);
        ref short dest = ref MemoryMarshal.GetReference(destination);
        if (Vector512.IsHardwareAccelerated && length >= Vector512<short>.Count)
        {
            var count = Vector512<short>.Count;
            int remaining = length % count;
            Vector512<short> valueVector = Vector512.Create(value);
            ref short end = ref Unsafe.Add(ref current, length - (remaining));
            do
            {
                var vector = Vector512.LoadUnsafe(ref current);
                (vector + valueVector).StoreUnsafe(ref dest);
                current = ref Unsafe.Add(ref current, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref current, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = (short)(source[i] + value);
            }
            return;
        }
        else if (Vector256.IsHardwareAccelerated && length >= Vector256<short>.Count)
        {
            var count = Vector256<short>.Count;
            int remaining = length % count;
            Vector256<short> valueVector = Vector256.Create(value);
            ref short end = ref Unsafe.Add(ref current, length - remaining);
            do
            {
                var vector = Vector256.LoadUnsafe(ref current);
                (vector + valueVector).StoreUnsafe(ref dest);
                current = ref Unsafe.Add(ref current, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref current, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = (short)(source[i] + value);
            }
            return;
        }
        else if (Vector128.IsHardwareAccelerated && length >= Vector128<short>.Count)
        {
            var count = Vector128<short>.Count;
            int remaining = length % count;
            ref short end = ref Unsafe.Add(ref current, length - remaining);
            Vector128<short> valueVector = Vector128.Create(value);
            do
            {
                var vector = Vector128.LoadUnsafe(ref current);
                (vector + valueVector).StoreUnsafe(ref dest);
                current = ref Unsafe.Add(ref current, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref current, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = (short)(source[i] + value);
            }
            return;
        }
        for (var i = 0; i < length; i++)
        {
            destination[i] = (short)(source[i] + value);
        }
    }
    /// <summary>
    /// Applies an element-wise addition between every element of the source span and a single scalar value, storing the results in a destination span (destination[i] = source[i] + value). The method automatically selects the widest available SIMD width (Vector512, Vector256 or Vector128) when hardware acceleration is enabled and the input is large enough, falling back to a scalar loop for the remaining elements.
    /// </summary>
    /// <param name="source">The read-only span of single-precision floating-point values to apply the addition to.</param>
    /// <param name="value">The scalar single-precision value used as the second operand for every element.</param>
    /// <param name="destination">The span that receives the results. Must be at least as long as <paramref name="source"/>.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="destination"/> is shorter than <paramref name="source"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AddAllElementsWithValue(ReadOnlySpan<float> source, float value, Span<float> destination)
    {
        if (destination.Length < source.Length)
            ThrowIfDestinationIsSmallerThanSource();
        int length = source.Length;
        ref float current = ref MemoryMarshal.GetReference(source);
        ref float dest = ref MemoryMarshal.GetReference(destination);
        if (Vector512.IsHardwareAccelerated && length >= Vector512<float>.Count)
        {
            var count = Vector512<float>.Count;
            int remaining = length % count;
            Vector512<float> valueVector = Vector512.Create(value);
            ref float end = ref Unsafe.Add(ref current, length - (remaining));
            do
            {
                var vector = Vector512.LoadUnsafe(ref current);
                (vector + valueVector).StoreUnsafe(ref dest);
                current = ref Unsafe.Add(ref current, count);
                dest = ref Unsafe.Add(ref current, count);
            } while (Unsafe.IsAddressLessThan(ref current, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = source[i] + value;
            }
            return;
        }
        else if (Vector256.IsHardwareAccelerated && length >= Vector256<float>.Count)
        {
            int count = Vector256<float>.Count;
            int remaining = length % count;
            Vector256<float> valueVector = Vector256.Create(value);
            ref float end = ref Unsafe.Add(ref current, length - remaining);
            do
            {
                var vector = Vector256.LoadUnsafe(ref current);
                (vector + valueVector).StoreUnsafe(ref dest);
                current = ref Unsafe.Add(ref current, Vector256<float>.Count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref current, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = source[i] + value;
            }
            return;
        }
        else if (Vector128.IsHardwareAccelerated && length >= Vector128<float>.Count)
        {
            int count = Vector128<float>.Count;
            int remaining = length % count;
            ref float end = ref Unsafe.Add(ref current, length - remaining);
            Vector128<float> valueVector = Vector128.Create(value);
            do
            {
                var vector = Vector128.LoadUnsafe(ref current);
                (vector + valueVector).StoreUnsafe(ref dest);
                current = ref Unsafe.Add(ref current, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref current, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = source[i] + value;
            }
            return;
        }
        for (var i = 0; i < length; i++)
        {
            destination[i] = source[i] + value;
        }
    }
    /// <summary>
    /// Applies an element-wise addition between every element of the source span and a single scalar value, storing the results in a destination span (destination[i] = source[i] + value). The method automatically selects the widest available SIMD width (Vector512, Vector256 or Vector128) when hardware acceleration is enabled and the input is large enough, falling back to a scalar loop for the remaining elements.
    /// </summary>
    /// <param name="source">The read-only span of double-precision floating-point values to apply the addition to.</param>
    /// <param name="value">The scalar double-precision value used as the second operand for every element.</param>
    /// <param name="destination">The span that receives the results. Must be at least as long as <paramref name="source"/>.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="destination"/> is shorter than <paramref name="source"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AddAllElementsWithValue(ReadOnlySpan<double> source, double value, Span<double> destination)
    {
        if (destination.Length < source.Length)
            ThrowIfDestinationIsSmallerThanSource();
        int length = source.Length;
        ref double current = ref MemoryMarshal.GetReference(source);
        ref double dest = ref MemoryMarshal.GetReference(destination);
        if (Vector512.IsHardwareAccelerated && length >= Vector512<double>.Count)
        {
            int count = Vector512<double>.Count;
            int remaining = length % count;
            Vector512<double> valueVector = Vector512.Create(value);
            ref double end = ref Unsafe.Add(ref current, length - (remaining));
            do
            {
                var vector = Vector512.LoadUnsafe(ref current);
                (vector + valueVector).StoreUnsafe(ref dest);
                current = ref Unsafe.Add(ref current, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref current, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = source[i] + value;
            }
            return;
        }
        else if (Vector256.IsHardwareAccelerated && length >= Vector256<double>.Count)
        {
            int count = Vector256<double>.Count;
            int remaining = length % count;
            Vector256<double> valueVector = Vector256.Create(value);
            ref double end = ref Unsafe.Add(ref current, length - remaining);
            do
            {
                var vector = Vector256.LoadUnsafe(ref current);
                (vector + valueVector).StoreUnsafe(ref dest);
                current = ref Unsafe.Add(ref current, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref current, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = source[i] + value;
            }
            return;
        }
        else if (Vector128.IsHardwareAccelerated && length >= Vector128<double>.Count)
        {
            int count = Vector128<double>.Count;
            int remaining = length % count;
            ref double end = ref Unsafe.Add(ref current, length - remaining);
            Vector128<double> valueVector = Vector128.Create(value);
            do
            {
                var vector = Vector128.LoadUnsafe(ref current);
                (vector + valueVector).StoreUnsafe(ref dest);
                current = ref Unsafe.Add(ref current, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref current, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = source[i] + value;
            }
            return;
        }
        for (var i = 0; i < length; i++)
        {
            destination[i] = source[i] + value;
        }
    }
    /// <summary>
    /// Applies an element-wise subtraction between every element of the source span and a single scalar value, storing the results in a destination span (destination[i] = source[i] - value). The method automatically selects the widest available SIMD width (Vector512, Vector256 or Vector128) when hardware acceleration is enabled and the input is large enough, falling back to a scalar loop for the remaining elements.
    /// </summary>
    /// <param name="source">The read-only span of integers to apply the subtraction to.</param>
    /// <param name="value">The scalar int value used as the second operand for every element.</param>
    /// <param name="destination">The span that receives the results. Must be at least as long as <paramref name="source"/>.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="destination"/> is shorter than <paramref name="source"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SubtractValueFromAllElements(ReadOnlySpan<int> source, int value, Span<int> destination)
    {
        if (destination.Length < source.Length)
            ThrowIfDestinationIsSmallerThanSource();
        int length = source.Length;
        ref int current = ref MemoryMarshal.GetReference(source);
        ref int dest = ref MemoryMarshal.GetReference(destination);
        if (Vector512.IsHardwareAccelerated && length >= Vector512<int>.Count)
        {
            int count = Vector512<int>.Count;
            int remaining = length % count;
            Vector512<int> valueVector = Vector512.Create<int>(value);
            ref int end = ref Unsafe.Add(ref current, length - (remaining));
            do
            {
                var vector = Vector512.LoadUnsafe(ref current);
                (vector - valueVector).StoreUnsafe(ref dest);
                current = ref Unsafe.Add(ref current, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref current, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = source[i] - value;
            }
            return;
        }
        else if (Vector256.IsHardwareAccelerated && length >= Vector256<int>.Count)
        {
            int count = Vector256<int>.Count;
            int remaining = length % count;
            Vector256<int> valueVector = Vector256.Create(value);
            ref int end = ref Unsafe.Add(ref current, length - remaining);
            do
            {
                var vector = Vector256.LoadUnsafe(ref current);
                (vector - valueVector).StoreUnsafe(ref dest);
                current = ref Unsafe.Add(ref current, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref current, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = source[i] - value;
            }
            return;
        }
        else if (Vector128.IsHardwareAccelerated && length >= Vector128<int>.Count)
        {
            int count = Vector128<int>.Count;
            int remaining = length % count;
            ref int end = ref Unsafe.Add(ref current, length - remaining);
            Vector128<int> valueVector = Vector128.Create(value);
            do
            {
                var vector = Vector128.LoadUnsafe(ref current);
                (vector - valueVector).StoreUnsafe(ref dest);
                current = ref Unsafe.Add(ref current, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref current, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = source[i] - value;
            }
            return;
        }
        for (var i = 0; i < length; i++)
        {
            destination[i] = source[i] - value;
        }
    }
    /// <summary>
    /// Applies an element-wise subtraction between every element of the source span and a single scalar value, storing the results in a destination span (destination[i] = source[i] - value). The method automatically selects the widest available SIMD width (Vector512, Vector256 or Vector128) when hardware acceleration is enabled and the input is large enough, falling back to a scalar loop for the remaining elements.
    /// </summary>
    /// <param name="source">The read-only span of 64-bit integers to apply the subtraction to.</param>
    /// <param name="value">The scalar 64-bit value used as the second operand for every element.</param>
    /// <param name="destination">The span that receives the results. Must be at least as long as <paramref name="source"/>.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="destination"/> is shorter than <paramref name="source"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SubtractValueFromAllElements(ReadOnlySpan<long> source, long value, Span<long> destination)
    {
        if (destination.Length < source.Length)
            ThrowIfDestinationIsSmallerThanSource();
        int length = source.Length;
        ref long current = ref MemoryMarshal.GetReference(source);
        ref long dest = ref MemoryMarshal.GetReference(destination);
        if (Vector512.IsHardwareAccelerated && length >= Vector512<long>.Count)
        {
            int count = Vector512<long>.Count;
            int remaining = length % count;
            Vector512<long> valueVector = Vector512.Create<long>(value);
            ref long end = ref Unsafe.Add(ref current, length - (remaining));
            do
            {
                var vector = Vector512.LoadUnsafe(ref current);
                (vector - valueVector).StoreUnsafe(ref dest);
                current = ref Unsafe.Add(ref current, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref current, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = source[i] - value;
            }
            return;
        }
        else if (Vector256.IsHardwareAccelerated && length >= Vector256<long>.Count)
        {
            int count = Vector256<long>.Count;
            int remaining = length % count;
            Vector256<long> valueVector = Vector256.Create(value);
            ref long end = ref Unsafe.Add(ref current, length - remaining);
            do
            {
                var vector = Vector256.LoadUnsafe(ref current);
                (vector - valueVector).StoreUnsafe(ref dest);
                current = ref Unsafe.Add(ref current, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref current, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = source[i] - value;
            }
            return;
        }
        else if (Vector128.IsHardwareAccelerated && length >= Vector128<long>.Count)
        {
            int count = Vector128<long>.Count;
            int remaining = length % count;
            ref long end = ref Unsafe.Add(ref current, length - remaining);
            Vector128<long> valueVector = Vector128.Create(value);
            do
            {
                var vector = Vector128.LoadUnsafe(ref current);
                (vector - valueVector).StoreUnsafe(ref dest);
                current = ref Unsafe.Add(ref current, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref current, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = source[i] - value;
            }
            return;
        }
        for (var i = 0; i < length; i++)
        {
            destination[i] = source[i] - value;
        }
    }
    /// <summary>
    /// Applies an element-wise subtraction between every element of the source span and a single scalar value, storing the results in a destination span (destination[i] = source[i] - value). The method automatically selects the widest available SIMD width (Vector512, Vector256 or Vector128) when hardware acceleration is enabled and the input is large enough, falling back to a scalar loop for the remaining elements.
    /// </summary>
    /// <param name="source">The read-only span of 16-bit integers to apply the subtraction to.</param>
    /// <param name="value">The scalar 16-bit value used as the second operand for every element.</param>
    /// <param name="destination">The span that receives the results. Must be at least as long as <paramref name="source"/>.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="destination"/> is shorter than <paramref name="source"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SubtractValueFromAllElements(ReadOnlySpan<short> source, short value, Span<short> destination)
    {
        if (destination.Length < source.Length)
            ThrowIfDestinationIsSmallerThanSource();
        int length = source.Length;
        ref short current = ref MemoryMarshal.GetReference(source);
        ref short dest = ref MemoryMarshal.GetReference(destination);
        if (Vector512.IsHardwareAccelerated && length >= Vector512<short>.Count)
        {
            int count = Vector512<short>.Count;
            int remaining = length % count;
            Vector512<short> valueVector = Vector512.Create(value);
            ref short end = ref Unsafe.Add(ref current, length - (remaining));
            do
            {
                var vector = Vector512.LoadUnsafe(ref current);
                (vector - valueVector).StoreUnsafe(ref dest);
                current = ref Unsafe.Add(ref current, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref current, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = (short)(source[i] - value);
            }
            return;
        }
        else if (Vector256.IsHardwareAccelerated && length >= Vector256<short>.Count)
        {
            int count = Vector256<short>.Count;
            int remaining = length % count;
            Vector256<short> valueVector = Vector256.Create(value);
            ref short end = ref Unsafe.Add(ref current, length - remaining);
            do
            {
                var vector = Vector256.LoadUnsafe(ref current);
                (vector - valueVector).StoreUnsafe(ref dest);
                current = ref Unsafe.Add(ref current, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref current, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = (short)(source[i] - value);
            }
            return;
        }
        else if (Vector128.IsHardwareAccelerated && length >= Vector128<short>.Count)
        {
            int count = Vector128<short>.Count;
            int remaining = length % count;
            ref short end = ref Unsafe.Add(ref current, length - remaining);
            Vector128<short> valueVector = Vector128.Create(value);
            do
            {
                var vector = Vector128.LoadUnsafe(ref current);
                (vector - valueVector).StoreUnsafe(ref dest);
                current = ref Unsafe.Add(ref current, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref current, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = (short)(source[i] - value);
            }
            return;
        }
        for (var i = 0; i < length; i++)
        {
            destination[i] = (short)(source[i] - value);
        }
    }
    /// <summary>
    /// Applies an element-wise subtraction between every element of the source span and a single scalar value, storing the results in a destination span (destination[i] = source[i] - value). The method automatically selects the widest available SIMD width (Vector512, Vector256 or Vector128) when hardware acceleration is enabled and the input is large enough, falling back to a scalar loop for the remaining elements.
    /// </summary>
    /// <param name="source">The read-only span of single-precision floating-point values to apply the subtraction to.</param>
    /// <param name="value">The scalar single-precision value used as the second operand for every element.</param>
    /// <param name="destination">The span that receives the results. Must be at least as long as <paramref name="source"/>.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="destination"/> is shorter than <paramref name="source"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SubtractValueFromAllElements(ReadOnlySpan<float> source, float value, Span<float> destination)
    {
        if (destination.Length < source.Length)
            ThrowIfDestinationIsSmallerThanSource();
        int length = source.Length;
        ref float current = ref MemoryMarshal.GetReference(source);
        ref float dest = ref MemoryMarshal.GetReference(destination);
        if (Vector512.IsHardwareAccelerated && length >= Vector512<float>.Count)
        {
            int count = Vector512<float>.Count;
            int remaining = length % count;
            Vector512<float> valueVector = Vector512.Create(value);
            ref float end = ref Unsafe.Add(ref current, length - (remaining));
            do
            {
                var vector = Vector512.LoadUnsafe(ref current);
                (vector - valueVector).StoreUnsafe(ref dest);
                current = ref Unsafe.Add(ref current, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref current, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = source[i] - value;
            }
            return;
        }
        else if (Vector256.IsHardwareAccelerated && length >= Vector256<float>.Count)
        {
            int count = Vector256<float>.Count;
            int remaining = length % count;
            Vector256<float> valueVector = Vector256.Create(value);
            ref float end = ref Unsafe.Add(ref current, length - remaining);
            do
            {
                var vector = Vector256.LoadUnsafe(ref current);
                (vector - valueVector).StoreUnsafe(ref dest);
                current = ref Unsafe.Add(ref current, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref current, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = source[i] - value;
            }
            return;
        }
        else if (Vector128.IsHardwareAccelerated && length >= Vector128<float>.Count)
        {
            int count = Vector128<float>.Count;
            int remaining = length % count;
            ref float end = ref Unsafe.Add(ref current, length - remaining);
            Vector128<float> valueVector = Vector128.Create(value);
            do
            {
                var vector = Vector128.LoadUnsafe(ref current);
                (vector - valueVector).StoreUnsafe(ref dest);
                current = ref Unsafe.Add(ref current, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref current, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = source[i] - value;
            }
            return;
        }
        for (var i = 0; i < length; i++)
        {
            destination[i] = source[i] - value;
        }
    }
    /// <summary>
    /// Applies an element-wise subtraction between every element of the source span and a single scalar value, storing the results in a destination span (destination[i] = source[i] - value). The method automatically selects the widest available SIMD width (Vector512, Vector256 or Vector128) when hardware acceleration is enabled and the input is large enough, falling back to a scalar loop for the remaining elements.
    /// </summary>
    /// <param name="source">The read-only span of double-precision floating-point values to apply the subtraction to.</param>
    /// <param name="value">The scalar double-precision value used as the second operand for every element.</param>
    /// <param name="destination">The span that receives the results. Must be at least as long as <paramref name="source"/>.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="destination"/> is shorter than <paramref name="source"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SubtractValueFromAllElements(ReadOnlySpan<double> source, double value, Span<double> destination)
    {
        if (destination.Length < source.Length)
            ThrowIfDestinationIsSmallerThanSource();
        int length = source.Length;
        ref double current = ref MemoryMarshal.GetReference(source);
        ref double dest = ref MemoryMarshal.GetReference(destination);
        if (Vector512.IsHardwareAccelerated && length >= Vector512<double>.Count)
        {
            int count = Vector512<double>.Count;
            int remaining = length % count;
            Vector512<double> valueVector = Vector512.Create(value);
            ref double end = ref Unsafe.Add(ref current, length - (remaining));
            do
            {
                var vector = Vector512.LoadUnsafe(ref current);
                (vector - valueVector).StoreUnsafe(ref dest);
                current = ref Unsafe.Add(ref current, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref current, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = source[i] - value;
            }
            return;
        }
        else if (Vector256.IsHardwareAccelerated && length >= Vector256<double>.Count)
        {
            int count = Vector256<double>.Count;
            int remaining = length % count;
            Vector256<double> valueVector = Vector256.Create(value);
            ref double end = ref Unsafe.Add(ref current, length - remaining);
            do
            {
                var vector = Vector256.LoadUnsafe(ref current);
                (vector - valueVector).StoreUnsafe(ref dest);
                current = ref Unsafe.Add(ref current, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref current, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = source[i] - value;
            }
            return;
        }
        else if (Vector128.IsHardwareAccelerated && length >= Vector128<double>.Count)
        {

            int count = Vector128<double>.Count;
            int remaining = length % count;
            ref double end = ref Unsafe.Add(ref current, length - remaining);
            Vector128<double> valueVector = Vector128.Create(value);
            do
            {
                var vector = Vector128.LoadUnsafe(ref current);
                (vector - valueVector).StoreUnsafe(ref dest);
                current = ref Unsafe.Add(ref current, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref current, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = source[i] - value;
            }
            return;
        }
        for (var i = 0; i < length; i++)
        {
            destination[i] = source[i] - value;
        }
    }
    /// <summary>
    /// Applies an element-wise multiplication between every element of the source span and a single scalar value, storing the results in a destination span (destination[i] = source[i] * value). The method automatically selects the widest available SIMD width (Vector512, Vector256 or Vector128) when hardware acceleration is enabled and the input is large enough, falling back to a scalar loop for the remaining elements.
    /// </summary>
    /// <param name="source">The read-only span of integers to apply the multiplication to.</param>
    /// <param name="value">The scalar int value used as the second operand for every element.</param>
    /// <param name="destination">The span that receives the results. Must be at least as long as <paramref name="source"/>.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="destination"/> is shorter than <paramref name="source"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void MultiplyElementsWithValue(ReadOnlySpan<int> source, int value, Span<int> destination)
    {
        if (destination.Length < source.Length)
            ThrowIfDestinationIsSmallerThanSource();
        int length = source.Length;
        ref int current = ref MemoryMarshal.GetReference(source);
        ref int dest = ref MemoryMarshal.GetReference(destination);
        if (Vector512.IsHardwareAccelerated && length >= Vector512<int>.Count)
        {
            int count = Vector512<int>.Count;
            int remaining = length % count;
            Vector512<int> valueVector = Vector512.Create<int>(value);
            ref int end = ref Unsafe.Add(ref current, length - (remaining));
            do
            {
                var vector = Vector512.LoadUnsafe(ref current);
                (vector * valueVector).StoreUnsafe(ref dest);
                current = ref Unsafe.Add(ref current, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref current, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = source[i] * value;
            }
            return;
        }
        else if (Vector256.IsHardwareAccelerated && length >= Vector256<int>.Count)
        {
            int count = Vector256<int>.Count;
            int remaining = length % count;
            Vector256<int> valueVector = Vector256.Create(value);
            ref int end = ref Unsafe.Add(ref current, length - remaining);
            do
            {
                var vector = Vector256.LoadUnsafe(ref current);
                (vector * valueVector).StoreUnsafe(ref dest);
                current = ref Unsafe.Add(ref current, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref current, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = source[i] * value;
            }
            return;
        }
        else if (Vector128.IsHardwareAccelerated && length >= Vector128<int>.Count)
        {
            int count = Vector128<int>.Count;
            int remaining = length % count;
            ref int end = ref Unsafe.Add(ref current, length - remaining);
            Vector128<int> valueVector = Vector128.Create(value);
            do
            {
                var vector = Vector128.LoadUnsafe(ref current);
                (vector * valueVector).StoreUnsafe(ref dest);
                current = ref Unsafe.Add(ref current, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref current, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = source[i] * value;
            }
            return;
        }
        for (var i = 0; i < length; i++)
        {
            destination[i] = source[i] * value;
        }
    }
    /// <summary>
    /// Applies an element-wise multiplication between every element of the source span and a single scalar value, storing the results in a destination span (destination[i] = source[i] * value). The method automatically selects the widest available SIMD width (Vector512, Vector256 or Vector128) when hardware acceleration is enabled and the input is large enough, falling back to a scalar loop for the remaining elements.
    /// </summary>
    /// <param name="source">The read-only span of 64-bit integers to apply the multiplication to.</param>
    /// <param name="value">The scalar 64-bit value used as the second operand for every element.</param>
    /// <param name="destination">The span that receives the results. Must be at least as long as <paramref name="source"/>.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="destination"/> is shorter than <paramref name="source"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void MultiplyElementsWithValue(ReadOnlySpan<long> source, long value, Span<long> destination)
    {
        if (destination.Length < source.Length)
            ThrowIfDestinationIsSmallerThanSource();
        int length = source.Length;
        ref long current = ref MemoryMarshal.GetReference(source);
        ref long dest = ref MemoryMarshal.GetReference(destination);
        if (Vector512.IsHardwareAccelerated && length >= Vector512<long>.Count)
        {
            var count = Vector512<long>.Count;
            int remaining = length % count;
            Vector512<long> valueVector = Vector512.Create<long>(value);
            ref long end = ref Unsafe.Add(ref current, length - (remaining));
            do
            {
                var vector = Vector512.LoadUnsafe(ref current);
                (vector * valueVector).StoreUnsafe(ref dest);
                current = ref Unsafe.Add(ref current, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref current, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = source[i] * value;
            }
            return;
        }
        else if (Vector256.IsHardwareAccelerated && length >= Vector256<long>.Count)
        {
            var count = Vector256<long>.Count;
            int remaining = length % count;
            Vector256<long> valueVector = Vector256.Create(value);
            ref long end = ref Unsafe.Add(ref current, length - remaining);
            do
            {
                var vector = Vector256.LoadUnsafe(ref current);
                (vector * valueVector).StoreUnsafe(ref dest);
                current = ref Unsafe.Add(ref current, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref current, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = source[i] * value;
            }
            return;
        }
        else if (Vector128.IsHardwareAccelerated && length >= Vector128<long>.Count)
        {
            var count = Vector128<long>.Count;
            int remaining = length % count;
            ref long end = ref Unsafe.Add(ref current, length - remaining);
            Vector128<long> valueVector = Vector128.Create(value);
            do
            {
                var vector = Vector128.LoadUnsafe(ref current);
                (vector * valueVector).StoreUnsafe(ref dest);
                current = ref Unsafe.Add(ref current, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref current, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = source[i] * value;
            }
            return;
        }
        for (var i = 0; i < length; i++)
        {
            destination[i] = source[i] * value;
        }
    }
    /// <summary>
    /// Applies an element-wise multiplication between every element of the source span and a single scalar value, storing the results in a destination span (destination[i] = source[i] * value). The method automatically selects the widest available SIMD width (Vector512, Vector256 or Vector128) when hardware acceleration is enabled and the input is large enough, falling back to a scalar loop for the remaining elements.
    /// </summary>
    /// <param name="source">The read-only span of 16-bit integers to apply the multiplication to.</param>
    /// <param name="value">The scalar 16-bit value used as the second operand for every element.</param>
    /// <param name="destination">The span that receives the results. Must be at least as long as <paramref name="source"/>.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="destination"/> is shorter than <paramref name="source"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void MultiplyElementsWithValue(ReadOnlySpan<short> source, short value, Span<short> destination)
    {
        if (destination.Length < source.Length)
            ThrowIfDestinationIsSmallerThanSource();
        int length = source.Length;
        ref short current = ref MemoryMarshal.GetReference(source);
        ref short dest = ref MemoryMarshal.GetReference(destination);
        if (Vector512.IsHardwareAccelerated && length >= Vector512<short>.Count)
        {
            int count = Vector512<short>.Count;
            int remaining = length % count;
            Vector512<short> valueVector = Vector512.Create(value);
            ref short end = ref Unsafe.Add(ref current, length - (remaining));
            do
            {
                var vector = Vector512.LoadUnsafe(ref current);
                (vector * valueVector).StoreUnsafe(ref dest);
                current = ref Unsafe.Add(ref current, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref current, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = (short)(source[i] * value);
            }
            return;
        }
        else if (Vector256.IsHardwareAccelerated && length >= Vector256<short>.Count)
        {
            int count = Vector256<short>.Count;
            int remaining = length % count;
            Vector256<short> valueVector = Vector256.Create(value);
            ref short end = ref Unsafe.Add(ref current, length - remaining);
            do
            {
                var vector = Vector256.LoadUnsafe(ref current);
                (vector * valueVector).StoreUnsafe(ref dest);
                current = ref Unsafe.Add(ref current, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref current, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = (short)(source[i] * value);
            }
            return;
        }
        else if (Vector128.IsHardwareAccelerated && length >= Vector128<short>.Count)
        {
            int count = Vector128<short>.Count;
            int remaining = length % count;
            ref short end = ref Unsafe.Add(ref current, length - remaining);
            Vector128<short> valueVector = Vector128.Create(value);
            do
            {
                var vector = Vector128.LoadUnsafe(ref current);
                (vector * valueVector).StoreUnsafe(ref dest);
                current = ref Unsafe.Add(ref current, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref current, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = (short)(source[i] * value);
            }
            return;
        }
        for (var i = 0; i < length; i++)
        {
            destination[i] = (short)(source[i] * value);
        }
    }
    /// <summary>
    /// Applies an element-wise multiplication between every element of the source span and a single scalar value, storing the results in a destination span (destination[i] = source[i] * value). The method automatically selects the widest available SIMD width (Vector512, Vector256 or Vector128) when hardware acceleration is enabled and the input is large enough, falling back to a scalar loop for the remaining elements.
    /// </summary>
    /// <param name="source">The read-only span of single-precision floating-point values to apply the multiplication to.</param>
    /// <param name="value">The scalar single-precision value used as the second operand for every element.</param>
    /// <param name="destination">The span that receives the results. Must be at least as long as <paramref name="source"/>.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="destination"/> is shorter than <paramref name="source"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void MultiplyElementsWithValue(ReadOnlySpan<float> source, float value, Span<float> destination)
    {
        if (destination.Length < source.Length)
            ThrowIfDestinationIsSmallerThanSource();
        int length = source.Length;
        ref float current = ref MemoryMarshal.GetReference(source);
        ref float dest = ref MemoryMarshal.GetReference(destination);
        if (Vector512.IsHardwareAccelerated && length >= Vector512<float>.Count)
        {
            int count = Vector512<float>.Count;
            int remaining = length % count;
            Vector512<float> valueVector = Vector512.Create(value);
            ref float end = ref Unsafe.Add(ref current, length - (remaining));
            do
            {
                var vector = Vector512.LoadUnsafe(ref current);
                (vector * valueVector).StoreUnsafe(ref dest);
                current = ref Unsafe.Add(ref current, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref current, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = source[i] * value;
            }
            return;
        }
        else if (Vector256.IsHardwareAccelerated && length >= Vector256<float>.Count)
        {
            int count = Vector256<float>.Count;
            int remaining = length % count;
            Vector256<float> valueVector = Vector256.Create(value);
            ref float end = ref Unsafe.Add(ref current, length - remaining);
            do
            {
                var vector = Vector256.LoadUnsafe(ref current);
                (vector * valueVector).StoreUnsafe(ref dest);
                current = ref Unsafe.Add(ref current, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref current, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = source[i] * value;
            }
            return;
        }
        else if (Vector128.IsHardwareAccelerated && length >= Vector128<float>.Count)
        {
            int count = Vector128<float>.Count;
            int remaining = length % count;
            ref float end = ref Unsafe.Add(ref current, length - remaining);
            Vector128<float> valueVector = Vector128.Create(value);
            do
            {
                var vector = Vector128.LoadUnsafe(ref current);
                (vector * valueVector).StoreUnsafe(ref dest);
                current = ref Unsafe.Add(ref current, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref current, ref end));
            
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = source[i] * value;
            }
            return;
        }
        for (var i = 0; i < length; i++)
        {
            destination[i] = source[i] * value;
        }
    }
    /// <summary>
    /// Applies an element-wise multiplication between every element of the source span and a single scalar value, storing the results in a destination span (destination[i] = source[i] * value). The method automatically selects the widest available SIMD width (Vector512, Vector256 or Vector128) when hardware acceleration is enabled and the input is large enough, falling back to a scalar loop for the remaining elements.
    /// </summary>
    /// <param name="source">The read-only span of double-precision floating-point values to apply the multiplication to.</param>
    /// <param name="value">The scalar double-precision value used as the second operand for every element.</param>
    /// <param name="destination">The span that receives the results. Must be at least as long as <paramref name="source"/>.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="destination"/> is shorter than <paramref name="source"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void MultiplyElementsWithValue(ReadOnlySpan<double> source, double value, Span<double> destination)
    {
        if (destination.Length < source.Length)
            ThrowIfDestinationIsSmallerThanSource();
        int length = source.Length;
        ref double current = ref MemoryMarshal.GetReference(source);
        ref double dest = ref MemoryMarshal.GetReference(destination);
        if (Vector512.IsHardwareAccelerated && length >= Vector512<double>.Count)
        {
            int count = Vector512<double>.Count;
            int remaining = length % count;
            Vector512<double> valueVector = Vector512.Create(value);
            ref double end = ref Unsafe.Add(ref current, length - (remaining));
            do
            {
                var vector = Vector512.LoadUnsafe(ref current);
                (vector * valueVector).StoreUnsafe(ref dest);
                current = ref Unsafe.Add(ref current, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref current, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = source[i] * value;
            }
            return;
        }
        else if (Vector256.IsHardwareAccelerated && length >= Vector256<double>.Count)
        {
            int count = Vector256<double>.Count;
            int remaining = length % count;
            Vector256<double> valueVector = Vector256.Create(value);
            ref double end = ref Unsafe.Add(ref current, length - remaining);
            do
            {
                var vector = Vector256.LoadUnsafe(ref current);
                (vector * valueVector).StoreUnsafe(ref dest);
                current = ref Unsafe.Add(ref current, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref current, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = source[i] * value;
            }
            return;
        }
        else if (Vector128.IsHardwareAccelerated && length >= Vector128<double>.Count)
        {

            int count = Vector128<double>.Count;
            int remaining = length % count;
            ref double end = ref Unsafe.Add(ref current, length - remaining);
            Vector128<double> valueVector = Vector128.Create(value);
            do
            {
                var vector = Vector128.LoadUnsafe(ref current);
                (vector * valueVector).StoreUnsafe(ref dest);
                current = ref Unsafe.Add(ref current, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref current, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = source[i] * value;
            }
            return;
        }
        for (var i = 0; i < length; i++)
        {
            destination[i] = source[i] * value;
        }
    }
    /// <summary>
    /// Applies an element-wise division between every element of the source span and a single scalar value, storing the results in a destination span (destination[i] = source[i] / value). The method automatically selects the widest available SIMD width (Vector512, Vector256 or Vector128) when hardware acceleration is enabled and the input is large enough, falling back to a scalar loop for the remaining elements.
    /// </summary>
    /// <param name="source">The read-only span of integers to apply the division to.</param>
    /// <param name="value">The scalar int value used as the second operand for every element.</param>
    /// <param name="destination">The span that receives the results. Must be at least as long as <paramref name="source"/>.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="destination"/> is shorter than <paramref name="source"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void DivideElementsByValue(ReadOnlySpan<int> source,int value, Span<int> destination)
    {
        if (destination.Length < source.Length)
            ThrowIfDestinationIsSmallerThanSource();
        int length = source.Length;
        ref int current = ref MemoryMarshal.GetReference(source);
        ref int dest = ref MemoryMarshal.GetReference(destination);
        if (Vector512.IsHardwareAccelerated && length >= Vector512<int>.Count)
        {
            int count = Vector512<int>.Count;
            int remaining = length % count;
            Vector512<int> valueVector = Vector512.Create<int>(value);
            ref int end = ref Unsafe.Add(ref current, length - (remaining));
            do
            {
                var vector = Vector512.LoadUnsafe(ref current);
                (vector / valueVector).StoreUnsafe(ref dest);
                current = ref Unsafe.Add(ref current, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref current, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = source[i] / value;
            }
            return;
        }
        else if (Vector256.IsHardwareAccelerated && length >= Vector256<int>.Count)
        {
            int count = Vector256<int>.Count;
            int remaining = length % count;
            Vector256<int> valueVector = Vector256.Create(value);
            ref int end = ref Unsafe.Add(ref current, length - remaining);
            do
            {
                var vector = Vector256.LoadUnsafe(ref current);
                (vector / valueVector).StoreUnsafe(ref dest);
                current = ref Unsafe.Add(ref current, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref current, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = source[i] / value;
            }
            return;
        }
        else if (Vector128.IsHardwareAccelerated && length >= Vector128<int>.Count)
        {
            int count = Vector128<int>.Count;
            int remaining = length % count;
            ref int end = ref Unsafe.Add(ref current, length - remaining);
            Vector128<int> valueVector = Vector128.Create(value);
            do
            {
                var vector = Vector128.LoadUnsafe(ref current);
                (vector / valueVector).StoreUnsafe(ref dest);
                current = ref Unsafe.Add(ref current, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref current, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = source[i] / value;
            }
            return;
        }
        for (var i = 0; i < length; i++)
        {
            destination[i] = source[i] / value;
        }
    }
    //[MethodImpl(MethodImplOptions.AggressiveInlining)]
    //public static void DivideElementsByValue(ReadOnlySpan<long> source, long value, Span<long> destination)
    //{
    //    if (destination.Length < source.Length)
    //        ThrowIfDestinationIsSmallerThanSource();
    //    int length = source.Length;
    //    ref long current = ref MemoryMarshal.GetReference(source);
    //    ref long dest = ref MemoryMarshal.GetReference(destination);
    //    if (Vector512.IsHardwareAccelerated && length >= Vector512<long>.Count)
    //    {
    //        var count = Vector512<long>.Count;
    //        int remaining = length % count;
    //        Vector512<long> valueVector = Vector512.Create<long>(value);
    //        ref long end = ref Unsafe.Add(ref current, length - (remaining));
    //        do
    //        {
    //            var vector = Vector512.LoadUnsafe(ref current);
    //            (vector / valueVector).StoreUnsafe(ref dest);
    //            current = ref Unsafe.Add(ref current, count);
    //            dest = ref Unsafe.Add(ref dest, count);
    //        } while (Unsafe.IsAddressLessThan(ref current, ref end));
    //        for (var i = length - remaining; i < length; i++)
    //        {
    //            destination[i] = source[i] / value;
    //        }
    //        return;
    //    }
    //    else if (Vector256.IsHardwareAccelerated && length >= Vector256<long>.Count)
    //    {
    //        var count = Vector256<long>.Count;
    //        int remaining = length % count;
    //        Vector256<long> valueVector = Vector256.Create(value);
    //        ref long end = ref Unsafe.Add(ref current, length - remaining);
    //        do
    //        {
    //            var vector = Vector256.LoadUnsafe(ref current);
    //            (vector / valueVector).StoreUnsafe(ref dest);
    //            current = ref Unsafe.Add(ref current, count);
    //            dest = ref Unsafe.Add(ref dest, count);
    //        } while (Unsafe.IsAddressLessThan(ref current, ref end));
    //        for (var i = length - remaining; i < length; i++)
    //        {
    //            destination[i] = source[i] / value;
    //        }
    //        return;
    //    }
    //    else if (Vector128.IsHardwareAccelerated && length >= Vector128<long>.Count)
    //    {
    //        var count = Vector128<long>.Count;
    //        int remaining = length % count;
    //        ref long end = ref Unsafe.Add(ref current, length - remaining);
    //        Vector128<long> valueVector = Vector128.Create(value);
    //        do
    //        {
    //            var vector = Vector128.LoadUnsafe(ref current);
    //            (vector / valueVector).StoreUnsafe(ref dest);
    //            current = ref Unsafe.Add(ref current, count);
    //            dest = ref Unsafe.Add(ref dest, count);
    //        } while (Unsafe.IsAddressLessThan(ref current, ref end));
    //        for (var i = length - remaining; i < length; i++)
    //        {
    //            destination[i] = source[i] / value;
    //        }
    //        return;
    //    }
    //    for (var i = 0; i < length; i++)
    //    {
    //        destination[i] = source[i] / value;
    //    }
    //}
    //[MethodImpl(MethodImplOptions.AggressiveInlining)]
    //public static void DivideElementsByValue(ReadOnlySpan<short> source, short value, Span<short> destination)
    //{
    //    if (destination.Length < source.Length)
    //        ThrowIfDestinationIsSmallerThanSource();
    //    int length = source.Length;
    //    ref short current = ref MemoryMarshal.GetReference(source);
    //    ref short dest = ref MemoryMarshal.GetReference(destination);
    //    if (Vector512.IsHardwareAccelerated && length >= Vector512<short>.Count)
    //    {
    //        int count = Vector512<short>.Count;
    //        int remaining = length % count;
    //        Vector512<short> valueVector = Vector512.Create(value);
    //        ref short end = ref Unsafe.Add(ref current, length - (remaining));
    //        do
    //        {
    //            var vector = Vector512.LoadUnsafe(ref current);
    //            (vector / valueVector).StoreUnsafe(ref dest);
    //            current = ref Unsafe.Add(ref current, count);
    //            dest = ref Unsafe.Add(ref dest, count);
    //        } while (Unsafe.IsAddressLessThan(ref current, ref end));
    //        for (var i = length - remaining; i < length; i++)
    //        {
    //            destination[i] = (short)(source[i] / value);
    //        }
    //        return;
    //    }
    //    else if (Vector256.IsHardwareAccelerated && length >= Vector256<short>.Count)
    //    {
    //        int count = Vector256<short>.Count;
    //        int remaining = length % count;
    //        Vector256<short> valueVector = Vector256.Create(value);
    //        ref short end = ref Unsafe.Add(ref current, length - remaining);
    //        do
    //        {
    //            var vector = Vector256.LoadUnsafe(ref current);
    //            (vector / valueVector).StoreUnsafe(ref dest);
    //            current = ref Unsafe.Add(ref current, count);
    //            dest = ref Unsafe.Add(ref dest, count);
    //        } while (Unsafe.IsAddressLessThan(ref current, ref end));
    //        for (var i = length - remaining; i < length; i++)
    //        {
    //            destination[i] = (short)(source[i] / value);
    //        }
    //        return;
    //    }
    //    else if (Vector128.IsHardwareAccelerated && length >= Vector128<short>.Count)
    //    {
    //        int count = Vector128<short>.Count;
    //        int remaining = length % count;
    //        ref short end = ref Unsafe.Add(ref current, length - remaining);
    //        Vector128<short> valueVector = Vector128.Create(value);
    //        do
    //        {
    //            var vector = Vector128.LoadUnsafe(ref current);
    //            (vector / valueVector).StoreUnsafe(ref dest);
    //            current = ref Unsafe.Add(ref current, count);
    //            dest = ref Unsafe.Add(ref dest, count);
    //        } while (Unsafe.IsAddressLessThan(ref current, ref end));
    //        for (var i = length - remaining; i < length; i++)
    //        {
    //            destination[i] = (short)(source[i] / value);
    //        }
    //        return;
    //    }
    //    for (var i = 0; i < length; i++)
    //    {
    //        destination[i] = (short)(source[i] / value);
    //    }
    //}
    /// <summary>
    /// Applies an element-wise division between every element of the source span and a single scalar value, storing the results in a destination span (destination[i] = source[i] / value). The method automatically selects the widest available SIMD width (Vector512, Vector256 or Vector128) when hardware acceleration is enabled and the input is large enough, falling back to a scalar loop for the remaining elements.
    /// </summary>
    /// <param name="source">The read-only span of single-precision floating-point values to apply the division to.</param>
    /// <param name="value">The scalar single-precision value used as the second operand for every element.</param>
    /// <param name="destination">The span that receives the results. Must be at least as long as <paramref name="source"/>.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="destination"/> is shorter than <paramref name="source"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void DivideElementsByValue(ReadOnlySpan<float> source, float value, Span<float> destination)
    {
        if (destination.Length < source.Length)
            ThrowIfDestinationIsSmallerThanSource();
        int length = source.Length;
        ref float current = ref MemoryMarshal.GetReference(source);
        ref float dest = ref MemoryMarshal.GetReference(destination);
        if (Vector512.IsHardwareAccelerated && length >= Vector512<float>.Count)
        {
            int count = Vector512<float>.Count;
            int remaining = length % count;
            Vector512<float> valueVector = Vector512.Create(value);
            ref float end = ref Unsafe.Add(ref current, length - (remaining));
            do
            {
                var vector = Vector512.LoadUnsafe(ref current);
                (vector / valueVector).StoreUnsafe(ref dest);
                current = ref Unsafe.Add(ref current, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref current, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = source[i] / value;
            }
            return;
        }
        else if (Vector256.IsHardwareAccelerated && length >= Vector256<float>.Count)
        {
            int count = Vector256<float>.Count;
            int remaining = length % count;
            Vector256<float> valueVector = Vector256.Create(value);
            ref float end = ref Unsafe.Add(ref current, length - remaining);
            do
            {
                var vector = Vector256.LoadUnsafe(ref current);
                (vector / valueVector).StoreUnsafe(ref dest);
                current = ref Unsafe.Add(ref current, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref current, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = source[i] / value;
            }
            return;
        }
        else if (Vector128.IsHardwareAccelerated && length >= Vector128<float>.Count)
        {
            int count = Vector128<float>.Count;
            int remaining = length % count;
            ref float end = ref Unsafe.Add(ref current, length - remaining);
            Vector128<float> valueVector = Vector128.Create(value);
            do
            {
                var vector = Vector128.LoadUnsafe(ref current);
                (vector / valueVector).StoreUnsafe(ref dest);
                current = ref Unsafe.Add(ref current, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref current, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = source[i] / value;
            }
            return;
        }
        for (var i = 0; i < length; i++)
        {
            destination[i] = source[i] / value;
        }
    }
    /// <summary>
    /// Applies an element-wise division between every element of the source span and a single scalar value, storing the results in a destination span (destination[i] = source[i] / value). The method automatically selects the widest available SIMD width (Vector512, Vector256 or Vector128) when hardware acceleration is enabled and the input is large enough, falling back to a scalar loop for the remaining elements.
    /// </summary>
    /// <param name="source">The read-only span of double-precision floating-point values to apply the division to.</param>
    /// <param name="value">The scalar double-precision value used as the second operand for every element.</param>
    /// <param name="destination">The span that receives the results. Must be at least as long as <paramref name="source"/>.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="destination"/> is shorter than <paramref name="source"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void DivideElementsByValue(ReadOnlySpan<double> source, double value, Span<double> destination)
    {
        if (destination.Length < source.Length)
            ThrowIfDestinationIsSmallerThanSource();
        int length = source.Length;
        ref double current = ref MemoryMarshal.GetReference(source);
        ref double dest = ref MemoryMarshal.GetReference(destination);
        if (Vector512.IsHardwareAccelerated && length >= Vector512<double>.Count)
        {
            int count = Vector512<double>.Count;
            int remaining = length % count;
            Vector512<double> valueVector = Vector512.Create(value);
            ref double end = ref Unsafe.Add(ref current, length - (remaining));
            do
            {
                var vector = Vector512.LoadUnsafe(ref current);
                (vector / valueVector).StoreUnsafe(ref dest);
                current = ref Unsafe.Add(ref current, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref current, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = source[i] / value;
            }
            return;
        }
        else if (Vector256.IsHardwareAccelerated && length >= Vector256<double>.Count)
        {
            int count = Vector256<double>.Count;
            int remaining = length % count;
            Vector256<double> valueVector = Vector256.Create(value);
            ref double end = ref Unsafe.Add(ref current, length - remaining);
            do
            {
                var vector = Vector256.LoadUnsafe(ref current);
                (vector / valueVector).StoreUnsafe(ref dest);
                current = ref Unsafe.Add(ref current, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref current, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = source[i] / value;
            }
            return;
        }
        else if (Vector128.IsHardwareAccelerated && length >= Vector128<double>.Count)
        {

            int count = Vector128<double>.Count;
            int remaining = length % count;
            ref double end = ref Unsafe.Add(ref current, length - remaining);
            Vector128<double> valueVector = Vector128.Create(value);
            do
            {
                var vector = Vector128.LoadUnsafe(ref current);
                (vector / valueVector).StoreUnsafe(ref dest);
                current = ref Unsafe.Add(ref current, count);
                dest = ref Unsafe.Add(ref dest, count);
            } while (Unsafe.IsAddressLessThan(ref current, ref end));
            for (var i = length - remaining; i < length; i++)
            {
                destination[i] = source[i] / value;
            }
            return;
        }
        for (var i = 0; i < length; i++)
        {
            destination[i] = source[i] / value;
        }
    }

    /// <summary>
    /// Computes the floored (Euclidean-style) modulo of a value with respect to a divisor, guaranteeing a non-negative result even when <paramref name="value"/> is negative.
    /// </summary>
    /// <param name="value">The value to take the modulo of.</param>
    /// <param name="divisor">The divisor.</param>
    /// <returns>The remainder of <paramref name="value"/> divided by <paramref name="divisor"/>, normalized to the range [0, |divisor|).</returns>
    public static float Mod(float value, float divisor)
    {
        return ((value % divisor) + divisor) % divisor;
    }
    /// <summary>
    /// Determines whether the specified byte sequence contains the given byte value. Uses SIMD-accelerated searches (Vector512/Vector256/Vector128) when available and falls back to a scalar loop otherwise.
    /// </summary>
    /// <param name="haystack">The read-only span of bytes to search.</param>
    /// <param name="needle">The byte value to look for.</param>
    /// <returns><c>true</c> if <paramref name="needle"/> occurs at least once in <paramref name="haystack"/>; otherwise, <c>false</c>.</returns>
    private static bool Contains(ReadOnlySpan<byte> haystack, byte needle)
    {
        if (Vector128.IsHardwareAccelerated && haystack.Length >= Vector128<byte>.Count)
        {
            ref byte current = ref MemoryMarshal.GetReference(haystack);

            if (Vector512.IsHardwareAccelerated && haystack.Length >= Vector512<byte>.Count)
            {
                Vector512<byte> target = Vector512.Create(needle);
                ref byte endMinusOneVector = ref Unsafe.Add(ref current, haystack.Length - Vector512<byte>.Count);
                do
                {
                    if (Vector512.EqualsAny(target, Vector512.LoadUnsafe(ref current)))
                        return true;

                    current = ref Unsafe.Add(ref current, Vector512<byte>.Count);
                }
                while (Unsafe.IsAddressLessThan(ref current, ref endMinusOneVector));

                if (Vector512.EqualsAny(target, Vector512.LoadUnsafe(ref endMinusOneVector)))
                    return true;
            }
            else if (Vector256.IsHardwareAccelerated && haystack.Length >= Vector256<byte>.Count)
            {
                Vector256<byte> target = Vector256.Create(needle);
                ref byte endMinusOneVector = ref Unsafe.Add(ref current, haystack.Length - Vector256<byte>.Count);
                do
                {
                    if (Vector256.EqualsAny(target, Vector256.LoadUnsafe(ref current)))
                        return true;

                    current = ref Unsafe.Add(ref current, Vector256<byte>.Count);
                }
                while (Unsafe.IsAddressLessThan(ref current, ref endMinusOneVector));

                if (Vector256.EqualsAny(target, Vector256.LoadUnsafe(ref endMinusOneVector)))
                    return true;
            }
            else
            {
                Vector128<byte> target = Vector128.Create(needle);
                ref byte endMinusOneVector = ref Unsafe.Add(ref current, haystack.Length - Vector128<byte>.Count);
                do
                {
                    if (Vector128.EqualsAny(target, Vector128.LoadUnsafe(ref current)))
                        return true;

                    current = ref Unsafe.Add(ref current, Vector128<byte>.Count);
                }
                while (Unsafe.IsAddressLessThan(ref current, ref endMinusOneVector));

                if (Vector128.EqualsAny(target, Vector128.LoadUnsafe(ref endMinusOneVector)))
                    return true;
            }
        }
        else
        {
            for (int i = 0; i < haystack.Length; i++)
                if (haystack[i] == needle)
                    return true;
        }

        return false;
    }
    /// <summary>
    /// Throws an <see cref="ArgumentException"/> indicating that the supplied spans do not all have the same length. Used as a no-inline guard clause helper.
    /// </summary>
    /// <param name="caller">The name of the calling member, supplied automatically via <see cref="CallerMemberNameAttribute"/>.</param>
    /// <exception cref="ArgumentException">Always thrown, with a message describing the length mismatch and the calling member as the parameter name.</exception>
    private static void ThrowIfLengthsAreNotTheSame([CallerMemberName] string caller = "")
    {
        throw new ArgumentException("The lengths of spans should be the same", caller);
    }
    /// <summary>
    /// Throws an <see cref="ArgumentException"/> indicating that the destination span is shorter than the source span. Used as a no-inline guard clause helper.
    /// </summary>
    /// <param name="caller">The name of the calling member, supplied automatically via <see cref="CallerMemberNameAttribute"/>.</param>
    /// <exception cref="ArgumentException">Always thrown, with a message describing the size requirement and the calling member as the parameter name.</exception>
    private static void ThrowIfDestinationIsSmallerThanSource([CallerMemberName] string caller = "")
    {
        throw new ArgumentException("The length of the source should be smaller than destination", caller);
    }
}
