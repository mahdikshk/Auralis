using System;
using System.Buffers;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Text;

namespace Auralis.Core.Mathematics;

/// <summary>
/// Represents a two-dimensional vector using single-precision floating-point values.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 8)]
public struct Vector2D
{
    /// <summary>
    /// The size of the <see cref="SizeInBytes"/> in bytes
    /// </summary>
    /// <summary>
    /// The size of the <see cref="T:Auralis.Core.Mathematics.Vector2D"/> type in bytes.
    /// </summary>
    public static readonly int SizeInBytes = Unsafe.SizeOf<Vector2D>();
    /// <summary>
    /// An instance of <see cref="Vector2D"/> with all the values set to 0
    /// </summary>
    /// <summary>
    /// A static instance of the <see cref="T:Auralis.Core.Mathematics.Vector2D"/> structure with all components set to zero.
    /// </summary>
    public static readonly Vector2D Zero = new();
    /// <summary>
    /// An instance of <see cref="Vector2D"/> with the value of <see cref="X"/> set to 1
    /// </summary>
    /// <summary>
    /// A static instance of the <see cref="T:Auralis.Core.Mathematics.Vector2D"/> structure representing the unit vector pointing along the X-axis (1, 0).
    /// </summary>
    public static readonly Vector2D UnitX = new(1.0f, 0.0f);
    /// <summary>
    /// An instance of <see cref="Vector2D"/> with the value of <see cref="X"/> set to 1
    /// </summary>
    /// <summary>
    /// A static instance of the <see cref="T:Auralis.Core.Mathematics.Vector2D"/> structure representing the unit vector pointing along the Y-axis (0, 1).
    /// </summary>
    public static readonly Vector2D UnitY = new(0.0f, 1.0f);
    /// <summary>
    /// An instance of <see cref="Vector2D"/> with all the values set to 1
    /// </summary>
    /// <summary>
    /// A static instance of the <see cref="T:Auralis.Core.Mathematics.Vector2D"/> structure with all components set to one.
    /// </summary>
    public static readonly Vector2D One = new(1.0f, 1.0f);

