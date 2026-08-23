using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;

namespace Auralis.Core.Mathematics;

public struct Vector4D
{
    /// <summary>
    /// The size of the <see cref="Vector4"/> type, in bytes.
    /// </summary>
    public static readonly int SizeInBytes = Unsafe.SizeOf<Vector4D>();

    /// <summary>
    /// A <see cref="Vector4D"/> with all of its components set to zero.
    /// </summary>
    public static readonly Vector4D Zero = new();

    /// <summary>
    /// The X unit <see cref="Vector4D"/> (1, 0, 0, 0).
    /// </summary>
    public static readonly Vector4D UnitX = new(1.0f, 0.0f, 0.0f, 0.0f);

    /// <summary>
    /// The Y unit <see cref="Vector4D"/> (0, 1, 0, 0).
    /// </summary>
    public static readonly Vector4 UnitY = new(0.0f, 1.0f, 0.0f, 0.0f);

    /// <summary>
    /// The Z unit <see cref="Vector4D"/> (0, 0, 1, 0).
    /// </summary>
    public static readonly Vector4D UnitZ = new(0.0f, 0.0f, 1.0f, 0.0f);

    /// <summary>
    /// The W unit <see cref="Vector4D"/> (0, 0, 0, 1).
    /// </summary>
    public static readonly Vector4 UnitW = new(0.0f, 0.0f, 0.0f, 1.0f);

    /// <summary>
    /// A <see cref="Vector4D"/> with all of its components set to one.
    /// </summary>
    public static readonly Vector4 One = new(1.0f, 1.0f, 1.0f, 1.0f);

    public float X;
    public float Y;
    public float Z;
    public float W;

    public Vector4D(float value)
    {
        X = value;
        Y = value;
        Z = value;
        W = value;
    }

    public Vector4D(float x, float y, float z, float w)
    {
        X = x;
        Y = y;
        Z = z;
        W = w;
    }
    public Vector4D(Vector3 value, float w)
    {
        X = value.X;
        Y = value.Y;
        Z = value.Z;
        W = w;
    }
    public Vector4D(Vector2 value, float z, float w)
    {
        X = value.X;
        Y = value.Y;
        Z = z;
        W = w;
    }
    public Vector4D(ReadOnlySpan<float> values)
    {
        if (values.Length != 4)
            throw new ArgumentOutOfRangeException(nameof(values), "There must be four and only four input values for Vector4.");

        X = values[0];
        Y = values[1];
        Z = values[2];
        W = values[3];
    }
    public static Vector4D operator *(float scale, Vector4D value)
    {
        return new Vector4D(value.X * scale, value.Y * scale, value.Z * scale, value.W * scale);
    }
    public static Vector4D operator-(Vector4D left,Vector4D right)
    {
        return new Vector4D((left.X - right.X) , (left.Y - right.Y) , (left.Z - right.Z) ,(left.W - right.W));
    }
    public readonly bool IsNormalized
    {
        get { return MathF.Abs((X * X) + (Y * Y) + (Z * Z) + (W * W) - 1f) < MathUtilities.ZeroTolerance; }
    }
    public float this[int index]
    {
        readonly get
        {
            return index switch
            {
                0 => X,
                1 => Y,
                2 => Z,
                3 => W,
                _ => throw new ArgumentOutOfRangeException(nameof(index), "Indices for Vector4 run from 0 to 3, inclusive."),
            };
        }

        set
        {
            switch (index)
            {
                case 0: X = value; break;
                case 1: Y = value; break;
                case 2: Z = value; break;
                case 3: W = value; break;
                default: throw new ArgumentOutOfRangeException(nameof(index), "Indices for Vector4 run from 0 to 3, inclusive.");
            }
        }
    }

    public readonly float Length()
    {
        //return MathF.Sqrt((X * X) + (Y * Y) + (Z * Z) + (W * W));
        return ((Vector4)this).Length();
    }

    public readonly float LengthSquared()
    {
        //return (X * X) + (Y * Y) + (Z * Z) + (W * W);
        return ((Vector4)this).LengthSquared();
    }
    public void Normalize()
    {
        float length = Length();
        if (length > MathUtilities.ZeroTolerance)
        {
            float inverse = 1.0f / length;
            X *= inverse;
            Y *= inverse;
            Z *= inverse;
            W *= inverse;
        }
    }

    public void Pow(float exponent)
    {
        X = MathF.Pow(X, exponent);
        Y = MathF.Pow(Y, exponent);
        Z = MathF.Pow(Z, exponent);
        W = MathF.Pow(W, exponent);
    }
    public readonly float[] ToArray()
    {
        return [X, Y, Z, W];
    }

