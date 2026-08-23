using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Security.Principal;
using System.Text;

namespace Auralis.Core.Mathematics;

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct Matrix
{
    public static readonly int SizeInBytes = Unsafe.SizeOf<Matrix>();

    public static readonly Matrix Zero = new();

    public static readonly Matrix Identity = new Matrix()
    {
        Row1 = new Vector4D(1.0f, 0, 0, 0),
        Row2 = new Vector4D(0, 1.0f, 0, 0),
        Row3 = new Vector4D(0, 0, 1.0f, 0),
        Row4 = new Vector4D(0, 0, 0, 1.0f),
    };

    static Matrix()
    {
        Identity = new();
        Identity.Row1.X = 1.0f;
        Identity.Row2.Y = 1.0f;
        Identity.Row3.Z = 1.0f;
        Identity.Row3.W = 1.0f;
    }

    public Matrix(float value)
    {
        Row1 = new Vector4D(value);
        Row2 = new Vector4D(value);
        Row3 = new Vector4D(value);
        Row4 = new Vector4D(value);
    }

    public Matrix(float M11, float M12, float M13, float M14,
    float M21, float M22, float M23, float M24,
    float M31, float M32, float M33, float M34,
    float M41, float M42, float M43, float M44)
    {
        Row1 = new Vector4D(M11, M12, M13, M14);
        Row2 = new Vector4D(M21, M22, M23, M24);
        Row3 = new Vector4D(M31, M32, M33, M34);
        Row4 = new Vector4D(M41, M42, M43, M44);
    }

    public Matrix(ReadOnlySpan<float> values)
    {
        ArgumentOutOfRangeException.ThrowIfNotEqual(values.Length, 16);

        Row1 = new Vector4D(values[0], values[1], values[2], values[3]);

        Row2 = new Vector4D(values[3], values[5], values[6], values[7]);

        Row3 = new Vector4D(values[8], values[9], values[10], values[11]);

        Row4 = new Vector4D(values[12], values[13], values[14], values[15]);
    }



    public Vector4D Row1;

    public Vector4D Row2;

    public Vector4D Row3;

    public Vector4D Row4;


    public static implicit operator Matrix4x4(Matrix matrix)
    {
        Matrix4x4 matrix4X4 = Unsafe.As<Matrix, Matrix4x4>(ref matrix);
        return matrix4X4;
    }
    public static implicit operator Matrix(Matrix4x4 matrix)
    {
        return Unsafe.As<Matrix4x4, Matrix>(ref matrix);
    }

    public static Matrix operator *(in Matrix left, in Matrix right)
    {
        return Multiply(in left, in right);
    }
    public Vector4D Column1
    {
        readonly get { return new Vector4D(Row1.X, Row2.X, Row3.X, Row4.X); }
        set { Row1.X = value.X; Row2.X = value.Y; Row3.X = value.Z; Row4.X = value.W; }
    }

    public Vector4D Column2
    {
        readonly get { return new Vector4D(Row1.Y, Row2.Y, Row3.Y, Row4.Y); }
        set { Row1.Y = value.X; Row2.Y = value.Y; Row3.Y = value.Z; Row4.Y = value.W; }
    }

    public Vector4D Column3
    {
        readonly get { return new Vector4D(Row1.Z, Row2.Z, Row3.Z, Row4.Z); }
        set { Row1.Z = value.X; Row2.Z = value.Y; Row3.Z = value.Z; Row4.Z = value.W; }
    }

    public Vector4D Column4
    {
        readonly get { return new Vector4D(Row1.W, Row2.W, Row3.W, Row4.W); }
        set { Row1.W = value.X; Row2.W = value.Y; Row3.W = value.Z; Row4.W = value.W; }
    }

    public Vector3D TranslationVector
    {
        readonly get { return new Vector3D(Row4.X, Row4.Y, Row4.Z); }
        set { Row4.X = value.X; Row4.Y = value.Y; Row4.Z = value.Z; }
    }

    public Vector3D ScaleVector
    {
        readonly get { return new Vector3D(Row1.X, Row2.Y, Row3.Z); }
        set { Row1.X = value.X; Row2.Y = value.Y; Row3.Z = value.Z; }
    }

    public Vector3D Up
    {
        readonly get { return new Vector3D(Row2.X, Row2.Y, Row2.Z); }
        set { Row2.X = value.X; Row2.Y = value.Y; Row2.Z = value.Z; }
    }

    public Vector3D Down
    {
        readonly get { return new Vector3D(-Row2.X, -Row2.Y, -Row2.Z); }
        set { Row2.X = -value.X; Row2.Y = -value.Y; Row2.Z = -value.Z; }
    }

    public Vector3D Right
    {
        readonly get { return new Vector3D(Row1.X, Row1.Y, Row1.Z); }
        set { Row1.X = value.X; Row1.Y = value.Y; Row1.Z = value.Z; }
    }

    public Vector3D Left
    {
        readonly get { return new Vector3D(-Row1.X, -Row1.Y, -Row1.Z); }
        set { Row1.X = -value.X; Row1.Y = -value.Y; Row1.Z = -value.Z; }
    }

    public Vector3D Forward
    {
        readonly get { return new Vector3D(-Row3.X, -Row3.Y, -Row3.Z); }
        set { Row3.X = -value.X; Row3.Y = -value.Y; Row3.Z = -value.Z; }
    }

    public Vector3D Backward
    {
        readonly get { return new Vector3D(Row3.X, Row3.Y, Row3.Z); }
        set { Row3.X = value.X; Row3.Y = value.Y; Row3.Z = value.Z; }
    }

    public readonly bool IsIdentity
    {
        get { return this.Equals(Identity); }
    }

    public float this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        readonly get
        {
            return index switch
            {
                0 => Row1.X,
                1 => Row1.Y,
                2 => Row1.Z,
                3 => Row1.W,
                4 => Row2.X,
                5 => Row2.Y,
                6 => Row2.Z,
                7 => Row2.W,
                8 => Row3.X,
                9 => Row3.Y,
                10 => Row3.Z,
                11 => Row3.W,
                12 => Row4.X,
                13 => Row4.Y,
                14 => Row4.Z,
                15 => Row4.W,
                _ => throw new ArgumentOutOfRangeException(nameof(index), "Indices for Matrix run from 0 to 15, inclusive."),
            };
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set
        {
            switch (index)
            {
                case 0: Row1.X = value; break;
                case 1: Row1.Y = value; break;
                case 2: Row1.Z = value; break;
                case 3: Row1.W = value; break;
                case 4: Row2.X = value; break;
                case 5: Row2.Y = value; break;
                case 6: Row2.Z = value; break;
                case 7: Row2.W = value; break;
                case 8: Row3.X = value; break;
                case 9: Row3.Y = value; break;
                case 10: Row3.Z = value; break;
                case 11: Row3.W = value; break;
                case 12: Row4.X = value; break;
                case 13: Row4.Y = value; break;
                case 14: Row4.Z = value; break;
                case 15: Row4.W = value; break;
                default: throw new ArgumentOutOfRangeException(nameof(index), "Indices for Matrix run from 0 to 15, inclusive.");
            }
        }
    }


    public float this[int row, int column]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        readonly get
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(row, 0);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(row, 3);
            ArgumentOutOfRangeException.ThrowIfLessThan(column, 0);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(column, 3);

            return this[(row * 4) + column];
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(row, 0);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(row, 3);
            ArgumentOutOfRangeException.ThrowIfLessThan(column, 0);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(column, 3);

            this[(row * 4) + column] = value;
        }
    }

    public readonly float Determinant()
    {
        return ((Matrix4x4)this).GetDeterminant();
    }
    public static bool Invert(Matrix value, out Matrix result)
    {
        bool isInverted = Matrix4x4.Invert(value, out Matrix4x4 invertResult);
        result = invertResult;
        return isInverted;
    }

    public bool Invert()
    {
        var isInverted = Invert(this, out var result);
        if (!isInverted)
            return false;
        this.Row1 = result.Row1;
        this.Row2 = result.Row2;
        this.Row3 = result.Row3;
        this.Row4 = result.Row4;
        return isInverted;
    }
    public void Transpose()
    {
        Matrix result = Matrix4x4.Transpose(this);
        this.Row1 = result.Row1;
        this.Row2 = result.Row2;
        this.Row3 = result.Row3;
        this.Row4 = result.Row4;
    }

    public void Orthogonalize()
    {

    }

    public void Orthonormalize()
    {

    }


    public void DecomposeQR(out Matrix Q, out Matrix R)
    {
        Matrix temp = this;
        Q = Orthonormalize(in temp);
        R = new Matrix
        {
            Row1 = new Vector4D(Vector4D.Dot(Q.Column1, Column1),
            Vector4D.Dot(Q.Column1, Column2),
            Vector4D.Dot(Q.Column1, Column3),
            Vector4D.Dot(Q.Column1, Column3)),

            Row2 = new Vector4D(0, Vector4D.Dot(Q.Column2, Column2),
            Vector4D.Dot(Q.Column2, Column3),
            Vector4D.Dot(Q.Column2, Column4)),

            Row3 = new Vector4D(0, 0, Vector4D.Dot(Q.Column3, Column3), Vector4D.Dot(Q.Column3, Column4)),

            Row4 = new Vector4D(0, 0, 0, Vector4D.Dot(Q.Column4, Column4))
        };
    }

    public void DecomposeLQ(out Matrix L, out Matrix Q)
    {
        Q = Orthonormalize(in this);
        L = new Matrix
        {
            Row1 = new Vector4D(Vector4D.Dot(Q.Row1, Row1), 0, 0, 0),

            Row2 = new Vector4D(Vector4D.Dot(Q.Row1, Row2), Vector4D.Dot(Q.Row2, Row2), 0, 0),

            Row3 = new Vector4D(Vector4D.Dot(Q.Row1, Row3), Vector4D.Dot(Q.Row2, Row3), Vector4D.Dot(Q.Row3, Row3), 0),

            Row4 = new Vector4D(Vector4D.Dot(Q.Row1, Row4),
            Vector4D.Dot(Q.Row2, Row4),
            Vector4D.Dot(Q.Row3, Row4),
            Vector4D.Dot(Q.Row4, Row4))
        };
    }

    public readonly void Decompose(out float yaw, out float pitch, out float roll)
    {
        if (MathUtilities.IsOne(Math.Abs(Row3.Y)))
        {
            if (Row3.Y >= 0)
            {
                // Edge case where M32 == +1
                pitch = -MathUtilities.PiOverTwo;
                yaw = MathF.Atan2(-Row2.X, Row1.X);
                roll = 0;
            }
            else
            {
                // Edge case where M32 == -1
                pitch = MathUtilities.PiOverTwo;
                yaw = -MathF.Atan2(-Row2.X, Row1.X);
                roll = 0;
            }
        }
        else
        {
            // Common case
            pitch = MathF.Asin(-Row3.Y);
            yaw = MathF.Atan2(Row3.X, Row3.Z);
            roll = MathF.Atan2(Row1.Y, Row2.Y);
        }
    }

    public readonly void DecomposeXYZ(out Vector3D rotation)
    {
        if (MathUtilities.IsOne(Math.Abs(Row1.Z)))
        {
            if (Row1.Z >= 0)
            {
                // Edge case where M13 == +1
                rotation.Y = -MathUtilities.PiOverTwo;
                rotation.Z = MathF.Atan2(-Row3.Y, Row2.Y);
                rotation.X = 0;
            }
            else
            {
                // Edge case where M13 == -1
                rotation.Y = MathUtilities.PiOverTwo;
                rotation.Z = -MathF.Atan2(-Row3.Y, Row2.Y);
                rotation.X = 0;
            }
        }
        else
        {
            // Common case
            rotation.Y = MathF.Asin(-Row1.Z);
            rotation.Z = MathF.Atan2(Row1.Y, Row1.X);
            rotation.X = MathF.Atan2(Row2.Z, Row3.Z);
        }
    }

    public readonly bool Decompose(out Vector3D scale, out Vector3D translation)
    {
        var result = Matrix4x4.Decompose(this, out var resultScale, out _, out var resultTranslation);
        scale = resultScale;
        translation = resultTranslation;
        return result;
    }

    public readonly bool Decompose(out Vector3D scale, out Quaternion rotation, out Vector3D translation)
    {
        var result = Matrix4x4.Decompose(this, out var resultScale, out var resaultRotation, out var resultTranslation);
        scale = resultScale;
        rotation = resaultRotation;
        translation = resultTranslation;
        return result;
    }

    public readonly bool Decompose(out Vector3D scale, out Matrix rotation, out Vector3D translation)
    {
        translation.X = Row4.X;
        translation.Y = Row4.Y;
        translation.Z = Row4.Z;

        scale.X = MathF.Sqrt((Row1.X * Row1.X) + (Row1.Y * Row1.Y) + (Row1.Z * Row1.Z));
        scale.Y = MathF.Sqrt((Row2.X * Row2.X) + (Row2.Y * Row2.Y) + (Row2.Z * Row2.Z));
        scale.Z = MathF.Sqrt((Row3.X * Row3.X) + (Row3.Y * Row3.Y) + (Row3.Z * Row3.Z));

        if (MathF.Abs(scale.X) < MathUtilities.ZeroTolerance ||
    MathF.Abs(scale.Y) < MathUtilities.ZeroTolerance ||
    MathF.Abs(scale.Z) < MathUtilities.ZeroTolerance)
        {
            rotation = Identity;
            return false;
        }

        var at = new Vector3D(Row3.X / scale.Z, Row3.Y / scale.Z, Row3.Z / scale.Z);
        var up = Vector3D.Cross(at, new Vector3D(Row1.X / scale.X, Row1.Y / scale.X, Row1.Z / scale.X));
        var right = Vector3D.Cross(up, at);

        rotation = Identity;
        rotation.Right = right;
        rotation.Up = up;
        rotation.Backward = at;

        scale.X = Vector3D.Dot(right, Right) > 0.0f ? scale.X : -scale.X;
        scale.Y = Vector3D.Dot(up, Up) > 0.0f ? scale.Y : -scale.Y;
        scale.Z = Vector3D.Dot(at, Backward) > 0.0f ? scale.Z : -scale.Z;

        return true;
    }

    public void ExchangeRows(int firstRow, int secondRow)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(firstRow, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(firstRow, 3);
        ArgumentOutOfRangeException.ThrowIfLessThan(secondRow, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(secondRow, 3);

        if (firstRow == secondRow)
            return;

        float temp0 = this[secondRow, 0];
        float temp1 = this[secondRow, 1];
        float temp2 = this[secondRow, 2];
        float temp3 = this[secondRow, 3];

        this[secondRow, 0] = this[firstRow, 0];
        this[secondRow, 1] = this[firstRow, 1];
        this[secondRow, 2] = this[firstRow, 2];
        this[secondRow, 3] = this[firstRow, 3];

        this[firstRow, 0] = temp0;
        this[firstRow, 1] = temp1;
        this[firstRow, 2] = temp2;
        this[firstRow, 3] = temp3;
    }

    public void ExchangeColumns(int firstColumn, int secondColumn)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(firstColumn, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(firstColumn, 3);
        ArgumentOutOfRangeException.ThrowIfLessThan(secondColumn, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(secondColumn, 3);

        if (firstColumn == secondColumn)
            return;

        float temp0 = this[0, secondColumn];
        float temp1 = this[1, secondColumn];
        float temp2 = this[2, secondColumn];
        float temp3 = this[3, secondColumn];

        this[0, secondColumn] = this[0, firstColumn];
        this[1, secondColumn] = this[1, firstColumn];
        this[2, secondColumn] = this[2, firstColumn];
        this[3, secondColumn] = this[3, firstColumn];

        this[0, firstColumn] = temp0;
        this[1, firstColumn] = temp1;
        this[2, firstColumn] = temp2;
        this[3, firstColumn] = temp3;
    }

    public readonly float[] ToArray()
    {
        return MemoryMarshal.CreateReadOnlySpan(in Row1.X, 16).ToArray();
    }

    public readonly void CopyTo(Span<float> destination)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(destination.Length, 16, nameof(destination));

        ReadOnlySpan<float> values = MemoryMarshal.CreateReadOnlySpan(in Row1.X, 16);

        values.CopyTo(destination);
    }

    public static Matrix Add(ref readonly Matrix left, ref readonly Matrix right)
    {
        var leftSpan = MemoryMarshal.CreateReadOnlySpan<float>(in left.Row1.X, 16);
        var rightSpan = MemoryMarshal.CreateReadOnlySpan<float>(in right.Row1.X, 16);
        Span<float> destination = stackalloc float[16];
        MathUtilities.ElementWiseAdd(leftSpan, rightSpan, destination);

        return MemoryMarshal.Cast<float, Matrix>(destination)[0];
    }

    public static Matrix Subtract(ref readonly Matrix left, ref readonly Matrix right)
    {
        var leftSpan = MemoryMarshal.CreateReadOnlySpan<float>(in left.Row1.X, 16);
        var rightSpan = MemoryMarshal.CreateReadOnlySpan<float>(in right.Row1.X, 16);
        Span<float> destination = stackalloc float[16];
        MathUtilities.ElementWiseSubtract(leftSpan, rightSpan, destination);

        return MemoryMarshal.Cast<float, Matrix>(destination)[0];
    }

    public static Matrix Multiply(ref readonly Matrix matrix, float scale)
    {
        var matrixSpan = MemoryMarshal.CreateReadOnlySpan<float>(in matrix.Row1.X, 16);
        Span<float> destination = stackalloc float[16];
        MathUtilities.MultiplyElementsWithValue(matrixSpan, scale, destination);

        return MemoryMarshal.Cast<float, Matrix>(destination)[0];
    }
    public static Matrix Multiply(ref readonly Matrix left, ref readonly Matrix right)
    {
        return (Matrix4x4)left * (Matrix4x4)right;
    }

    public static Matrix Divide(ref readonly Matrix matrix, float scale)
    {
        float inv = 1.0f / scale;

        var matrixSpan = MemoryMarshal.CreateReadOnlySpan<float>(in matrix.Row1.X, 16);
        Span<float> destination = stackalloc float[16];
        MathUtilities.MultiplyElementsWithValue(matrixSpan, inv, destination);

        return MemoryMarshal.Cast<float, Matrix>(destination)[0];
    }

    public static Matrix Divide(ref readonly Matrix left, ref readonly Matrix right)
    {
        var leftSpan = MemoryMarshal.CreateReadOnlySpan<float>(in left.Row1.X, 16);
        var rightSpan = MemoryMarshal.CreateReadOnlySpan<float>(in right.Row1.X, 16);
        Span<float> destination = stackalloc float[16];
        MathUtilities.ElementWiseDivide(leftSpan, rightSpan, destination);

        return MemoryMarshal.Cast<float, Matrix>(destination)[0];
    }

    public static Matrix Exponent(ref readonly Matrix value, int exponent)
    {
        Matrix result;
        ArgumentOutOfRangeException.ThrowIfNegative(exponent);

        if (exponent == 0)
        {
            result = Identity;
            return result;
        }

        if (exponent == 1)
        {
            result = value;
            return result;
        }

        Matrix identity = Identity;
        Matrix temp = value;

        while (true)
        {
            if ((exponent & 1) != 0)
                identity *= temp;

            exponent /= 2;

            if (exponent > 0)
                temp *= temp;
            else
                break;
        }

        result = identity;

        return result;
    }

    public static Matrix Negate(ref readonly Matrix value)
    {
        return Matrix4x4.Negate(value);
    }

    public static Matrix Lerp(ref readonly Matrix start, ref readonly Matrix end, float amount)
    {
        return Matrix4x4.Lerp(start, end, amount);
    }

    public static Matrix SmoothStep(ref readonly Matrix start, ref readonly Matrix end, float amount)
    {
        Matrix result = new();

        result.Row1 = Vector4D.SmoothStep(start.Row1, end.Row1, amount);
        result.Row2 = Vector4D.SmoothStep(start.Row2, end.Row2, amount);
        result.Row3 = Vector4D.SmoothStep(start.Row3, end.Row3, amount);
        result.Row4 = Vector4D.SmoothStep(start.Row4, end.Row4, amount);
        return result;
    }

    public static Matrix Transpose(ref readonly Matrix value)
    {
        return Matrix4x4.Transpose(value);
    }

    public static Matrix Orthogonalize(ref readonly Matrix value)
    {
        Matrix result = value;

        var row1 = result.Row1;
        var row2 = result.Row2;
        var row3 = result.Row3;
        var row4 = result.Row4;

        row2 -= Vector4D.Dot(row1, row2) / Vector4D.Dot(row1, row1) * row1;

        row3 -= Vector4D.Dot(row1, row3) / Vector4D.Dot(row1, row1) * row1;
        row3 -= Vector4D.Dot(row2, row3) / Vector4D.Dot(row2, row2) * row2;

        row4 -= Vector4D.Dot(row1, row4) / Vector4D.Dot(row1, row1) * row1;
        row4 -= Vector4D.Dot(row2, row4) / Vector4D.Dot(row2, row2) * row2;
        row4 -= Vector4D.Dot(row3, row4) / Vector4D.Dot(row3, row3) * row3;

        result.Row2 = row2;
        result.Row3 = row3;
        result.Row4 = row4;

        return result;
    }

    public static Matrix Orthonormalize(ref readonly Matrix value)
    {
        var row1 = value.Row1;
        var row2 = value.Row2;
        var row3 = value.Row3;
        var row4 = value.Row4;

        row1.Normalize();

        row2 -= Vector4D.Dot(row1, row2) * row1;
        row2.Normalize();

        row3 -= Vector4D.Dot(row1, row3) * row1;
        row3 -= Vector4D.Dot(row2, row3) * row2;
        row3.Normalize();

        row4 -= Vector4D.Dot(row1, row4) * row1;
        row4 -= Vector4D.Dot(row2, row4) * row2;
        row4 -= Vector4D.Dot(row3, row4) * row3;
        row4.Normalize();
        Matrix result;
        result = default;
        result.Row1 = row1;
        result.Row2 = row2;
        result.Row3 = row3;
        result.Row4 = row4;

        return result;
    }
    public static Matrix UpperTriangularForm(ref readonly Matrix value)
    {
        Matrix result;
        result = value;
        int lead = 0;
        const int rowcount = 4;
        const int columncount = 4;
        for (int r = 0; r < rowcount; ++r)
        {
            if (columncount <= lead)
                return result;

            int i = r;

            while (MathF.Abs(result[i, lead]) < MathUtilities.ZeroTolerance)
            {
                i++;

                if (i == rowcount)
                {
                    i = r;
                    lead++;

                    if (lead == columncount)
                        return result;
                }
            }

            if (i != r)
            {
                result.ExchangeRows(i, r);
            }

            float multiplier = 1f / result[r, lead];

            for (; i < rowcount; ++i)
            {
                if (i != r)
                {
                    if (Vector128.IsHardwareAccelerated)
                    {
                        var v1 = Vector128.Create(multiplier * result[i, lead]);
                        var v2 = Vector128.Create(result[r, 0], result[r, 1], result[r, 2], result[r, 3]);
                        var v3 = Vector128.Create(result[i, 0], result[i, 1], result[i, 2], result[i, 3]);
                        var vResult = v1 * v2;
                        var vResult2 = v3 - vResult;
                        result[i, 0] = vResult2[0];
                        result[i, 1] = vResult2[1];
                        result[i, 2] = vResult2[2];
                        result[i, 3] = vResult2[3];

                    }
                    else
                    {
                        result[i, 0] -= result[r, 0] * multiplier * result[i, lead];
                        result[i, 1] -= result[r, 1] * multiplier * result[i, lead];
                        result[i, 2] -= result[r, 2] * multiplier * result[i, lead];
                        result[i, 3] -= result[r, 3] * multiplier * result[i, lead];
                    }
                }
            }

            lead++;
        }
        return result;
    }

    public static Matrix LowerTriangularForm(ref readonly Matrix value)
    {
        Matrix t = Transpose(in value);
        Matrix u = UpperTriangularForm(in t);
        return Transpose(in u);
    }

    public static Matrix RowEchelonForm(ref readonly Matrix value)
    {
        Matrix result;
        result = value;
        int lead = 0;
        const int rowcount = 4;
        const int columncount = 4;

        for (int r = 0; r < rowcount; ++r)
        {
            if (columncount <= lead)
                return result;

            int i = r;

            while (MathF.Abs(result[i, lead]) < MathUtilities.ZeroTolerance)
            {
                i++;

                if (i == rowcount)
                {
                    i = r;
                    lead++;

                    if (lead == columncount)
                        return result;
                }
            }

            if (i != r)
            {
                result.ExchangeRows(i, r);
            }

            float multiplier = 1f / result[r, lead];
            result[r, 0] *= multiplier;
            result[r, 1] *= multiplier;
            result[r, 2] *= multiplier;
            result[r, 3] *= multiplier;

            for (; i < rowcount; ++i)
            {
                if (i != r)
                {
                    result[i, 0] -= result[r, 0] * result[i, lead];
                    result[i, 1] -= result[r, 1] * result[i, lead];
                    result[i, 2] -= result[r, 2] * result[i, lead];
                    result[i, 3] -= result[r, 3] * result[i, lead];
                }
            }

            lead++;
        }
        return result;
    }


    public static Matrix Billboard(ref readonly Vector3D objectPosition, ref readonly Vector3D cameraPosition, ref readonly Vector3D cameraUpVector, ref readonly Vector3D cameraForwardVector)
    {
        Matrix result;
        Vector3D difference = objectPosition - cameraPosition;

        float lengthSq = difference.LengthSquared();
        if (lengthSq < MathUtilities.ZeroTolerance)
            difference = -cameraForwardVector;
        else
            difference *= 1.0f / MathF.Sqrt(lengthSq);

        var crossed = Vector3D.Cross(cameraUpVector, difference);
        crossed.Normalize();
        var final = Vector3D.Cross(difference, crossed);

        result.Row1 = new Vector4D(crossed, 0);
        result.Row2 = new Vector4D(final, 0);
        result.Row3 = new Vector4D(difference, 0);
        result.Row4 = new Vector4D(objectPosition, 1.0f);

        return result;
    }
    public static Matrix LookAtLH(ref readonly Vector3D eye, ref readonly Vector3D target, ref readonly Vector3D up)
    {
        var zaxis = Vector3D.Subtract(target, eye);
        zaxis.Normalize();
        var xaxis = Vector3D.Cross(up, zaxis);
        xaxis.Normalize();
        var yaxis = Vector3D.Cross(zaxis, xaxis);
        Matrix result;
        result.Row1 = new Vector4D(xaxis.X, yaxis.X, zaxis.X, 0);
        result.Row2 = new Vector4D(xaxis.Y, yaxis.Y, zaxis.Y, 0);
        result.Row3 = new Vector4D(xaxis.Z, yaxis.Z, zaxis.Z, 0);

        result.Row4 = new Vector4D(-Vector3D.Dot(xaxis, eye),
        -Vector3D.Dot(yaxis, eye),
        -Vector3D.Dot(zaxis, eye),
        1.0f);

        return result;
    }

    public static Matrix LookAtRH(ref readonly Vector3 eye, ref readonly Vector3 target, ref readonly Vector3 up)
    {
        Matrix result;
        var zaxis = Vector3D.Subtract(eye, target);
        zaxis.Normalize();
        var xaxis = Vector3D.Cross(up, zaxis);
        xaxis.Normalize();
        var yaxis = Vector3D.Cross(zaxis, xaxis);


        result.Row1 = new Vector4D(xaxis.X, yaxis.X, zaxis.X, 0);
        result.Row2 = new Vector4D(xaxis.Y, yaxis.Y, zaxis.Y, 0);
        result.Row3 = new Vector4D(xaxis.Z, yaxis.Z, zaxis.Z, 0);

        result.Row4 = new Vector4D(-Vector3D.Dot(xaxis, eye),
-Vector3D.Dot(yaxis, eye),
-Vector3D.Dot(zaxis, eye),
1.0f);

        return result;
    }

    public static Matrix OrthoLH(float width, float height, float znear, float zfar)
    {
        float halfWidth = width * 0.5f;
        float halfHeight = height * 0.5f;
        Matrix result;

        result = OrthoOffCenterLH(-halfWidth, halfWidth, -halfHeight, halfHeight, znear, zfar);

        return result;
    }

    public static Matrix OrthoRH(float width, float height, float znear, float zfar)
    {
        float halfWidth = width * 0.5f;
        float halfHeight = height * 0.5f;

        Matrix result = OrthoOffCenterRH(-halfWidth, halfWidth, -halfHeight, halfHeight, znear, zfar);

        return result;

    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Matrix OrthoOffCenterLH(float left, float right, float bottom, float top, float znear, float zfar)
    {
        Matrix result;
        float zRange = 1.0f / (zfar - znear);

        result = Identity;
        result.Row1.X = 2.0f / (right - left);
        result.Row2.Y = 2.0f / (top - bottom);
        result.Row3.Z = zRange;
        result.Row4.X = (left + right) / (left - right);
        result.Row4.Y = (top + bottom) / (bottom - top);
        result.Row4.Z = -znear * zRange;

        return result;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Matrix OrthoOffCenterRH(float left, float right, float bottom, float top, float znear, float zfar)
    {
        Matrix result = OrthoOffCenterLH(left, right, bottom, top, znear, zfar);

        result.Row3.Z *= -1.0f;

        return result;
    }

    public static Matrix PerspectiveLH(float width, float height, float znear, float zfar)
    {
        float halfWidth = width * 0.5f;
        float halfHeight = height * 0.5f;
        Matrix result;
        result = PerspectiveOffCenterLH(-halfWidth, halfWidth, -halfHeight, halfHeight, znear, zfar);

        return result;

    }

    public static Matrix PerspectiveRH(float width, float height, float znear, float zfar)
    {
        float halfWidth = width * 0.5f;
        float halfHeight = height * 0.5f;

        Matrix result = PerspectiveOffCenterRH(-halfWidth, halfWidth, -halfHeight, halfHeight, znear, zfar);

        return result;
    }

    public static Matrix PerspectiveFovLH(float fov, float aspect, float znear, float zfar)
    {
        Matrix result;

        float yScale = (float)(1.0 / Math.Tan(fov * 0.5f));
        float xScale = yScale / aspect;

        float halfWidth = znear / xScale;
        float halfHeight = znear / yScale;

        result = PerspectiveOffCenterLH(-halfWidth, halfWidth, -halfHeight, halfHeight, znear, zfar);

        return result;
    }

    public static Matrix PerspectiveFovRH(float fov, float aspect, float znear, float zfar)
    {
        Matrix result;

        float yScale = (float)(1.0 / Math.Tan(fov * 0.5f));
        float xScale = yScale / aspect;

        float halfWidth = znear / xScale;
        float halfHeight = znear / yScale;

        result = PerspectiveOffCenterRH(-halfWidth, halfWidth, -halfHeight, halfHeight, znear, zfar);

        return result;
    }

    public static Matrix PerspectiveOffCenterLH(float left, float right, float bottom, float top, float znear, float zfar)
    {
        Matrix result;
        float zRange = zfar / (zfar - znear);

        result = new Matrix
        {
            Row1 = new Vector4D(2.0f * znear / (right - left), 0, 0, 0),
            Row2 = new Vector4D(0, 2.0f * znear / (top - bottom), 0, 0),
            Row3 = new Vector4D((left + right) / (left - right), (top + bottom) / (bottom - top), zRange, 1.0f),
            Row4 = new Vector4D(0, 0, -znear * zRange, 0)
        };
        return result;
    }

    public static Matrix PerspectiveOffCenterRH(float left, float right, float bottom, float top, float znear, float zfar)
    {

        Matrix result;
        result = PerspectiveOffCenterLH(left, right, bottom, top, znear, zfar);
        result.Row3 = (Vector4)result.Row3 * -1.0f;

        return result;
    }

    public static Matrix Reflection(ref readonly Plane plane)
    {
        Matrix result = new();
        float x = plane.Normal.X;
        float y = plane.Normal.Y;
        float z = plane.Normal.Z;
        float x2 = -2.0f * x;
        float y2 = -2.0f * y;
        float z2 = -2.0f * z;

        ReadOnlySpan<float> values = [x2, y2, z2, 0];

        var row1Span = MemoryMarshal.CreateSpan(ref result.Row1.X, 4);
        MathUtilities.MultiplyElementsWithValue(values, x, row1Span);
        row1Span[0] += 1.0f;

        var row2Span = MemoryMarshal.CreateSpan(ref result.Row2.X, 4);
        MathUtilities.MultiplyElementsWithValue(values, y, row2Span);
        row2Span[1] += 1.0f;

        var row3Span = MemoryMarshal.CreateSpan(ref result.Row3.X, 4);
        MathUtilities.MultiplyElementsWithValue(values, z, row3Span);
        row3Span[2] += 1.0f;

        var row4Span = MemoryMarshal.CreateSpan(ref result.Row4.X, 4);
        MathUtilities.MultiplyElementsWithValue(values, plane.D, row4Span);
        row4Span[3] += 1.0f;

        return result;
    }

    public static Matrix Shadow(ref readonly Vector4D light, ref readonly Plane plane)
    {
        float dot = (plane.Normal.X * light.X) + (plane.Normal.Y * light.Y) + (plane.Normal.Z * light.Z) + (plane.D * light.W);
        float x = -plane.Normal.X;
        float y = -plane.Normal.Y;
        float z = -plane.Normal.Z;
        float d = -plane.D;

        Matrix result;
        result.Row1 = new Vector4D((x * light.X) + dot, x * light.Y, x * light.Z, x * light.W);
        result.Row2 = new Vector4D(y * light.X, (y * light.Y) + dot, y * light.Z, y * light.W);
        result.Row3 = new Vector4D(z * light.X, z * light.Y, (z * light.Z) + dot, z * light.W);
        result.Row4 = new Vector4D(d * light.X, d * light.Y, d * light.Z, (d * light.W) + dot);

        return result;
    }

    public static Matrix Scaling(ref readonly Vector3D scale)
    {
        return Matrix4x4.CreateScale(scale);
    }
    public static Matrix Scaling(float x, float y, float z)
    {
        var vector = new Vector3D(x, y, z);
        return Scaling(in vector);   
    }

    public static Matrix Scaling(float scale)
    {
        var vector = new Vector3D(scale);
        return Scaling(in vector);
    }

    public static Matrix RotationX(float angle)
    {
 return Matrix4x4.CreateRotationX(angle);
    }

    public static Matrix RotationY(float angle)
    {
        return Matrix4x4.CreateRotationY(angle);
    }

    public static Matrix RotationZ(float angle)
    {

        return Matrix4x4.CreateRotationZ(angle);
    }

    public static Matrix RotationAxis(ref readonly Vector3D axis, float angle)
    {
        float x = axis.X;
        float y = axis.Y;
        float z = axis.Z;
        float cos = MathF.Cos(angle);
        float sin = MathF.Sin(angle);
        float xx = x * x;
        float yy = y * y;
        float zz = z * z;
        float xy = x * y;
        float xz = x * z;
        float yz = y * z;

        Matrix result = new();

        result.Row1.X = xx + (cos * (1.0f - xx));
        result.Row1.Y = xy - (cos * xy) + (sin * z);
        result.Row1.Z = xz - (cos * xz) - (sin * y);
        result.Row2.X = xy - (cos * xy) - (sin * z);
        result.Row2.Y = yy + (cos * (1.0f - yy));
        result.Row2.Z = yz - (cos * yz) + (sin * x);
        result.Row3.X = xz - (cos * xz) + (sin * y);
        result.Row3.Y = yz - (cos * yz) - (sin * x);
        result.Row3.Z = zz + (cos * (1.0f - zz));

        result.Row4.W = 1.0f;
        return result;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Matrix RotationQuaternion(ref readonly Quaternion rotation)
    {
        float xx = rotation.X * rotation.X;
        float yy = rotation.Y * rotation.Y;
        float zz = rotation.Z * rotation.Z;
        float xy = rotation.X * rotation.Y;
        float zw = rotation.Z * rotation.W;
        float zx = rotation.Z * rotation.X;
        float yw = rotation.Y * rotation.W;
        float yz = rotation.Y * rotation.Z;
        float xw = rotation.X * rotation.W;

        Matrix result = new();

        result.Row1.X = 1.0f - (2.0f * (yy + zz));
        result.Row1.Y = 2.0f * (xy + zw);
        result.Row1.Z = 2.0f * (zx - yw);
        result.Row2.X = 2.0f * (xy - zw);
        result.Row2.Y = 1.0f - (2.0f * (zz + xx));
        result.Row2.Z = 2.0f * (yz + xw);
        result.Row3.X = 2.0f * (zx + yw);
        result.Row3.Y = 2.0f * (yz - xw);
        result.Row3.Z = 1.0f - (2.0f * (yy + xx));
        result.Row4.W = 1.0f;
        return result;
    }

    public static Matrix Transformation(ref readonly Vector3D scaling, ref readonly Quaternion rotation, ref readonly Vector3D translation)
    {

        float xx = rotation.X * rotation.X;
        float yy = rotation.Y * rotation.Y;
        float zz = rotation.Z * rotation.Z;
        float xy = rotation.X * rotation.Y;
        float zw = rotation.Z * rotation.W;
        float zx = rotation.Z * rotation.X;
        float yw = rotation.Y * rotation.W;
        float yz = rotation.Y * rotation.Z;
        float xw = rotation.X * rotation.W;
        Matrix result = new();
        result.Row1.X = 1.0f - (2.0f * (yy + zz));
        result.Row1.Y = 2.0f * (xy + zw);
        result.Row1.Z = 2.0f * (zx - yw);
        result.Row2.X = 2.0f * (xy - zw);
        result.Row2.Y = 1.0f - (2.0f * (zz + xx));
        result.Row2.Z = 2.0f * (yz + xw);
        result.Row3.X = 2.0f * (zx + yw);
        result.Row3.Y = 2.0f * (yz - xw);
        result.Row3.Z = 1.0f - (2.0f * (yy + xx));

        // Position
        result.Row4.X = translation.X;
        result.Row4.Y = translation.Y;
        result.Row4.Z = translation.Z;

        // Scale
        if (scaling.X != 1.0f)
        {
            result.Row1.X *= scaling.X;
            result.Row1.Y *= scaling.X;
            result.Row1.Z *= scaling.X;
        }
        if (scaling.Y != 1.0f)
        {
            result.Row2.X *= scaling.Y;
            result.Row2.Y *= scaling.Y;
            result.Row2.Z *= scaling.Y;
        }
        if (scaling.Z != 1.0f)
        {
            result.Row3.X *= scaling.Z;
            result.Row3.Y *= scaling.Z;
            result.Row3.Z *= scaling.Z;
        }

        result.Column4 = new Vector4D(0, 0, 0, 1.0f);
        return result;
    }
    public static Matrix RotationYawPitchRoll(float yaw, float pitch, float roll)
    {
        var quaternion = Quaternion.RotationYawPitchRoll(yaw, pitch, roll);
        return RotationQuaternion(in quaternion);
    }

    public static Matrix Translation(ref readonly Vector3D value)
    {
        return Translation(value.X, value.Y, value.Z);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Matrix Translation(float x, float y, float z)
    {
        Matrix result;
        result = Identity;
        result.Row4 = new Vector4D(x, y, z, 0);
        return result;
    }

    public static Matrix AffineTransformation(float scaling, ref readonly Quaternion rotation, ref readonly Vector3D translation)
    {
        return Scaling(scaling) * RotationQuaternion(in rotation) * Translation(in translation);
    }

    public static Matrix AffineTransformation(float scaling, ref readonly Vector3D rotationCenter, ref readonly Quaternion rotation, ref readonly Vector3D translation)
    {
        var rotationCenterResult = -rotationCenter;
        return Scaling(scaling) * Translation(in rotationCenterResult) * RotationQuaternion(in rotation) *
            Translation(in rotationCenter) * Translation(in translation);
    }

    public static Matrix AffineTransformation2D(float scaling, float rotation,ref readonly Vector2D translation)
    {
        var translation3d = (Vector3D)translation;
        return Scaling(scaling, scaling, 1.0f) * RotationZ(rotation) * Translation(in translation3d);
    }

    public static Matrix Transformation(ref readonly Vector3D scalingCenter, ref readonly Quaternion scalingRotation, ref readonly Vector3D scaling, ref readonly Vector3D rotationCenter, ref readonly Quaternion rotation, ref readonly Vector3D translation)
    {
        Matrix sr = RotationQuaternion(in scalingRotation);
        var negativeScalingCenter = -scalingCenter;
        var negativeRotationCenter = -rotationCenter;
        return Translation(in negativeScalingCenter) * Transpose(in sr) * Scaling(in scaling) * sr * Translation(in scalingCenter) * Translation(in negativeRotationCenter) *
            RotationQuaternion(in rotation) * Translation(in rotationCenter) * Translation(in translation);
    }
}