    /// <summary>
    /// The X component of the vector
    /// </summary>
    /// <summary>
    /// The X component of the vector.
    /// </summary>
    public float X;
    /// <summary>
    /// The Y component of the vector
    /// </summary>
    /// <summary>
    /// The Y component of the vector.
    /// </summary>
    public float Y;
    /// <summary>
    /// Initializes an instance of <see cref="Vector2D"/> struct with all values set to default
    /// </summary>
    /// <summary>
    /// Initializes a new instance of the <see cref="T:Auralis.Core.Mathematics.Vector2D"/> structure.
    /// </summary>
    public Vector2D() { }
    /// <summary>
    /// Initializes an instance of <see cref="Vector2D"/> struct with all values set to <paramref name="value"/>
    /// </summary>
    /// <param name="value">The value that sets the <see cref="X"/> and <see cref="Y"/> to this value</param>
    /// <summary>
    /// Initializes a new instance of the <see cref="T:Auralis.Core.Mathematics.Vector2D"/> structure whose components are all set to the same value.
    /// </summary>
    public Vector2D(float value)
    {
        X = value;
        Y = value;
    }
    /// <summary>
    /// Initializes an instance of <see cref="Vector2D"/> struct with the given values
    /// </summary>
    /// <param name="x">The value of the X component</param>
    /// <param name="y">Th value of the Y component</param>
    /// <summary>
    /// Initializes a new instance of the <see cref="T:Auralis.Core.Mathematics.Vector2D"/> structure with the specified components.
    /// </summary>
    public Vector2D(float x, float y)
    {
        X = x;
        Y = y;
    }
    /// <summary>
    /// Gets or sets the component at the specified index.
    /// </summary>
    /// <value>The value of the X or Y component, depending on the index.</value>
    /// <param name="index">The index of the component to access. Use 0 for the X component and 1 for the Y component.</param>
    /// <returns>The value of the component at the specified index.</returns>
    /// <exception cref="System.ArgumentOutOfRangeException">Thrown when the <paramref name="index"/> is out of the range [0, 1].</exception>
    /// <summary>
    /// Gets or sets the component at the specified index. Indices for a two-dimensional vector run from 0 to 1, mapping to X and Y respectively.
    /// </summary>
    public float this[int index]
    {
        get
        {
            switch (index)
            {
                case 0: return X;
                case 1: return Y;
            }

            throw new ArgumentOutOfRangeException(nameof(index), "Indices for Vector2 run from 0 to 1, inclusive.");
        }

        set
        {
            switch (index)
            {
                case 0: X = value; break;
                case 1: Y = value; break;
                default: throw new ArgumentOutOfRangeException(nameof(index), "Indices for Vector2 run from 0 to 1, inclusive.");
            }
        }
    }
    /// <summary>
    /// Initializes a new instance of the <see cref="Vector2D"/> struct.
    /// </summary>
    /// <param name="values">The values to assign to the X and Y components of the vector. This must be an array with two elements.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="values"/> contains more or less than two elements.</exception>
    /// <summary>
    /// Initializes a new instance of the <see cref="T:Auralis.Core.Mathematics.Vector2D"/> structure from a read-only span of two values.
    /// </summary>
    public Vector2D(ReadOnlySpan<float> values)
    {
        if (values.Length != 2)
            throw new ArgumentOutOfRangeException(nameof(values), "There must be two and only two input values for Vector2.");

        X = values[0];
        Y = values[1];
    }
    /// <summary>
    /// Calculates the length of the vector
    /// </summary>
    /// <returns>returns the length of the vector</returns>
    /// <summary>
    /// Returns the Euclidean length of this vector.
    /// </summary>
    public readonly float Length()
    {
        return MathF.Sqrt((X * X) + (Y * Y));
    }
    /// <summary>
    /// Calculates the squared length of the vector.
    /// </summary>
    /// <returns>The squared length of the vector.</returns>
    /// <remarks>
    /// This method may be preferred to <see cref="Vector2D.Length"/> when only a relative length is needed
    /// and speed is of the essence.
    /// </remarks>
    /// <summary>
    /// Returns the squared Euclidean length of this vector.
    /// </summary>
    public float LengthSquared()
    {
        return (X * X) + (Y * Y);
    }
    /// <summary>
    /// Converts the vector into a unit vector.
    /// </summary>
    /// <summary>
    /// Normalizes this vector in place so that it has unit length.
    /// </summary>
    public void Normalize()
    {
        float length = Length();
        if (length > MathUtilities.ZeroTolerance)
        {
            float inv = 1.0f / length;
            X *= inv;
            Y *= inv;
        }
    }
    /// <summary>
    /// Converts the vector into an array
    /// </summary>
    /// <returns>An array with the 0 index being <see cref="X"/> and the 1 index being <see cref="Y"/></returns>
    /// <summary>
    /// Returns the vector components (X, Y) as a new array.
    /// </summary>
    public float[] ToArray()
    {
        return [X, Y];
    }

