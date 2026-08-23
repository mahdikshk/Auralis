using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics.X86;
using System.Text;

namespace Auralis.Core.Mathematics;

public struct Quaternion : IEquatable<Quaternion>
{
    public static readonly int SizeInBytes = Unsafe.SizeOf<Quaternion>();

    public static readonly Quaternion Zero = new();

    public static readonly Quaternion One = new(1.0f, 1.0f, 1.0f, 1.0f);

    public static readonly Quaternion Identity = new(0.0f, 0.0f, 0.0f, 1.0f);



    public float X;

    public float Y;

    public float Z;

    public float W;
    public Quaternion(float value)
    {
        X = value;
        Y = value;
        Z = value;
        W = value;
    }

    public Quaternion(Vector3D value, float w)
    {
        X = value.X;
        Y = value.Y;
        Z = value.Z;
        W = w;
    }

    public Quaternion(Vector2D value, float z, float w)
    {
        X = value.X;
        Y = value.Y;
        Z = z;
        W = w;
    }

    public Quaternion(float x, float y, float z, float w)
    {
        X = x;
        Y = y;
        Z = z;
        W = w;
    }
    public Quaternion(ReadOnlySpan<float> values)
    {
        if (values.Length != 4)
            throw new ArgumentOutOfRangeException(nameof(values), "There must be four and only four input values for Quaternion.");

        X = values[0];
        Y = values[1];
        Z = values[2];
        W = values[3];
    }

    public static Quaternion operator *(in Quaternion left, in Quaternion right)
    {
        return (System.Numerics.Quaternion)left * (System.Numerics.Quaternion)right;
    }

    public bool IsIdentity
    {
        get { return this.Equals(Identity); }
    }

    public bool IsNormalized
    {
        get { return MathF.Abs((X * X) + (Y * Y) + (Z * Z) + (W * W) - 1f) < MathUtilities.ZeroTolerance; }
    }

    public float Angle
    {
        get
        {
            float length = (X * X) + (Y * Y) + (Z * Z);
            if (length < MathUtilities.ZeroTolerance)
                return 0.0f;

            return 2.0f * MathF.Acos(W);
        }
    }

    public Vector3D Axis
    {
        get
        {
            float length = (X * X) + (Y * Y) + (Z * Z);
            if (length < MathUtilities.ZeroTolerance)
                return Vector3D.UnitX;

            float inv = 1.0f / length;
            return new Vector3D(X * inv, Y * inv, Z * inv);
        }
    }

    public Vector3D YawPitchRoll
    {
        get
        {
            Vector3D yawPitchRoll;
            RotationYawPitchRoll(ref this, out yawPitchRoll.X, out yawPitchRoll.Y, out yawPitchRoll.Z);
            return yawPitchRoll;
        }
    }

    public float this[int index]
    {
        get
        {
            switch (index)
            {
                case 0: return X;
                case 1: return Y;
                case 2: return Z;
                case 3: return W;
            }

            throw new ArgumentOutOfRangeException(nameof(index), "Indices for Quaternion run from 0 to 3, inclusive.");
        }

        set
        {
            switch (index)
            {
                case 0: X = value; break;
                case 1: Y = value; break;
                case 2: Z = value; break;
                case 3: W = value; break;
                default: throw new ArgumentOutOfRangeException(nameof(index), "Indices for Quaternion run from 0 to 3, inclusive.");
            }
        }
    }

    public void Conjugate()
    {
        X = -X;
        Y = -Y;
        Z = -Z;
    }
    public static Quaternion Conjugate(Quaternion value)
    {
        return System.Numerics.Quaternion.Conjugate(value);
    }
    public void Invert()
    {
        float lengthSq = LengthSquared();
        if (lengthSq > MathUtilities.ZeroTolerance)
        {
            lengthSq = 1.0f / lengthSq;

            X = -X * lengthSq;
            Y = -Y * lengthSq;
            Z = -Z * lengthSq;
            W = W * lengthSq;
        }
    }

    public readonly float Length()
    {
        return MathF.Sqrt((X * X) + (Y * Y) + (Z * Z) + (W * W));
    }

