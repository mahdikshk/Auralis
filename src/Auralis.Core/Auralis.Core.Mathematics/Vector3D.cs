using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Text;

namespace Auralis.Core.Mathematics;

[StructLayout(LayoutKind.Sequential, Pack = 8)]
public struct Vector3D
{
    /// <summary>
    /// The size of the <see cref="Auralis.Core.Mathematics.Vector3D"/> struct in bytes
    /// </summary>
    public static readonly int SizeInBytes = Unsafe.SizeOf<Vector3>();

    /// <summary>
    /// A <see cref="Auralis.Core.Mathematics.Vector3D"/> instance with all the values instanciated to 0
    /// </summary>
    public static readonly Vector3D Zero = new();
    /// <summary>
    /// A <see cref="Auralis.Core.Mathematics.Vector3D"/> instance with all the values being set to 1
    /// </summary>
    public static readonly Vector3D One = new(1f, 1f, 1f);
    /// <summary>
    /// A <see cref="Auralis.Core.Mathematics.Vector3D"/> instance with the X value being set to 1
    /// </summary>
    public static readonly Vector3D UnitX = new(1f, 0, 0);
    /// <summary>
    /// A <see cref="Auralis.Core.Mathematics.Vector3D"/> instance with the Y value being set to 1
    /// </summary>
    public static readonly Vector3D UnitY = new(0, 1f, 0);
    /// <summary>
    /// A <see cref="Auralis.Core.Mathematics.Vector3D"/> instance with the Z value being set to 1
    /// </summary>
    public static readonly Vector3D UnitZ = new(0, 0, 1f);


    public Vector3D()
    {

    }