    /// <summary>
    /// Adds two vectors together.
    /// </summary>
    public static Vector2D operator +(Vector2D v1, Vector2D v2)
    {
        Vector2D result = new();
        result.X = v1.X + v2.X;
        result.Y = v1.Y + v2.Y;
        return result;
    }
    /// <summary>
    /// Subtracts the second vector from the first.
    /// </summary>
    public static Vector2D operator -(Vector2D v1, Vector2D v2)
    {
        Vector2D result = new();
        result.X = v1.X - v2.X;
        result.Y = v1.Y - v2.Y;
        return result;
    }
    /// <summary>
    /// Divides the components of the first vector by the components of the second vector.
    /// </summary>
    public static Vector2D operator /(Vector2D v1, Vector2D v2)
    {
        Vector2D result = new();
        result.X = v1.X / v2.X;
        result.Y = v1.Y / v2.Y;
        return result;
    }
    /// <summary>
    /// Divides a vector by the specified scalar value.
    /// </summary>
    public static Vector2D operator /(Vector2D vector, float scale)
    {
        Vector2D result = new();
        result.X = vector.X / scale;
        result.Y = vector.Y / scale;
        return result;
    }
    /// <summary>
    /// Multiplies the components of two vectors together (component-wise multiplication).
    /// </summary>
    public static Vector2D operator *(Vector2D v1, Vector2D v2)
    {
        Vector2D result = new();
        result.X = v1.X * v2.X;
        result.Y = v1.Y * v2.Y;
        return result;
    }
    /// <summary>
    /// Scales a vector by the specified scalar value.
    /// </summary>
    public static Vector2D operator *(float scale, Vector2D vector)
    {
        return vector * scale;
    }
    /// <summary>
    /// Scales a vector by the specified scalar value.
    /// </summary>
    public static Vector2D operator *(Vector2D vector, float scale)
    {
        Vector2D result = new();
        result.X = vector.X * scale;
        result.Y = vector.Y * scale;
        return result;
    }
    /// <summary>
    /// Increments both components of the specified vector by one.
    /// </summary>
    public static Vector2D operator ++(Vector2D v1)
    {
        Vector2D result = new();
        result.X++;
        result.Y++;
        return result;
    }

    /// <summary>
    /// Defines an implicit conversion of a <see cref="T:System.Numerics.Vector2"/> into a <see cref="T:Auralis.Core.Mathematics.Vector2D"/>.
    /// </summary>
    public static implicit operator Vector2D(System.Numerics.Vector2 vector)
    {
        return new Vector2D(vector.X, vector.Y);
    }

    /// <summary>
    /// Defines an implicit conversion of a <see cref="T:Auralis.Core.Mathematics.Vector2D"/> into a <see cref="T:System.Numerics.Vector2"/>.
    /// </summary>
    public static implicit operator System.Numerics.Vector2(Vector2D vector)
    {
        return new Vector2(vector.X, vector.Y);
    }

    /// <summary>
    /// Defines an implicit conversion of a <see cref="T:Auralis.Core.Mathematics.Vector2D"/> into a <see cref="T:Auralis.Core.Mathematics.Vector3D"/>, setting Z to zero.
    /// </summary>
    public static implicit operator Vector3D(in Vector2D vector)
    {
        return new Vector3D(vector.X, vector.Y, 0);
    }