    public static Vector4D Moveto(Vector4D from, Vector4D to, float maxTravelDistance)
    {
        Vector4D distance = Vector4.Subtract(to, from);

        float length = distance.Length();

        if (maxTravelDistance >= length || length == 0)
        {
            return to;
        }
        else
        {
            var v = 1f / length * maxTravelDistance;
            return new Vector4D(from.X + (distance.X * v), from.Y + (distance.Y * v), from.Z + (distance.Z * v), from.W + (distance.W * v));
        }
    }
    public static Vector4D Add(Vector4D left, Vector4D right)
    {
        return Vector4.Add(left, right);
    }
    public static Vector4D Subtract(Vector4D left, Vector4D right)
    {
        return Vector4.Subtract(left, right);
    }
    public static Vector4D Multiply(Vector4D value, float scale)
    {
        return Vector4.Multiply(value, scale);
    }

    public static Vector4D Modulate(Vector4D left, Vector4D right)
    {
        return Vector4.Multiply(left, right);
    }

    public static Vector4D Divide(Vector4D value, float scale)
    {
        return Vector4.Divide(value, scale);
    }

    public static Vector4D Demodulate(Vector4D left, Vector4D right)
    {
        return Vector4.Divide(left, right);
    }
    public static Vector4D Negate(Vector4D value)
    {
        return Vector4.Negate(value);
    }
    public static Vector4D Barycentric(Vector4D value1, Vector4D value2, Vector4D value3, float amount1, float amount2)
    {
        return new Vector4D(
            value1.X + (amount1 * (value2.X - value1.X)) + (amount2 * (value3.X - value1.X)),
            value1.Y + (amount1 * (value2.Y - value1.Y)) + (amount2 * (value3.Y - value1.Y)),
            value1.Z + (amount1 * (value2.Z - value1.Z)) + (amount2 * (value3.Z - value1.Z)),
            value1.W + (amount1 * (value2.W - value1.W)) + (amount2 * (value3.W - value1.W)));
    }
    public static Vector4D Clamp(Vector4D value, Vector4D min, Vector4D max)
    {
        return Vector4.Clamp(value, min, max);
    }
    public static float Distance(Vector4D value1, Vector4D value2)
    {
        return Vector4.Distance(value1, value2);
    }

    public static float DistanceSquared(Vector4 value1, Vector4 value2)
    {
        return Vector4.DistanceSquared(value1, value2);
    }

    public static float Dot(Vector4D left, Vector4D right)
    {
        return Vector4.Dot(left, right);
    }

    public static Vector4D Normalize(Vector4D value)
    {
        return Vector4.Normalize(value);
    }

    public static Vector4D Lerp(Vector4D start, Vector4D end, float amount)
    {
        return Vector4.Lerp(start, end, amount);
    }

    public static Vector4D SmoothStep(Vector4D start, Vector4D end, float amount)
    {
        amount = (amount > 1.0f) ? 1.0f : ((amount < 0.0f) ? 0.0f : amount);
        amount = amount * amount * (3.0f - (2.0f * amount));
        Vector4D result;
        result.X = start.X + ((end.X - start.X) * amount);
        result.Y = start.Y + ((end.Y - start.Y) * amount);
        result.Z = start.Z + ((end.Z - start.Z) * amount);
        result.W = start.W + ((end.W - start.W) * amount);
        return result;
    }
    public static Vector4D Hermite(Vector4D value1, Vector4D tangent1, Vector4D value2, Vector4D tangent2, float amount)
    {
        float squared = amount * amount;
        float cubed = amount * squared;
        float part1 = (2.0f * cubed) - (3.0f * squared) + 1.0f;
        float part2 = (-2.0f * cubed) + (3.0f * squared);
        float part3 = cubed - (2.0f * squared) + amount;
        float part4 = cubed - squared;

        return new Vector4(
            (value1.X * part1) + (value2.X * part2) + (tangent1.X * part3) + (tangent2.X * part4),
            (value1.Y * part1) + (value2.Y * part2) + (tangent1.Y * part3) + (tangent2.Y * part4),
            (value1.Z * part1) + (value2.Z * part2) + (tangent1.Z * part3) + (tangent2.Z * part4),
            (value1.W * part1) + (value2.W * part2) + (tangent1.W * part3) + (tangent2.W * part4));
    }