    public Vector3D(float x, float y, float z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public Vector3D(float value)
    {
        X = Y = Z = value;
    }

    public Vector3D(Vector2D value, float z)
    {
        X = value.X;
        Y = value.Y;
        Z = z;
    }
    public Vector3D(ReadOnlySpan<float> values)
    {
        if (values.Length != 3)
        {
            throw new ArgumentOutOfRangeException(nameof(values), "The length of the span should be exactly 3");
        }
        X = values[0];
        Y = values[1];
        Z = values[2];
    }
    public float X;

    public float Y;

    public float Z;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3D operator *(in Vector3D value, float scale)
    {
        return new Vector3D(value.X * scale, value.Y * scale, value.Z * scale);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3D operator *(float scale, in Vector3D value)
    {
        return new Vector3D(value.X * scale, value.Y * scale, value.Z * scale);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3D operator -(in Vector3D left, in Vector3D right)
    {
        return new Vector3D(left.X - right.X, left.Y - right.Y, left.Z - right.Z);
    }

    public static Vector3D operator -(in Vector3D value)
    {
        return Vector3D.Negate(value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3D operator +(in Vector3D left, in Vector3D right)
    {
        return new Vector3D
        {
            X = left.X + right.X,
            Y = left.Y + right.Y,
            Z = left.Z + right.Z
        };
    }
    /// <summary>
    /// Gets a value indicating whether this instance is normalized.
    /// </summary>
    public readonly bool IsNormalized
    {
        get
        {
            return MathF.Abs((X * X) + (Y * Y) + (Z * Z) - 1f) < MathUtilities.ZeroTolerance;
        }
    }
    /// <summary>
    /// Gets or sets the component at the specified index.
    /// </summary>
    /// <value>The value of the X, Y, or Z component, depending on the index.</value>
    /// <param name="index">The index of the component to access. Use 0 for the X component, 1 for the Y component, and 2 for the Z component.</param>
    /// <returns>The value of the component at the specified index.</returns>
    public float this[int index]
    {
        get
        {
            switch (index)
            {
                case 0: return X;
                case 1: return Y;
                case 2: return Z;
            }

            throw new ArgumentOutOfRangeException(nameof(index), "The index should be in the range of 0 to 2");
        }

        set
        {
            switch (index)
            {
                case 0: X = value; break;
                case 1: Y = value; break;
                case 2: Z = value; break;
                default: throw new ArgumentOutOfRangeException(nameof(index), "The index should be in the range of 0 to 2");
            }
        }
    }
    public static implicit operator Vector3D(Vector3 value)
    {
        return Unsafe.As<Vector3, Vector3D>(ref value);
    }
    public static implicit operator Vector3(Vector3D value)
    {
        return Unsafe.As<Vector3D, Vector3>(ref value);
    }
    public readonly float Length()
    {
        return MathF.Sqrt((X * X) + (Y * Y) + (Z * Z));
    }
    public readonly float LengthSquared()
    {
        return (X * X) + (Y * Y) + (Z * Z);
    }
    /// <summary>
    /// Converts the vector into a unit vector.
    /// </summary>
    public void Normalize()
    {
        Vector3.Normalize(this);
    }
    /// <summary>
    /// Raises the exponent for each components.
    /// </summary>
    public void Pow(float exponent)
    {
        X = MathF.Pow(X, exponent);
        Y = MathF.Pow(Y, exponent);
        Z = MathF.Pow(Z, exponent);
    }
    public readonly float[] ToArray()
    {
        return [X, Y, Z];
    }

    public Vector3D MoveTo(Vector3D from, Vector3D to, float maximumTravelDistance)
    {
        var distance = Subtract(to, from);
        float length = distance.Length();
        if (maximumTravelDistance >= length || length == 0)
            return to;
        return new Vector3D(from.X + distance.X / length * maximumTravelDistance, from.Y + distance.Y / length * maximumTravelDistance, from.Z + distance.Z / length * maximumTravelDistance);

    }

    public static Vector3D Subtract(Vector3D left, Vector3D right)
    {
        return (Vector3)left - right;
    }

    public static Vector3D Add(Vector3D left, Vector3D right)
    {
        return (Vector3)left + right;
    }

    public static Vector3D Multiply(Vector3D value, float scale)
    {
        return (Vector3)value * scale;
    }

    public static Vector3D Modulate(Vector3D left, Vector3D right)
    {
        return (Vector3)left + right;
    }

    public static Vector3D Divide(Vector3D value, float scale)
    {
        return (Vector3)value / scale;
    }

    public static Vector3D Demodulate(Vector3D right, Vector3D left)
    {
        return (Vector3)right / left;
    }

    public static Vector3D Negate(Vector3D value)
    {
        return Multiply(value, -1);
    }

    public static Vector3D Barycentric(Vector3 value1, Vector3 value2, Vector3 value3, float amount1, float amount2)
    {
        return new Vector3(
            (value1.X + (amount1 * (value2.X - value1.X))) + (amount2 * (value3.X - value1.X)),
            (value1.Y + (amount1 * (value2.Y - value1.Y))) + (amount2 * (value3.Y - value1.Y)),
            (value1.Z + (amount1 * (value2.Z - value1.Z))) + (amount2 * (value3.Z - value1.Z)));
    }

    public static Vector3D Clamp(Vector3D value, Vector3D min, Vector3D max)
    {
        return Vector3.Clamp(value, min, max);
    }

    public static Vector3D Cross(Vector3D left, Vector3D right)
    {
        return Vector3.Cross(left, right);
    }

    public static float Distance(Vector3D left, Vector3D right)
    {
        return Vector3.Distance(left, right);
    }
    public static float DistanceSquared(Vector3D left, Vector3D right)
    {
        return Vector3.DistanceSquared(left, right);
    }

    public static float Dot(Vector3D left, Vector3D right)
    {
        return Vector3.Dot(left, right);
    }

    public static Vector3D Normalize(Vector3D value)
    {
        return Vector3.Normalize(value);
    }

    public static Vector3D Lerp(Vector3D start, Vector3D end, float amount)
    {
        return Vector3.Lerp(start, end, amount);
    }
    public static Vector3D SmoothStep(Vector3D start, Vector3D end, float amount)
    {
        amount = (amount > 1.0f) ? 1.0f : ((amount < 0.0f) ? 0.0f : amount);
        amount = (amount * amount) * (3.0f - (2.0f * amount));
        var result = new Vector3D
        {
            X = start.X + ((end.X - start.X) * amount),
            Y = start.Y + ((end.Y - start.Y) * amount),
            Z = start.Z + ((end.Z - start.Z) * amount)
        };
        return result;
    }

    public static Vector3D Hermite(Vector3D value1, Vector3D tangent1, Vector3D value2, Vector3 tangent2, float amount)
    {
        float squared = amount * amount;
        float cubed = amount * squared;
        float part1 = ((2.0f * cubed) - (3.0f * squared)) + 1.0f;
        float part2 = (-2.0f * cubed) + (3.0f * squared);
        float part3 = (cubed - (2.0f * squared)) + amount;
        float part4 = cubed - squared;

        return new Vector3D
        {
            X = (((value1.X * part1) + (value2.X * part2)) + (tangent1.X * part3)) + (tangent2.X * part4),
            Y = (((value1.Y * part1) + (value2.Y * part2)) + (tangent1.Y * part3)) + (tangent2.Y * part4),
            Z = (((value1.Z * part1) + (value2.Z * part2)) + (tangent1.Z * part3)) + (tangent2.Z * part4)
        };
    }
    public static Vector3D CatmullRom(Vector3D value1, Vector3D value2, Vector3D value3, Vector3D value4, float amount)
    {
        float squared = amount * amount;
        float cubed = amount * squared;

        return new Vector3D
        {
            X = 0.5f * ((((2.0f * value2.X) + ((-value1.X + value3.X) * amount)) +
        (((((2.0f * value1.X) - (5.0f * value2.X)) + (4.0f * value3.X)) - value4.X) * squared)) +
        ((((-value1.X + (3.0f * value2.X)) - (3.0f * value3.X)) + value4.X) * cubed)),
            Y = 0.5f * ((((2.0f * value2.Y) + ((-value1.Y + value3.Y) * amount)) +
            (((((2.0f * value1.Y) - (5.0f * value2.Y)) + (4.0f * value3.Y)) - value4.Y) * squared)) +
            ((((-value1.Y + (3.0f * value2.Y)) - (3.0f * value3.Y)) + value4.Y) * cubed)),
            Z = 0.5f * ((((2.0f * value2.Z) + ((-value1.Z + value3.Z) * amount)) +
            (((((2.0f * value1.Z) - (5.0f * value2.Z)) + (4.0f * value3.Z)) - value4.Z) * squared)) +
            ((((-value1.Z + (3.0f * value2.Z)) - (3.0f * value3.Z)) + value4.Z) * cubed))
        };
    }

    public static Vector3D Mod(Vector3D left, Vector3D right)
    {
        return new Vector3D
        {
            X = MathUtilities.Mod(left.X, right.X),
            Y = MathUtilities.Mod(left.Y, right.Y),
            Z = MathUtilities.Mod(left.Z, right.Z)
        };
    }

    public static Vector3D Min(Vector3D left, Vector3D right)
    {
        return new Vector3D
        {
            X = Math.Min(left.X, right.X),
            Y = Math.Min(left.Y, right.Y),
            Z = Math.Min(left.Z, right.Z)
        };
    }

    public static Vector3D Max(Vector3D left, Vector3D right)
    {
        return new Vector3D
        {
            X = Math.Max(left.X, right.X),
            Y = Math.Max(left.Y, right.Y),
            Z = Math.Max(left.Z, right.Z)
        };
    }

    public static Vector3D Project(Vector3 vector, float x, float y, float width, float height, float minZ, float maxZ, Matrix worldViewProjection)
    {
        Vector3D trasformed = Vector3.Transform(vector, worldViewProjection);
        return new Vector3(((1.0f + trasformed.X) * 0.5f * width) + x, ((1.0f - trasformed.Y) * 0.5f * height) + y, (trasformed.Z * (maxZ - minZ)) + minZ);
    }

    public static Vector3D Unproject(ref readonly Vector3D vector,
        float x,
        float y,
        float width,
        float height,
        float minZ,
        float maxZ,
        ref readonly Matrix worldViewProjection)
    {
        Vector3D v = new();
        bool isValid = Matrix.Invert(worldViewProjection, out var resultMatrix);
        if (!isValid)
            throw new ArgumentException("The matrix can't be inverted", nameof(worldViewProjection));
        v.X = (((vector.X - x) / width) * 2.0f) - 1.0f;
        v.Y = -((((vector.Y - y) / height) * 2.0f) - 1.0f);
        v.Z = (vector.Z - minZ) / (maxZ - minZ);
        var result = TransformCoordinate(in v, in resultMatrix);
        return result;
    }
    public static Vector3D Reflect(Vector3D vector, Vector3D normal)
    {
        return Vector3.Reflect(vector, normal);
    }
    public static void Orthogonalize(ReadOnlySpan<Vector3D> source, Span<Vector3D> destination)
    {
        if (destination.Length < source.Length)
            throw new ArgumentOutOfRangeException(nameof(destination), "The destination array must be of same length or larger length than the source array.");
        for (var i = 0; i < source.Length; i++)
        {
            Vector3D newvector = source[i];
            for (int r = 0; r < i; ++r)
            {
                newvector -= (Dot(destination[r], newvector) / Dot(destination[r], destination[r])) * destination[r];
            }

            destination[i] = newvector;
        }
    }
    public static void Orthonormalize(ReadOnlySpan<Vector3D> source, Span<Vector3D> destination)
    {
        if (destination.Length < source.Length)
            throw new ArgumentOutOfRangeException(nameof(destination), "The destination array must be of same length or larger length than the source array.");
        for (int i = 0; i < source.Length; ++i)
        {
            Vector3D newvector = source[i];

            for (int r = 0; r < i; ++r)
            {
                newvector -= Dot(destination[r], newvector) * destination[r];
            }

            newvector.Normalize();
            destination[i] = newvector;
        }
    }

    public static void Transform(ReadOnlySpan<Vector3D> source, Quaternion rotation, Span<Vector3D> destination)
    {
        if (destination.Length < source.Length)
            throw new ArgumentOutOfRangeException(nameof(destination), "The destination array must be of same length or larger length than the source array.");
        System.Numerics.Quaternion quaternion = rotation;
        for (int i = 0; i < source.Length; i++)
        {
            Vector3 newVector = source[i];
            destination[i] = Vector3.Transform(newVector, quaternion);
        }
    }
    public static Vector4D Transform(ref readonly Vector3D vector, ref readonly Matrix transform)
    {
        return Vector4.Transform(vector, transform);
    }
    //public static Vector3D Transform(ref readonly Vector3D vector, ref readonly Matrix transform)
    //{
    //    return new Vector3D(
    //        (vector.X * transform.M11) + (vector.Y * transform.M21) + (vector.Z * transform.M31) + transform.M41,
    //        (vector.X * transform.M12) + (vector.Y * transform.M22) + (vector.Z * transform.M32) + transform.M42,
    //        (vector.X * transform.M13) + (vector.Y * transform.M23) + (vector.Z * transform.M33) + transform.M43);
    //}

    public static void Transform(ReadOnlySpan<Vector3D> source, ref readonly Matrix transform, Span<Vector4D> destination)
    {
        if (destination.Length < source.Length)
            throw new ArgumentOutOfRangeException(nameof(destination), "The destination array must be of same length or larger length than the source array.");

        for (int i = 0; i < source.Length; ++i)
        {
            destination[i] = Transform(in source[i], in transform);
        }
    }
    public static Vector3D TransformCoordinate(ref readonly Vector3D coordinate, ref readonly Matrix transform)
    {
        return Vector3.Transform(coordinate, transform);
    }
    public static void TransformNormal(ReadOnlySpan<Vector3D> source, ref readonly Matrix transform, Span<Vector3D> destination)
    {
        if (destination.Length < source.Length)
            throw new ArgumentOutOfRangeException(nameof(destination), "The destination array must be of same length or larger length than the source array.");
        Matrix4x4 matrix = transform;
        for (int i = 0; i < source.Length; ++i)
        {
            destination[i] = Vector3.TransformNormal(source[i], matrix);
        }
    }
    public static Vector3D RotationYawPitchRoll(Quaternion quaternion)
    {
        Vector3D yawPitchRoll;
        Quaternion.RotationYawPitchRoll(ref quaternion, out yawPitchRoll.X, out yawPitchRoll.Y, out yawPitchRoll.Z);
        return yawPitchRoll;
    }

    public static Vector3D RotateAround(Vector3D source, Vector3D target, Vector3D axis, float angle)
    {
        Vector3D local = source - target;
        Quaternion q = Quaternion.RotationAxis(axis, angle);
        q.Rotate(ref local);
        return target + local;
    }
}