    /// <summary>
    /// Adds to vectors and returns the sum vector
    /// </summary>
    /// <param name="left">The first vector to add</param>
    /// <param name="right">The second vector to add</param>
    /// <returns>The sum of the vectors</returns>
    /// <summary>
    /// Adds two vectors together.
    /// </summary>
    public static Vector2D Add(Vector2D left, Vector2D right)
    {
        return Vector2.Add(left, right);
    }
    /// <summary>
    /// Subtracts two vectors
    /// </summary>
    /// <param name="left">The first vector to subtract</param>
    /// <param name="right">The second vector to subtract</param>
    /// <returns>The result of the subtraction</returns>
    /// <summary>
    /// Subtracts the second vector from the first.
    /// </summary>
    public static Vector2D Subtract(Vector2D left, Vector2D right)
    {
        return Vector2.Subtract(left, right);
    }
    /// <summary>
    /// Scales a vector by the given value
    /// </summary>
    /// <param name="value">The vector to get scaled</param>
    /// <param name="scale">The scale</param>
    /// <returns>The scaled vector</returns>
    /// <summary>
    /// Scales a vector by the specified scalar value.
    /// </summary>
    public static Vector2D Multiply(Vector2D value, float scale)
    {
        return Vector2.Multiply(scale, value);
    }
    /// <summary>
    /// Modulate 2 vectors with another by performing component wise multiplication
    /// </summary>
    /// <param name="left">The first vector to modulate</param>
    /// <param name="right">The second vector to modulate</param>
    /// <returns>The modulated vector</returns>
    /// <summary>
    /// Multiplies the components of two vectors together (component-wise multiplication).
    /// </summary>
    public static Vector2D Modulate(ref readonly Vector2D left, ref readonly Vector2D right)
    {
        return (Vector2)left * (Vector2)right;
    }
    /// <summary>
    /// Scales a vector by the given value
    /// </summary>
    /// <param name="value">The vector to scale</param>
    /// <param name="scale">The scale value</param>
    /// <returns>The scale vector</returns>
    /// <summary>
    /// Divides a vector by the specified scalar value.
    /// </summary>
    public static Vector2D Divide(Vector2D value, float scale)
    {
        return Vector2.Divide(value, scale);
    }
    /// <summary>
    /// Reverses the direction of the given vector
    /// </summary>
    /// <param name="value">The vector to negate</param>
    /// <returns>A negated vector</returns>
    /// <summary>
    /// Negates the specified vector.
    /// </summary>
    public static Vector2D Negate(Vector2D value)
    {
        return Vector2.Negate(value);
    }
    /// <summary>
    /// 
    /// </summary>
    /// <param name="value1"></param>
    /// <param name="value2"></param>
    /// <param name="value3"></param>
    /// <param name="amount1"></param>
    /// <param name="amount2"></param>
    /// <param name="result"></param>
    /// <summary>
    /// Returns a point on a bezier spline defined by the first four points.
    /// </summary>
    public static Vector2D Barycentric(Vector2D value1, Vector2D value2, Vector2D value3, float amount1, float amount2)
    {
        return new Vector2D(
            (value1.X + (amount1 * (value2.X - value1.X))) + (amount2 * (value3.X - value1.X)),
            (value1.Y + (amount1 * (value2.Y - value1.Y))) + (amount2 * (value3.Y - value1.Y)));
    }
    /// <summary>
    /// Restricts a <see cref="Vector2D"/> to be within a specified range
    /// </summary>
    /// <param name="value">The vector to clamp</param>
    /// <param name="min">The minimum value</param>
    /// <param name="max">The maximum value</param>
    /// <param name="result">The clamped value</param>
    /// <summary>
    /// Restricts a vector between a minimum and a maximum value, component-wise.
    /// </summary>
    public static Vector2D Clamp(Vector2D value, Vector2D min, Vector2D max)
    {
        return Vector2.Clamp(value, min, max);
    }