    public static Vector4D CatmullRom(Vector4D value1, Vector4D value2, Vector4D value3, Vector4D value4, float amount)
    {
        float squared = amount * amount;
        float cubed = amount * squared;

        Vector4D result;
        result.X = 0.5f * ((2.0f * value2.X) + ((-value1.X + value3.X) * amount) + (((((2.0f * value1.X) - (5.0f * value2.X)) + (4.0f * value3.X)) - value4.X) * squared) + ((((-value1.X + (3.0f * value2.X)) - (3.0f * value3.X)) + value4.X) * cubed));
        result.Y = 0.5f * ((2.0f * value2.Y) + ((-value1.Y + value3.Y) * amount) + (((((2.0f * value1.Y) - (5.0f * value2.Y)) + (4.0f * value3.Y)) - value4.Y) * squared) + ((((-value1.Y + (3.0f * value2.Y)) - (3.0f * value3.Y)) + value4.Y) * cubed));
        result.Z = 0.5f * ((2.0f * value2.Z) + ((-value1.Z + value3.Z) * amount) + (((((2.0f * value1.Z) - (5.0f * value2.Z)) + (4.0f * value3.Z)) - value4.Z) * squared) + ((((-value1.Z + (3.0f * value2.Z)) - (3.0f * value3.Z)) + value4.Z) * cubed));
        result.W = 0.5f * ((2.0f * value2.W) + ((-value1.W + value3.W) * amount) + (((((2.0f * value1.W) - (5.0f * value2.W)) + (4.0f * value3.W)) - value4.W) * squared) + ((((-value1.W + (3.0f * value2.W)) - (3.0f * value3.W)) + value4.W) * cubed));
        return result;
    }
    public static Vector4D Max(Vector4D left, Vector4D right)
    {
        return Vector4.Max(left, right);
    }
    public static Vector4D Min(Vector4D left,Vector4D right)
    {
        return Vector4.Min(left, right);
    }

    public static void Orthogonalize(Span<Vector4D> destination, params ReadOnlySpan<Vector4D> source)
    {
        if (destination.Length < source.Length)
            throw new ArgumentOutOfRangeException(nameof(destination), "The destination array must be of same length or larger length than the source array.");

        for (int i = 0; i < source.Length; ++i)
        {
            Vector4D newvector = source[i];

            for (int r = 0; r < i; ++r)
            {
                newvector -= Dot(destination[r], newvector) / Dot(destination[r], destination[r]) * destination[r];
            }

            destination[i] = newvector;
        }
    }

    public static void Orthonormalize(Span<Vector4D> destination, params ReadOnlySpan<Vector4D> source)
    {
        //Uses the modified Gram-Schmidt process.
        //Because we are making unit vectors, we can optimize the math for orthogonalization
        //and simplify the projection operation to remove the division.
        //q1 = m1 / |m1|
        //q2 = (m2 - (q1 ⋅ m2) * q1) / |m2 - (q1 ⋅ m2) * q1|
        //q3 = (m3 - (q1 ⋅ m3) * q1 - (q2 ⋅ m3) * q2) / |m3 - (q1 ⋅ m3) * q1 - (q2 ⋅ m3) * q2|
        //q4 = (m4 - (q1 ⋅ m4) * q1 - (q2 ⋅ m4) * q2 - (q3 ⋅ m4) * q3) / |m4 - (q1 ⋅ m4) * q1 - (q2 ⋅ m4) * q2 - (q3 ⋅ m4) * q3|
        //q5 = ...

        if (destination.Length < source.Length)
            throw new ArgumentOutOfRangeException(nameof(destination), "The destination array must be of same length or larger length than the source array.");

        for (int i = 0; i < source.Length; ++i)
        {
            Vector4D newvector = source[i];

            for (int r = 0; r < i; r++)
            {
                newvector -= Dot(destination[r], newvector) * destination[r];
            }

            newvector.Normalize();
            destination[i] = newvector;
        }
    }
    public static Vector4D Transform(Vector4D vector, Quaternion rotation)
    {
       return Vector4.Transform(vector, rotation);
    }

    public static void Transform(ReadOnlySpan<Vector4D> source, Quaternion rotation, Span<Vector4D> destination)
    {
        if (destination.Length < source.Length)
            throw new ArgumentOutOfRangeException(nameof(destination), "The destination array must be of same length or larger length than the source array.");
        for (int i = 0; i < source.Length; i++)
        {
            destination[i] = Transform(source[i], rotation);
        }
    }

    public static void Transform(ReadOnlySpan<Vector4D> source, Matrix transform, Span<Vector4D> destination)
    {
        if (destination.Length < source.Length)
            throw new ArgumentOutOfRangeException(nameof(destination), "The destination array must be of same length or larger length than the source array.");

        for (int i = 0; i < source.Length; i++)
        {
           destination[i] = Transform(source[i], transform);
        }
    }


    public static Vector4D Transform(Vector4D vector, Matrix transform)
    {
        return Vector4.Transform(vector, transform);
    }
    public static implicit operator Vector4(Vector4D value)
    {
        return new Vector4
        {
            X = value.X,
            Y = value.Y,
            Z = value.Z,
            W = value.W
        };
    }
    public static implicit operator Vector4D(Vector4 value)
    {
        return new Vector4D
        {
            X = value.X,
            Y = value.Y,
            Z = value.Z,
            W = value.W
        };
    }
}