    public readonly float LengthSquared()
    {
        return (X * X) + (Y * Y) + (Z * Z) + (W * W);
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

    public float[] ToArray()
    {
        return [X, Y, Z, W];
    }

    public static Quaternion Add(Quaternion left, Quaternion right)
    {
        Quaternion result;
        result.X = left.X + right.X;
        result.Y = left.Y + right.Y;
        result.Z = left.Z + right.Z;
        result.W = left.W + right.W;
        return result;
    }

    public static Quaternion Subtract(Quaternion left, Quaternion right)
    {
        Quaternion result;
        result.X = left.X - right.X;
        result.Y = left.Y - right.Y;
        result.Z = left.Z - right.Z;
        result.W = left.W - right.W;
        return result;
    }

    public static Quaternion Multiply(Quaternion value, float scale)
    {
        Quaternion result;
        result.X = value.X * scale;
        result.Y = value.Y * scale;
        result.Z = value.Z * scale;
        result.W = value.W * scale;
        return result;
    }

    public static Quaternion Multiply(Quaternion left, Quaternion right)
    {
        float lx = left.X;
        float ly = left.Y;
        float lz = left.Z;
        float lw = left.W;
        float rx = right.X;
        float ry = right.Y;
        float rz = right.Z;
        float rw = right.W;
        Quaternion result;
        result.X = (rx * lw + lx * rw + ry * lz) - (rz * ly);
        result.Y = (ry * lw + ly * rw + rz * lx) - (rx * lz);
        result.Z = (rz * lw + lz * rw + rx * ly) - (ry * lx);
        result.W = (rw * lw) - (rx * lx + ry * ly + rz * lz);
        return result;
    }

    public static Quaternion Negate(Quaternion value)
    {
        return System.Numerics.Quaternion.Negate(value);
    }

    public static Quaternion Barycentric(Quaternion value1, Quaternion value2, Quaternion value3, float amount1, float amount2)
    {
        Quaternion result;
        System.Numerics.Quaternion start = System.Numerics.Quaternion.Slerp(value1, value2, amount1 + amount2);
        System.Numerics.Quaternion end = System.Numerics.Quaternion.Slerp(value1, value3, amount1 + amount2);
        result = System.Numerics.Quaternion.Slerp(start, end, amount2 / (amount1 + amount2));
        return result;
    }

    public static float Dot(Quaternion left, Quaternion right)
    {
        float result = System.Numerics.Quaternion.Dot(left, right);
        return result;
    }

    public static float AngleBetween(Quaternion a, Quaternion b)
    {
        return MathF.Acos(MathF.Min(MathF.Abs(Dot(a, b)), 1f)) * 2f;
    }

    public static Quaternion Exponential(Quaternion value)
    {
        Quaternion result;
        float angle = MathF.Sqrt((value.X * value.X) + (value.Y * value.Y) + (value.Z * value.Z));
        float sin = MathF.Sin(angle);

        if (MathF.Abs(sin) >= MathUtilities.ZeroTolerance)
        {
            float coeff = sin / angle;
            result.X = coeff * value.X;
            result.Y = coeff * value.Y;
            result.Z = coeff * value.Z;
        }
        else
        {
            result = value;
        }

        result.W = MathF.Cos(angle);
        return result;
    }

    public static Quaternion Invert(Quaternion value)
    {
        value.Invert();
        return value;
    }

    public static Quaternion Lerp(Quaternion start, Quaternion end, float amount)
    {
        return System.Numerics.Quaternion.Lerp(start, end, amount);
    }
    public static Quaternion LookRotation(Vector3D forward, Vector3D up)
    {
        var right = Vector3D.Normalize(Vector3D.Cross(up, forward));
        var orthoUp = Vector3D.Cross(forward, right);
        var m = new Matrix
        {
            Row1 = new Vector4D(right,0),
            Row2 = new Vector4D(orthoUp,0),
            Row3 = new Vector4D(forward,0),
        };
        return RotationMatrix(m);
    }
    public readonly void Rotate(ref Vector3D vector)
    {
        var pureQuaternion = new Quaternion(vector, 0);
        pureQuaternion = Conjugate(this) * pureQuaternion * this;

        vector.X = pureQuaternion.X;
        vector.Y = pureQuaternion.Y;
        vector.Z = pureQuaternion.Z;
    }
    public static Quaternion Logarithm(Quaternion value)
    {
        Quaternion result;
        if (MathF.Abs(value.W) < 1.0f)
        {
            float angle = MathF.Acos(value.W);
            float sin = MathF.Sin(angle);

            if (MathF.Abs(sin) >= MathUtilities.ZeroTolerance)
            {
                float coeff = angle / sin;
                result.X = value.X * coeff;
                result.Y = value.Y * coeff;
                result.Z = value.Z * coeff;
            }
            else
            {
                result = value;
            }
        }
        else
        {
            result = value;
        }

        result.W = 0.0f;
        return result;
    }
    public static Quaternion Normalize(Quaternion value)
    {
        return System.Numerics.Quaternion.Normalize(value);
    }
    public static implicit operator System.Numerics.Quaternion(Auralis.Core.Mathematics.Quaternion quaternion)
    {
        return new System.Numerics.Quaternion
        {
            X = quaternion.X,
            Y = quaternion.Y,
            Z = quaternion.Z,
            W = quaternion.W
        };
    }
    public static implicit operator Auralis.Core.Mathematics.Quaternion(System.Numerics.Quaternion quaternion)
    {
        return new Quaternion
        {
            X = quaternion.X,
            Y = quaternion.Y,
            Z = quaternion.Z,
            W = quaternion.W
        };
    }





    public static Quaternion RotationAxis(Vector3D axis, float angle)
    {
        var normalized = Vector3D.Normalize(axis);

        float half = angle * 0.5f;
        float sin = MathF.Sin(half);
        float cos = MathF.Cos(half);
        Quaternion result;
        result.X = normalized.X * sin;
        result.Y = normalized.Y * sin;
        result.Z = normalized.Z * sin;
        result.W = cos;
        return result;
    }


    public static Quaternion RotationMatrix(Matrix matrix)
    {
        return System.Numerics.Quaternion.CreateFromRotationMatrix(matrix);
    }

    public static Quaternion RotationX(float angle)
    {
        Quaternion result;
        float halfAngle = angle * 0.5f;
        result = new Quaternion(MathF.Sin(halfAngle), 0.0f, 0.0f, MathF.Cos(halfAngle));
        return result;
    }

    public static Quaternion RotationY(float angle)
    {
        Quaternion result;
        float halfAngle = angle * 0.5f;
        result = new Quaternion(0.0f, MathF.Sin(halfAngle), 0.0f, MathF.Cos(halfAngle));
        return result;
    }

    public static Quaternion RotationZ(float angle)
    {
        Quaternion result;
        float halfAngle = angle * 0.5f;
        result = new Quaternion(0.0f, 0.0f, MathF.Sin(halfAngle), MathF.Cos(halfAngle));
        return result;
    }

    public static Quaternion RotationYawPitchRoll(float yaw, float pitch, float roll)
    {
        return System.Numerics.Quaternion.CreateFromYawPitchRoll(yaw, pitch, roll);
    }

    public static void RotationYawPitchRoll(ref readonly Quaternion rotation, out float yaw, out float pitch, out float roll)
    {
        var xx = rotation.X * rotation.X;
        var yy = rotation.Y * rotation.Y;
        var zz = rotation.Z * rotation.Z;
        var xy = rotation.X * rotation.Y;
        var zw = rotation.Z * rotation.W;
        var zx = rotation.Z * rotation.X;
        var yw = rotation.Y * rotation.W;
        var yz = rotation.Y * rotation.Z;
        var xw = rotation.X * rotation.W;

        var M11 = 1.0f - (2.0f * (yy + zz));
        var M12 = 2.0f * (xy + zw);
        //var M13 = 2.0f * (zx - yw);
        var M21 = 2.0f * (xy - zw);
        var M22 = 1.0f - (2.0f * (zz + xx));
        //var M23 = 2.0f * (yz + xw);
        var M31 = 2.0f * (zx + yw);
        var M32 = 2.0f * (yz - xw);
        var M33 = 1.0f - (2.0f * (yy + xx));

        /*** Refer to Matrix.Decompose(out float yaw, out float pitch, out float roll) for code and license ***/
        if (MathUtilities.IsOne(Math.Abs(M32)))
        {
            if (M32 >= 0)
            {
                // Edge case where M32 == +1
                pitch = -MathUtilities.PiOverTwo;
                yaw = MathF.Atan2(-M21, M11);
                roll = 0;
            }
            else
            {
                // Edge case where M32 == -1
                pitch = MathUtilities.PiOverTwo;
                yaw = -MathF.Atan2(-M21, M11);
                roll = 0;
            }
        }
        else
        {
            // Common case
            pitch = MathF.Asin(-M32);
            yaw = MathF.Atan2(M31, M33);
            roll = MathF.Atan2(M12, M22);
        }
    }
    public static Quaternion BetweenDirections(Vector3D source, Vector3D target)
    {
        Quaternion result;
        var norms = MathF.Sqrt(source.LengthSquared() * target.LengthSquared());
        var real = norms + Vector3.Dot(source, target);
        if (real < MathUtilities.ZeroTolerance * norms)
        {
            // If source and target are exactly opposite, rotate 180 degrees around an arbitrary orthogonal axis.
            // Axis normalisation can happen later, when we normalise the quaternion.
            result = MathF.Abs(source.X) > MathF.Abs(source.Z)
                ? new Quaternion(-source.Y, source.X, 0.0f, 0.0f)
                : new Quaternion(0.0f, -source.Z, source.Y, 0.0f);
        }
        else
        {
            // Otherwise, build quaternion the standard way.
            var axis = Vector3.Cross(source, target);
            result = new Quaternion(axis, real);
        }
        result.Normalize();
        return result;
    }

    public static Quaternion Slerp(Quaternion start, Quaternion end, float amount)
    {
        return System.Numerics.Quaternion.Slerp(start, end, amount);
    }

    public static Quaternion RotateTowards(Quaternion current, Quaternion target, float angle)
    {
        var maxAngle = AngleBetween(current, target);
        return maxAngle == 0f ? target : Slerp(current, target, MathF.Min(1f, angle / maxAngle));
    }
    public static Quaternion Squad(Quaternion value1, Quaternion value2, Quaternion value3, Quaternion value4, float amount)
    {
        var start = Slerp(value1, value4, amount);
        var end = Slerp(value2, value3, amount);
        Quaternion result = Slerp(start, end, 2.0f * amount * (1.0f - amount));
        return result;
    }

    public static Quaternion[] SquadSetup(in Quaternion value1, in Quaternion value2, in Quaternion value3, in Quaternion value4)
    {
        var results = new Quaternion[3];
        SquadSetup(in value1, in value2, in value3, in value4, results);
        return results;
    }
    public static void SquadSetup(in Quaternion value1, in Quaternion value2, in Quaternion value3,
        in Quaternion value4, Span<Quaternion> destination)
    {
        if (destination.Length < 3)
        {
            throw new ArgumentException("The length of the span should be 3 or more", nameof(destination));
        }

        Quaternion q0 = (value1 + value2).LengthSquared() < (value1 - value2).LengthSquared() ? -value1 : value1;
        Quaternion q2 = (value2 + value3).LengthSquared() < (value2 - value3).LengthSquared() ? -value3 : value3;
        Quaternion q3 = (value3 + value4).LengthSquared() < (value3 - value4).LengthSquared() ? -value4 : value4;
        Quaternion q1 = value2;

        var q1Exp = Exponential(q1);
        var q2Exp = Exponential(q2);

        destination[0] = q1 * Exponential(-0.25f * (Logarithm(q1Exp * q2) + Logarithm(q1Exp * q0)));
        destination[1] = q2 * Exponential(-0.25f * (Logarithm(q2Exp * q3) + Logarithm(q2Exp * q1)));
        destination[2] = q2;
    }

    public static Quaternion operator +(Quaternion left, Quaternion right)
    {
        return Add(left, right);
    }

    public static Quaternion operator -(Quaternion left, Quaternion right)
    {
        return Subtract(left, right);
    }

    public static Quaternion operator *(Quaternion left, Quaternion right)
    {
        return Multiply(left, right);
    }

    public static Quaternion operator *(float scale, Quaternion value)
    {
        return Multiply(value, scale);
    }

    public static Quaternion operator -(in Quaternion value)
    {
        return Negate(value);
    }

    public static Quaternion operator /(Quaternion left, Quaternion right)
    {
        return System.Numerics.Quaternion.Divide(left, right);
    }
    public bool Equals(Quaternion other)
    {
        return (MathF.Abs(other.X - X) < MathUtilities.ZeroTolerance &&
    MathF.Abs(other.Y - Y) < MathUtilities.ZeroTolerance &&
    MathF.Abs(other.Z - Z) < MathUtilities.ZeroTolerance &&
    MathF.Abs(other.W - W) < MathUtilities.ZeroTolerance);
    }
}