    /// <summary>
    /// Calculates the distance between the two vectors
    /// </summary>
    /// <param name="value1">The first vector</param>
    /// <param name="value2">The second vector</param>
    /// <returns>The distance of the two vectors</returns>
    /// <summary>
    /// Returns the Euclidean distance between two vectors.
    /// </summary>
    public static float Distance(Vector2D value1, Vector2D value2)
    {
        return Vector2.Distance(value1, value2);
    }
    /// <summary>
    /// Calculates the squared distance between two vectors
    /// </summary>
    /// <param name="value1">The first vector</param>
    /// <param name="value2">The second vector</param>
    /// <returns>The squared distance of two vectors</returns>
    /// <summary>
    /// Returns the squared Euclidean distance between two vectors.
    /// </summary>
    public static float DistanceSquared(Vector2D value1, Vector2D value2)
    {
        return Vector2.DistanceSquared(value1, value2);
    }
    /// <summary>
    /// Calculates the dot product of the two vectors
    /// </summary>
    /// <param name="left">The first vector</param>
    /// <param name="right">The send vector</param>
    /// <returns>The dot product of two vectors</returns>
    /// <summary>
    /// Returns the dot product of two vectors.
    /// </summary>
    public static float Dot(Vector2D left, Vector2D right)
    {
        return Vector2.Dot(left, right);
    }
    /// <summary>
    /// Converts the given vector into an unit vector
    /// </summary>
    /// <param name="value">The vector to normalize</param>
    /// <returns>The normalized vector</returns>
    /// <summary>
    /// Returns a normalized version of the specified vector.
    /// </summary>
    public static Vector2D Normalize(ref readonly Vector2D value)
    {
        value.Normalize();
        return value;
    }
    /// <summary>
    /// Performs a linear interpolation between two vectors.
    /// </summary>
    /// <param name="start">Start vector</param>
    /// <param name="end">End vector</param>
    /// <param name="amount">Value between 0 and 1 indicating the weight of <paramref name="end"/></param>
    /// <returns>The linear interpolation result</returns>
    /// <summary>
    /// Performs a linear interpolation between two vectors.
    /// </summary>
    public static Vector2D Lerp(ref readonly Vector2D start, ref readonly Vector2D end, float amount)
    {
        return Vector2.Lerp(start, end, amount);
    }
    /// <summary>
    /// Performs a cubic interpolation between the two vectors
    /// </summary>
    /// <param name="start">The start vector</param>
    /// <param name="end">The end vector</param>
    /// <param name="amount">Value between 0 and 1 indicating the weight of <paramref name="end"/></param>
    /// <param name="result">The cubic interpolation operation result</param>
    /// <summary>
    /// Interpolates between two vectors using a cubic equation.
    /// </summary>
    public static Vector2D SmoothStep(ref readonly Vector2D start, ref readonly Vector2D end, float amount)
    {
        amount = (amount > 1.0f) ? 1.0f : ((amount < 0.0f) ? 0.0f : amount);
        amount = (amount * amount) * (3.0f - (2.0f * amount));
        Vector2D result;
        result.X = start.X + ((end.X - start.X) * amount);
        result.Y = start.Y + ((end.Y - start.Y) * amount);
        return result;
    }
    /// <summary>
    /// Performs a Hermite spline interpolation
    /// </summary>
    /// <param name="value1">First source's position vector</param>
    /// <param name="tangent1">First source tangent vector</param>
    /// <param name="value2">Second source's position vector vector</param>
    /// <param name="tangent2">Second source tangent vector</param>
    /// <param name="amount">Weight factor</param>
    /// <param name="result">The hermite spline interpolation operation</param>
    /// <summary>
    /// Creates Hermite interpolation between two positions based on the given tangents.
    /// </summary>
    public static Vector2D Hermite(ref readonly Vector2D value1,
        ref readonly Vector2D tangent1,
        ref readonly Vector2D value2,
        ref readonly Vector2D tangent2,
        float amount)
    {
        float squared = amount * amount;
        float cubed = amount * squared;
        float part1 = ((2.0f * cubed) - (3.0f * squared)) + 1.0f;
        float part2 = (-2.0f * cubed) + (3.0f * squared);
        float part3 = (cubed - (2.0f * squared)) + amount;
        float part4 = cubed - squared;
        Vector2D result;
        result.X = (((value1.X * part1) + (value2.X * part2)) + (tangent1.X * part3)) + (tangent2.X * part4);
        result.Y = (((value1.Y * part1) + (value2.Y * part2)) + (tangent1.Y * part3)) + (tangent2.Y * part4);
        return result;
    }
    /// <summary>
    /// Performs a Catmull-Rom interpolation
    /// </summary>
    /// <param name="value1">The first position in the interpolation</param>
    /// <param name="value2">The second position in the interpolation</param>
    /// <param name="value3">The third position in the interpolation</param>
    /// <param name="value4">The forth position in the interpolation</param>
    /// <param name="amount">The weighting factor</param>
    /// <param name="result">The result of the interpolation</param>
    /// <summary>
    /// Performs a Catmull-Rom interpolation.
    /// </summary>
    public static Vector2D CatmullRom(ref readonly Vector2D value1,
        ref readonly Vector2D value2,
        ref readonly Vector2D value3,
        ref readonly Vector2D value4, float amount)
    {
        float squared = amount * amount;
        float cubed = amount * squared;
        Vector2D result;
        result.X = 0.5f * ((((2.0f * value2.X) + ((-value1.X + value3.X) * amount)) +
        (((((2.0f * value1.X) - (5.0f * value2.X)) + (4.0f * value3.X)) - value4.X) * squared)) +
        ((((-value1.X + (3.0f * value2.X)) - (3.0f * value3.X)) + value4.X) * cubed));

        result.Y = 0.5f * ((((2.0f * value2.Y) + ((-value1.Y + value3.Y) * amount)) +
            (((((2.0f * value1.Y) - (5.0f * value2.Y)) + (4.0f * value3.Y)) - value4.Y) * squared)) +
            ((((-value1.Y + (3.0f * value2.Y)) - (3.0f * value3.Y)) + value4.Y) * cubed));
        return result;
    }
    /// <summary>
    /// Returns a vector with the maximum component for each dimension from the pair of vectors
    /// </summary>
    /// <param name="left">The first vector</param>
    /// <param name="right">The second vector</param>
    /// <summary>
    /// Returns a vector whose components are the largest of the corresponding components of the given vectors.
    /// </summary>
    public static Vector2D Max(Vector2D left, Vector2D right)
    {
        return Vector2.Max(left, right);
    }
    /// <summary>
    /// Returns a vector with the maximum component for each dimension from the bunch of vectors
    /// </summary>
    /// <param name="vectors">The vectors</param>
    /// <returns>The maximum components present in the vectors</returns>
    /// <summary>
    /// Returns the component-wise maximum of all vectors in the given read-only span.
    /// </summary>
    public static Vector2D Max(ReadOnlySpan<Vector2D> vectors)
    {
        float maxX = 0f;
        float maxY = 0f;
        foreach (var vector in vectors)
        {
            if (vector.X > maxX)
                maxX = vector.X;
            if (vector.Y > maxY)
                maxY = vector.Y;
        }
        return new Vector2D(maxX, maxY);
    }

    /// <summary>
    /// Returns a vector with the minimum component for each dimension from the pair of vectors
    /// </summary>
    /// <param name="left">The first vector</param>
    /// <param name="right">The second vector</param>
    /// <returns>The min vector</returns>
    /// <summary>
    /// Returns a vector whose components are the smallest of the corresponding components of the given vectors.
    /// </summary>
    public static Vector2D Min(Vector2D left, Vector2D right)
    {
        return Vector2.Min(left, right);
    }

    /// <summary>
    /// Returns the reflection of a vector off a surface that has the specified normal
    /// </summary>
    /// <param name="vector">The source vector</param>
    /// <param name="normal">The normal surface</param>
    /// <returns>The reflected vector</returns>
    /// <summary>
    /// Returns the reflection of a vector off a surface specified by its normal.
    /// </summary>
    public static Vector2D Reflect(ref readonly Vector2D vector, ref readonly Vector2D normal)
    {
        return Vector2.Reflect(vector, normal);
    }
    /// <summary>
    /// Orthogonalizes a list of vectors
    /// </summary>
    /// <param name="destination">The list of orthogonalized vectors</param>
    /// <param name="source">The list of vectors to orthogonalize</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="destination"/> is shorter in length than <paramref name="source"/></exception>
    /// <summary>
    /// Orthogonalizes the source vectors using the Gram-Schmidt process, writing the results to the destination span.
    /// </summary>
    public static void Orthogonalize(Span<Vector2D> destination, params ReadOnlySpan<Vector2D> source)
    {
        //Uses the modified Gram-Schmidt process.
        //q1 = m1
        //q2 = m2 - ((q1 ⋅ m2) / (q1 ⋅ q1)) * q1
        //q3 = m3 - ((q1 ⋅ m3) / (q1 ⋅ q1)) * q1 - ((q2 ⋅ m3) / (q2 ⋅ q2)) * q2
        //q4 = m4 - ((q1 ⋅ m4) / (q1 ⋅ q1)) * q1 - ((q2 ⋅ m4) / (q2 ⋅ q2)) * q2 - ((q3 ⋅ m4) / (q3 ⋅ q3)) * q3
        //q5 = ...
        if (destination.Length < source.Length)
            throw new ArgumentOutOfRangeException(nameof(destination), "The destination array must be of same length or larger length than the source array.");

        for (int i = 0; i < source.Length; ++i)
        {
            Vector2D newvector = source[i];

            for (int r = 0; r < i; ++r)
            {
                newvector -= (Dot(destination[r], newvector) / Dot(destination[r], destination[r])) * destination[r];
            }

            destination[i] = newvector;
        }
    }
    /// <summary>
    /// Orthonormalizes a list of vectors.
    /// </summary>
    /// <param name="destination">The list of orthonormalized vectors.</param>
    /// <param name="source">The list of vectors to orthonormalize.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="destination"/> is shorter in length than <paramref name="source"/></exception>
    /// <summary>
    /// Orthonormalizes the source vectors using the Gram-Schmidt process, writing the results to the destination span.
    /// </summary>
    public static void Orthonormalize(Span<Vector2D> destination, params ReadOnlySpan<Vector2D> source)
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
            Vector2D newvector = source[i];

            for (int r = 0; r < i; ++r)
            {
                newvector -= Dot(destination[r], newvector) * destination[r];
            }

            newvector.Normalize();
            destination[i] = newvector;
        }
    }
    /// <summary>
    /// Transforms a 2D vector by the given <see cref="Auralis.Core.Mathematics.Quaternion"/> rotation.
    /// </summary>
    /// <param name="vector">The vector to rotate</param>
    /// <param name="rotation">The <see cref="Auralis.Core.Mathematics.Quaternion"/> rotation to apply</param>
    /// <param name="result">When the method completes, contains the transformed <see cref="Auralis.Core.Mathematics.Vector4D"/>.</param>
    /// <summary>
    /// Transforms a vector by the specified rotation quaternion.
    /// </summary>
    public static Vector2D Transform(Vector2D vector, Quaternion rotation)
    {
        float x = rotation.X + rotation.X;
        float y = rotation.Y + rotation.Y;
        float z = rotation.Z + rotation.Z;
        float wz = rotation.W * z;
        float xx = rotation.X * x;
        float xy = rotation.X * y;
        float yy = rotation.Y * y;
        float zz = rotation.Z * z;

        return new Vector2D((vector.X * (1.0f - yy - zz)) + (vector.Y * (xy - wz)), (vector.X * (xy + wz)) + (vector.Y * (1.0f - xx - zz)));
    }

    /// <summary>
    /// Transforms a set of vectors by the given <see cref="Auralis.Core.Mathematics.Quaternion"/> rotation
    /// </summary>
    /// <param name="source">Multiple vectors to transform</param>
    /// <param name="rotation">The <see cref="Auralis.Core.Mathematics.Quaternion"/> to apply</param>
    /// <param name="destination">The list of rotated vectors</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="destination"/> is shorter in length than <paramref name="source"/>.</exception>
    /// <summary>
    /// Transforms a span of vectors by the specified rotation quaternion into a destination span.
    /// </summary>
    public static void Transform(ReadOnlySpan<Vector2D> source, Quaternion rotation, Span<Vector2D> destination)
    {
        if (destination.Length < source.Length)
            throw new ArgumentOutOfRangeException(nameof(destination), "The destination array must be of same length or larger length than the source array.");

        float x = rotation.X + rotation.X;
        float y = rotation.Y + rotation.Y;
        float z = rotation.Z + rotation.Z;
        float wz = rotation.W * z;
        float xx = rotation.X * x;
        float xy = rotation.X * y;
        float yy = rotation.Y * y;
        float zz = rotation.Z * z;

        float num1 = (1.0f - yy - zz);
        float num2 = (xy - wz);
        float num3 = (xy + wz);
        float num4 = (1.0f - xx - zz);

        for (int i = 0; i < source.Length; ++i)
        {
            destination[i] = new Vector2D(
                (source[i].X * num1) + (source[i].Y * num2),
                (source[i].X * num3) + (source[i].Y * num4));
        }
    }
    /// <summary>
    /// Transforms a 2D vector by the given <see cref="Auralis.Core.Mathematics.Matrix"/>
    /// </summary>
    /// <param name="vector">The source vector</param>
    /// <param name="transform">The transformation. <see cref="Auralis.Core.Mathematics.Matrix"/></param>
    /// <param name="result">The transformed <see cref="Auralis.Core.Mathematics.Vector4D"/></param>
    /// <summary>
    /// Transforms a vector by the specified matrix, producing a four-dimensional vector.
    /// </summary>
    public static Vector4D Transform(ref readonly Vector2D vector, ref readonly Matrix transform)
    {
        return new Vector4D(
            (vector.X * transform.Row1.X) + (vector.Y * transform.Row2.X) + transform.Row4.X,
            (vector.X * transform.Row1.Y) + (vector.Y * transform.Row2.Y) + transform.Row4.Y,
            (vector.X * transform.Row1.Z) + (vector.Y * transform.Row2.Z) + transform.Row4.Z,
            (vector.X * transform.Row1.W) + (vector.Y * transform.Row2.W) + transform.Row4.W);
    }
    /// <summary>
    /// Transforms a 2D vector by the given <see cref="Auralis.Core.Mathematics.Matrix"/>
    /// </summary>
    /// <param name="source">The vectors to be transformed</param>
    /// <param name="transform">The transformation</param>
    /// <param name="destination">The transformed vectors</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if the <paramref name="destination"/> length is smaller than <paramref name="source"/></exception>
    /// <summary>
    /// Transforms a span of vectors by the specified matrix into a destination span of four-dimensional vectors.
    /// </summary>
    public static void Transform(ReadOnlySpan<Vector2D> source, ref readonly Matrix transform, Span<Vector4D> destination)
    {
        if (destination.Length < source.Length)
            throw new ArgumentOutOfRangeException(nameof(destination), "The destination array must be of same length or larger length than the source array.");

        for (int i = 0; i < source.Length; ++i)
        {
            destination[i] = Transform(in source[i], in transform);
        }
    }
    /// <summary>
    /// Transforms a texture coordinate by the specified matrix.
    /// </summary>
    public static Vector2D TransformCoordinate(ref readonly Vector2D coordinate, ref readonly Matrix transform)
    {
        return Vector2.Transform(coordinate, transform);
    }
    /// <summary>
    /// Transforms a span of texture coordinates by the specified matrix into a destination span.
    /// </summary>
    public static void TransformCoordinate(ReadOnlySpan<Vector2D> source, ref readonly Matrix transform, Span<Vector2D> destination)
    {

        if (destination.Length < source.Length)
            throw new ArgumentOutOfRangeException(nameof(destination), "The destination array must be of same length or larger length than the source array.");

        for (int i = 0; i < source.Length; ++i)
        {
            destination[i] = TransformCoordinate(in source[i], in transform);
        }
    }
    /// <summary>
    /// Transforms a normal vector by the rotational part of the specified matrix.
    /// </summary>
    public static Vector2D TransformNormal(ref readonly Vector2D normal, ref readonly Matrix transform)
    {
        return Vector2.TransformNormal(normal, transform);
    }
    /// <summary>
    /// Transforms a span of normal vectors by the specified matrix into a destination span.
    /// </summary>
    public static void TransformNormal(ReadOnlySpan<Vector2D> source, ref readonly Matrix transform, Span<Vector2D> destination)
    {
        if (destination.Length < source.Length)
            throw new ArgumentOutOfRangeException(nameof(destination), "The destination array must be of same length or larger length than the source array.");

        for (int i = 0; i < source.Length; ++i)
        {
            destination[i] = TransformNormal(in source[i], in transform);
        }
    }

}

