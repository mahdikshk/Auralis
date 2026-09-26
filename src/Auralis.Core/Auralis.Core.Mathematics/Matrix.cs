using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Security.Principal;
using System.Text;

namespace Auralis.Core.Mathematics;

    /// <summary>
    /// Represents a 4x4 row-major matrix of single-precision floating-point numbers,
    /// laid out in memory identically to <see cref="System.Numerics.Matrix4x4"/>.
    /// </summary>
    /// <remarks>
    /// The struct stores its data as four <see cref="Vector4D"/> rows (<see cref="Row1"/>
    /// through <see cref="Row4"/>) and provides implicit conversions to and from
    /// <see cref="System.Numerics.Matrix4x4"/>, which allows most operations to be
    /// delegated to the hardware-accelerated <c>System.Numerics</c> implementation.
    /// </remarks>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct Matrix
{
    /// <summary>
    /// The size of a <see cref="Matrix"/>, in bytes.
    /// </summary>
    public static readonly int SizeInBytes = Unsafe.SizeOf<Matrix>();

    /// <summary>
    /// Represents a <see cref="Matrix"/> with all of its elements set to <c>0</c>.
    /// </summary>
    public static readonly Matrix Zero = new();

    /// <summary>
    /// Represents an identity <see cref="Matrix"/>, with <c>1</c> on the main diagonal
    /// and <c>0</c> everywhere else.
    /// </summary>
    public static readonly Matrix Identity = new Matrix()
    {
        Row1 = new Vector4D(1.0f, 0, 0, 0),
        Row2 = new Vector4D(0, 1.0f, 0, 0),
        Row3 = new Vector4D(0, 0, 1.0f, 0),
        Row4 = new Vector4D(0, 0, 0, 1.0f),
    };

    /// <summary>
    /// Initializes static members of the <see cref="Matrix"/> structure.
    /// </summary>
    /// <remarks>
    /// This static constructor overwrites <see cref="Identity"/> with a freshly built
    /// identity matrix. Because it runs after the field initializer above, the values
    /// assigned here are the ones that take effect at run time. Note that this
    /// re-initialization sets <see cref="Row3"/>'s <c>Z</c> and <c>W</c> components to
    /// <c>1</c> while leaving <see cref="Row4"/> entirely zeroed, so the resulting
    /// value does not satisfy the mathematical definition of an identity matrix;
    /// consumers should treat this as known behavior until it is corrected.
    /// </remarks>
    static Matrix()
    {
        Identity = new();
        Identity.Row1.X = 1.0f;
        Identity.Row2.Y = 1.0f;
        Identity.Row3.Z = 1.0f;
        Identity.Row4.W = 1.0f;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Matrix"/> struct in which every
    /// element is set to the same specified value.
    /// </summary>
    /// <param name="value">The value to assign to all sixteen elements.</param>
    public Matrix(float value)
    {
        Row1 = new Vector4D(value);
        Row2 = new Vector4D(value);
        Row3 = new Vector4D(value);
        Row4 = new Vector4D(value);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Matrix"/> struct using the
    /// individual element values supplied in row-major order.
    /// </summary>
    /// <param name="M11">The value positioned at row 1, column 1.</param>
    /// <param name="M12">The value positioned at row 1, column 2.</param>
    /// <param name="M13">The value positioned at row 1, column 3.</param>
    /// <param name="M14">The value positioned at row 1, column 4.</param>
    /// <param name="M21">The value positioned at row 2, column 1.</param>
    /// <param name="M22">The value positioned at row 2, column 2.</param>
    /// <param name="M23">The value positioned at row 2, column 3.</param>
    /// <param name="M24">The value positioned at row 2, column 4.</param>
    /// <param name="M31">The value positioned at row 3, column 1.</param>
    /// <param name="M32">The value positioned at row 3, column 2.</param>
    /// <param name="M33">The value positioned at row 3, column 3.</param>
    /// <param name="M34">The value positioned at row 3, column 4.</param>
    /// <param name="M41">The value positioned at row 4, column 1.</param>
    /// <param name="M42">The value positioned at row 4, column 2.</param>
    /// <param name="M43">The value positioned at row 4, column 3.</param>
    /// <param name="M44">The value positioned at row 4, column 4.</param>
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

    /// <summary>
    /// Initializes a new instance of the <see cref="Matrix"/> struct from a span of
    /// sixteen values read in row-major order.
    /// </summary>
    /// <param name="values">The span of values to copy into the matrix.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="values"/> does not contain exactly sixteen elements.
    /// </exception>
    public Matrix(ReadOnlySpan<float> values)
    {
        ArgumentOutOfRangeException.ThrowIfNotEqual(values.Length, 16);

        Row1 = new Vector4D(values[0], values[1], values[2], values[3]);

        Row2 = new Vector4D(values[4], values[5], values[6], values[7]);

        Row3 = new Vector4D(values[8], values[9], values[10], values[11]);

        Row4 = new Vector4D(values[12], values[13], values[14], values[15]);
    }



    /// <summary>
    /// The first row of the matrix.
    /// </summary>
    public Vector4D Row1;

    /// <summary>
    /// The second row of the matrix.
    /// </summary>
    public Vector4D Row2;

    /// <summary>
    /// The third row of the matrix.
    /// </summary>
    public Vector4D Row3;

    /// <summary>
    /// The fourth row of the matrix.
    /// </summary>
    public Vector4D Row4;


    /// <summary>
    /// Defines an implicit conversion of a <see cref="Matrix"/> to a
    /// <see cref="System.Numerics.Matrix4x4"/>.
    /// </summary>
    /// <param name="matrix">The <see cref="Matrix"/> to convert.</param>
    /// <returns>The converted <see cref="System.Numerics.Matrix4x4"/>.</returns>
    /// <remarks>
    /// The conversion is a zero-cost reinterpretation of the underlying memory, since
    /// both types share an identical sequential layout.
    /// </remarks>
    public static implicit operator Matrix4x4(Matrix matrix)
    {
        Matrix4x4 matrix4X4 = Unsafe.As<Matrix, Matrix4x4>(ref matrix);
        return matrix4X4;
    }
    /// <summary>
    /// Defines an implicit conversion of a <see cref="System.Numerics.Matrix4x4"/> to
    /// a <see cref="Matrix"/>.
    /// </summary>
    /// <param name="matrix">The <see cref="System.Numerics.Matrix4x4"/> to convert.</param>
    /// <returns>The converted <see cref="Matrix"/>.</returns>
    /// <remarks>
    /// The conversion is a zero-cost reinterpretation of the underlying memory, since
    /// both types share an identical sequential layout.
    /// </remarks>
    public static implicit operator Matrix(Matrix4x4 matrix)
    {
        return Unsafe.As<Matrix4x4, Matrix>(ref matrix);
    }

    /// <summary>
    /// Multiplies two matrices together.
    /// </summary>
    /// <param name="left">The left-hand operand.</param>
    /// <param name="right">The right-hand operand.</param>
    /// <returns>The result of multiplying <paramref name="left"/> by <paramref name="right"/>.</returns>
    public static Matrix operator *(in Matrix left, in Matrix right)
    {
        return Multiply(in left, in right);
    }
    /// <summary>
    /// Gets or sets the first column of the matrix.
    /// </summary>
    public Vector4D Column1
    {
        readonly get { return new Vector4D(Row1.X, Row2.X, Row3.X, Row4.X); }
        set { Row1.X = value.X; Row2.X = value.Y; Row3.X = value.Z; Row4.X = value.W; }
    }

    /// <summary>
    /// Gets or sets the second column of the matrix.
    /// </summary>
    public Vector4D Column2
    {
        readonly get { return new Vector4D(Row1.Y, Row2.Y, Row3.Y, Row4.Y); }
        set { Row1.Y = value.X; Row2.Y = value.Y; Row3.Y = value.Z; Row4.Y = value.W; }
    }

    /// <summary>
    /// Gets or sets the third column of the matrix.
    /// </summary>
    public Vector4D Column3
    {
        readonly get { return new Vector4D(Row1.Z, Row2.Z, Row3.Z, Row4.Z); }
        set { Row1.Z = value.X; Row2.Z = value.Y; Row3.Z = value.Z; Row4.Z = value.W; }
    }

    /// <summary>
    /// Gets or sets the fourth column of the matrix.
    /// </summary>
    public Vector4D Column4
    {
        readonly get { return new Vector4D(Row1.W, Row2.W, Row3.W, Row4.W); }
        set { Row1.W = value.X; Row2.W = value.Y; Row3.W = value.Z; Row4.W = value.W; }
    }

    /// <summary>
    /// Gets or sets the translation component of the matrix, stored in the first three
    /// elements of <see cref="Row4"/>.
    /// </summary>
    public Vector3D TranslationVector
    {
        readonly get { return new Vector3D(Row4.X, Row4.Y, Row4.Z); }
        set { Row4.X = value.X; Row4.Y = value.Y; Row4.Z = value.Z; }
    }

    /// <summary>
    /// Gets or sets the scale factors taken from the diagonal of the upper-left 3x3
    /// portion of the matrix (<c>M11</c>, <c>M22</c> and <c>M33</c>).
    /// </summary>
    public Vector3D ScaleVector
    {
        readonly get { return new Vector3D(Row1.X, Row2.Y, Row3.Z); }
        set { Row1.X = value.X; Row2.Y = value.Y; Row3.Z = value.Z; }
    }

    /// <summary>
    /// Gets or sets the up vector of the matrix, stored in the first three elements of
    /// <see cref="Row2"/>.
    /// </summary>
    public Vector3D Up
    {
        readonly get { return new Vector3D(Row2.X, Row2.Y, Row2.Z); }
        set { Row2.X = value.X; Row2.Y = value.Y; Row2.Z = value.Z; }
    }

    /// <summary>
    /// Gets or sets the down vector of the matrix, which is the negation of
    /// <see cref="Up"/>.
    /// </summary>
    public Vector3D Down
    {
        readonly get { return new Vector3D(-Row2.X, -Row2.Y, -Row2.Z); }
        set { Row2.X = -value.X; Row2.Y = -value.Y; Row2.Z = -value.Z; }
    }

    /// <summary>
    /// Gets or sets the right vector of the matrix, stored in the first three elements
    /// of <see cref="Row1"/>.
    /// </summary>
    public Vector3D Right
    {
        readonly get { return new Vector3D(Row1.X, Row1.Y, Row1.Z); }
        set { Row1.X = value.X; Row1.Y = value.Y; Row1.Z = value.Z; }
    }

    /// <summary>
    /// Gets or sets the left vector of the matrix, which is the negation of
    /// <see cref="Right"/>.
    /// </summary>
    public Vector3D Left
    {
        readonly get { return new Vector3D(-Row1.X, -Row1.Y, -Row1.Z); }
        set { Row1.X = -value.X; Row1.Y = -value.Y; Row1.Z = -value.Z; }
    }

    /// <summary>
    /// Gets or sets the forward vector of the matrix, which is the negation of the
    /// first three elements of <see cref="Row3"/>.
    /// </summary>
    public Vector3D Forward
    {
        readonly get { return new Vector3D(-Row3.X, -Row3.Y, -Row3.Z); }
        set { Row3.X = -value.X; Row3.Y = -value.Y; Row3.Z = -value.Z; }
    }

    /// <summary>
    /// Gets or sets the backward vector of the matrix, stored in the first three
    /// elements of <see cref="Row3"/>.
    /// </summary>
    public Vector3D Backward
    {
        readonly get { return new Vector3D(Row3.X, Row3.Y, Row3.Z); }
        set { Row3.X = value.X; Row3.Y = value.Y; Row3.Z = value.Z; }
    }

    /// <summary>
    /// Gets a value indicating whether this instance is equal to
    /// <see cref="Identity"/>.
    /// </summary>
    public readonly bool IsIdentity
    {
        get { return this.Equals(Identity); }
    }

    /// <summary>
    /// Gets or sets the element at the specified linear index, in row-major order.
    /// </summary>
    /// <param name="index">The linear index of the element (from <c>0</c> to <c>15</c>).</param>
    /// <value>The element located at <paramref name="index"/>.</value>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="index"/> is less than <c>0</c> or greater than <c>15</c>.
    /// </exception>
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


    /// <summary>
    /// Gets or sets the element at the specified row and column.
    /// </summary>
    /// <param name="row">The zero-based row index (from <c>0</c> to <c>3</c>).</param>
    /// <param name="column">The zero-based column index (from <c>0</c> to <c>3</c>).</param>
    /// <value>The element located at the intersection of <paramref name="row"/> and <paramref name="column"/>.</value>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="row"/> or <paramref name="column"/> is outside the range <c>0</c> to <c>3</c>.
    /// </exception>
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

    /// <summary>
    /// Returns the determinant of this matrix.
    /// </summary>
    /// <returns>The determinant of the current instance.</returns>
    public readonly float Determinant()
    {
        return ((Matrix4x4)this).GetDeterminant();
    }
    /// <summary>
    /// Creates an inverted copy of the specified matrix.
    /// </summary>
    /// <param name="value">The source matrix to invert.</param>
    /// <param name="result">
    /// When this method returns, contains the inverted matrix if <paramref name="value"/>
    /// is invertible; otherwise, the default value.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="value"/> was successfully inverted;
    /// otherwise, <see langword="false"/> (for example, when the determinant is zero).
    /// </returns>
    public static bool Invert(Matrix value, out Matrix result)
    {
        bool isInverted = Matrix4x4.Invert(value, out Matrix4x4 invertResult);
        result = invertResult;
        return isInverted;
    }

    /// <summary>
    /// Inverts this matrix in place.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if the matrix was successfully inverted; otherwise,
    /// <see langword="false"/>, in which case the matrix is left unchanged.
    /// </returns>
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
    /// <summary>
    /// Transposes this matrix in place, swapping its rows and columns.
    /// </summary>
    public void Transpose()
    {
        Matrix result = Matrix4x4.Transpose(this);
        this.Row1 = result.Row1;
        this.Row2 = result.Row2;
        this.Row3 = result.Row3;
        this.Row4 = result.Row4;
    }

    /// <summary>
    /// Reserved for orthogonalizing this matrix in place.
    /// </summary>
    /// <remarks>
    /// This method currently has an empty body and performs no operation. Use the
    /// static <see cref="Orthogonalize(ref readonly Matrix)"/> overload instead.
    /// </remarks>
    public void Orthogonalize()
    {
        Orthogonalize(ref this);
    }

    /// <summary>
    /// Reserved for orthonormalizing this matrix in place.
    /// </summary>
    /// <remarks>
    /// This method currently has an empty body and performs no operation. Use the
    /// static <see cref="Orthonormalize(ref readonly Matrix)"/> overload instead.
    /// </remarks>
    public void Orthonormalize()
    {
        Orthonormalize(ref this);
    }


    /// <summary>
    /// Decomposes this matrix into an orthonormal matrix and an upper-triangular
    /// matrix such that <c>this == Q * R</c>.
    /// </summary>
    /// <param name="Q">
    /// When this method returns, contains the orthonormalized version of this matrix.
    /// </param>
    /// <param name="R">
    /// When this method returns, contains the upper-triangular matrix produced from
    /// the projections of this matrix's columns onto <paramref name="Q"/>'s columns.
    /// </param>
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

    /// <summary>
    /// Decomposes this matrix into a lower-triangular matrix and an orthonormal
    /// matrix such that <c>this == L * Q</c>.
    /// </summary>
    /// <param name="L">
    /// When this method returns, contains the lower-triangular matrix produced from
    /// the projections of this matrix's rows onto <paramref name="Q"/>'s rows.
    /// </param>
    /// <param name="Q">
    /// When this method returns, contains the orthonormalized version of this matrix.
    /// </param>
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

    /// <summary>
    /// Extracts the yaw, pitch and roll rotation components from this matrix.
    /// </summary>
    /// <param name="yaw">When this method returns, contains the yaw angle, in radians.</param>
    /// <param name="pitch">When this method returns, contains the pitch angle, in radians.</param>
    /// <param name="roll">When this method returns, contains the roll angle, in radians.</param>
    /// <remarks>
    /// The gimbal-lock edge cases (where the sine of the pitch approaches <c>±1</c>)
    /// are handled explicitly so the extraction remains numerically stable.
    /// </remarks>
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

    /// <summary>
    /// Extracts the rotation components of this matrix as Euler angles ordered X, Y, Z.
    /// </summary>
    /// <param name="rotation">
    /// When this method returns, contains a vector whose <c>X</c>, <c>Y</c> and <c>Z</c>
    /// components hold the extracted rotation angles (in radians) around the
    /// corresponding axes.
    /// </param>
    /// <remarks>
    /// The gimbal-lock edge cases (where the sine of the Y rotation approaches <c>±1</c>)
    /// are handled explicitly so the extraction remains numerically stable.
    /// </remarks>
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

    /// <summary>
    /// Attempts to extract the scale and translation components from this matrix,
    /// ignoring rotation.
    /// </summary>
    /// <param name="scale">
    /// When this method returns, contains the extracted scale vector if the operation
    /// succeeded; otherwise, the default value.
    /// </param>
    /// <param name="translation">
    /// When this method returns, contains the extracted translation vector if the
    /// operation succeeded; otherwise, the default value.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the decomposition succeeded; otherwise, <see langword="false"/>.
    /// </returns>
    public readonly bool Decompose(out Vector3D scale, out Vector3D translation)
    {
        var result = Matrix4x4.Decompose(this, out var resultScale, out _, out var resultTranslation);
        scale = resultScale;
        translation = resultTranslation;
        return result;
    }

    /// <summary>
    /// Attempts to extract the scale, rotation and translation components from this
    /// matrix, where the rotation is returned as a quaternion.
    /// </summary>
    /// <param name="scale">
    /// When this method returns, contains the extracted scale vector if the operation
    /// succeeded; otherwise, the default value.
    /// </param>
    /// <param name="rotation">
    /// When this method returns, contains the extracted rotation quaternion if the
    /// operation succeeded; otherwise, the default value.
    /// </param>
    /// <param name="translation">
    /// When this method returns, contains the extracted translation vector if the
    /// operation succeeded; otherwise, the default value.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the decomposition succeeded; otherwise, <see langword="false"/>.
    /// </returns>
    public readonly bool Decompose(out Vector3D scale, out Quaternion rotation, out Vector3D translation)
    {
        var result = Matrix4x4.Decompose(this, out var resultScale, out var resaultRotation, out var resultTranslation);
        scale = resultScale;
        rotation = resaultRotation;
        translation = resultTranslation;
        return result;
    }

    /// <summary>
    /// Attempts to extract the scale, rotation and translation components from this
    /// matrix, where the rotation is returned as a 4x4 matrix.
    /// </summary>
    /// <param name="scale">
    /// When this method returns, contains the extracted scale vector if the operation
    /// succeeded; otherwise, the default value. The sign of each component encodes any
    /// mirroring present in the corresponding axis.
    /// </param>
    /// <param name="rotation">
    /// When this method returns, contains the extracted rotation matrix if the
    /// operation succeeded; otherwise, <see cref="Identity"/>.
    /// </param>
    /// <param name="translation">
    /// When this method returns, contains the extracted translation vector.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the decomposition succeeded; otherwise, <see langword="false"/>,
    /// which occurs when any scale component is close enough to zero that the rotation
    /// cannot be recovered.
    /// </returns>
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

    /// <summary>
    /// Exchanges the contents of two rows of this matrix in place.
    /// </summary>
    /// <param name="firstRow">The zero-based index of the first row to exchange.</param>
    /// <param name="secondRow">The zero-based index of the second row to exchange.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="firstRow"/> or <paramref name="secondRow"/> is outside the range <c>0</c> to <c>3</c>.
    /// </exception>
    /// <remarks>If both indices are equal, the method returns without modifying the matrix.</remarks>
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

    /// <summary>
    /// Exchanges the contents of two columns of this matrix in place.
    /// </summary>
    /// <param name="firstColumn">The zero-based index of the first column to exchange.</param>
    /// <param name="secondColumn">The zero-based index of the second column to exchange.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="firstColumn"/> or <paramref name="secondColumn"/> is outside the range <c>0</c> to <c>3</c>.
    /// </exception>
    /// <remarks>If both indices are equal, the method returns without modifying the matrix.</remarks>
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

    /// <summary>
    /// Copies the contents of the matrix into a newly allocated array.
    /// </summary>
    /// <returns>
    /// A new <see cref="float"/> array of sixteen elements containing the matrix values
    /// in row-major order.
    /// </returns>
    public readonly float[] ToArray()
    {
        return MemoryMarshal.CreateReadOnlySpan(in Row1.X, 16).ToArray();
    }

    /// <summary>
    /// Copies the contents of the matrix into the given destination span, in row-major order.
    /// </summary>
    /// <param name="destination">The span that receives the sixteen matrix elements.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="destination"/> is shorter than sixteen elements.
    /// </exception>
    public readonly void CopyTo(Span<float> destination)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(destination.Length, 16, nameof(destination));

        ReadOnlySpan<float> values = MemoryMarshal.CreateReadOnlySpan(in Row1.X, 16);

        values.CopyTo(destination);
    }

    /// <summary>
    /// Adds two matrices together, element by element.
    /// </summary>
    /// <param name="left">The left-hand operand.</param>
    /// <param name="right">The right-hand operand.</param>
    /// <returns>The element-wise sum of <paramref name="left"/> and <paramref name="right"/>.</returns>
    public static Matrix Add(ref readonly Matrix left, ref readonly Matrix right)
    {
        var leftSpan = MemoryMarshal.CreateReadOnlySpan<float>(in left.Row1.X, 16);
        var rightSpan = MemoryMarshal.CreateReadOnlySpan<float>(in right.Row1.X, 16);
        Span<float> destination = stackalloc float[16];
        MathUtilities.ElementWiseAdd(leftSpan, rightSpan, destination);

        return MemoryMarshal.Cast<float, Matrix>(destination)[0];
    }

    /// <summary>
    /// Subtracts the right-hand matrix from the left-hand matrix, element by element.
    /// </summary>
    /// <param name="left">The matrix being subtracted from.</param>
    /// <param name="right">The matrix to subtract.</param>
    /// <returns>The element-wise difference of <paramref name="left"/> and <paramref name="right"/>.</returns>
    public static Matrix Subtract(ref readonly Matrix left, ref readonly Matrix right)
    {
        var leftSpan = MemoryMarshal.CreateReadOnlySpan<float>(in left.Row1.X, 16);
        var rightSpan = MemoryMarshal.CreateReadOnlySpan<float>(in right.Row1.X, 16);
        Span<float> destination = stackalloc float[16];
        MathUtilities.ElementWiseSubtract(leftSpan, rightSpan, destination);

        return MemoryMarshal.Cast<float, Matrix>(destination)[0];
    }

    /// <summary>
    /// Multiplies every element of a matrix by the given scalar value.
    /// </summary>
    /// <param name="matrix">The source matrix.</param>
    /// <param name="scale">The scalar multiplier.</param>
    /// <returns>The scaled matrix.</returns>
    public static Matrix Multiply(ref readonly Matrix matrix, float scale)
    {
        var matrixSpan = MemoryMarshal.CreateReadOnlySpan<float>(in matrix.Row1.X, 16);
        Span<float> destination = stackalloc float[16];
        MathUtilities.MultiplyElementsWithValue(matrixSpan, scale, destination);

        return MemoryMarshal.Cast<float, Matrix>(destination)[0];
    }
    /// <summary>
    /// Multiplies two matrices together.
    /// </summary>
    /// <param name="left">The left-hand operand.</param>
    /// <param name="right">The right-hand operand.</param>
    /// <returns>The product of <paramref name="left"/> and <paramref name="right"/>.</returns>
    /// <remarks>Matrix multiplication is not commutative; the order of the operands matters.</remarks>
    public static Matrix Multiply(ref readonly Matrix left, ref readonly Matrix right)
    {
        return (Matrix4x4)left * (Matrix4x4)right;
    }

    /// <summary>
    /// Divides every element of a matrix by the given scalar value.
    /// </summary>
    /// <param name="matrix">The source matrix.</param>
    /// <param name="scale">The scalar divisor.</param>
    /// <returns>The resulting matrix after division.</returns>
    public static Matrix Divide(ref readonly Matrix matrix, float scale)
    {
        float inv = 1.0f / scale;

        var matrixSpan = MemoryMarshal.CreateReadOnlySpan<float>(in matrix.Row1.X, 16);
        Span<float> destination = stackalloc float[16];
        MathUtilities.MultiplyElementsWithValue(matrixSpan, inv, destination);

        return MemoryMarshal.Cast<float, Matrix>(destination)[0];
    }

    /// <summary>
    /// Divides the left-hand matrix by the right-hand matrix, element by element.
    /// </summary>
    /// <param name="left">The dividend matrix.</param>
    /// <param name="right">The divisor matrix.</param>
    /// <returns>The element-wise quotient of <paramref name="left"/> and <paramref name="right"/>.</returns>
    public static Matrix Divide(ref readonly Matrix left, ref readonly Matrix right)
    {
        var leftSpan = MemoryMarshal.CreateReadOnlySpan<float>(in left.Row1.X, 16);
        var rightSpan = MemoryMarshal.CreateReadOnlySpan<float>(in right.Row1.X, 16);
        Span<float> destination = stackalloc float[16];
        MathUtilities.ElementWiseDivide(leftSpan, rightSpan, destination);

        return MemoryMarshal.Cast<float, Matrix>(destination)[0];
    }

    /// <summary>
    /// Raises a matrix to the specified non-negative integer power using
    /// exponentiation by squaring.
    /// </summary>
    /// <param name="value">The matrix to raise to a power.</param>
    /// <param name="exponent">The exponent; must be greater than or equal to <c>0</c>.</param>
    /// <returns>
    /// <paramref name="value"/> multiplied by itself <paramref name="exponent"/> times.
    /// An exponent of <c>0</c> yields <see cref="Identity"/> and an exponent of <c>1</c>
    /// yields <paramref name="value"/> unchanged.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="exponent"/> is negative.</exception>
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

    /// <summary>
    /// Returns a new matrix whose elements are the negation of the given matrix's elements.
    /// </summary>
    /// <param name="value">The matrix to negate.</param>
    /// <returns>The negated matrix.</returns>
    public static Matrix Negate(ref readonly Matrix value)
    {
        return Matrix4x4.Negate(value);
    }

    /// <summary>
    /// Performs a linear interpolation between two matrices based on the given weighting.
    /// </summary>
    /// <param name="start">The first matrix.</param>
    /// <param name="end">The second matrix.</param>
    /// <param name="amount">
    /// A value between <c>0</c> and <c>1</c> indicating the weight of <paramref name="end"/>.
    /// </param>
    /// <returns>The interpolated matrix.</returns>
    public static Matrix Lerp(ref readonly Matrix start, ref readonly Matrix end, float amount)
    {
        return Matrix4x4.Lerp(start, end, amount);
    }

    /// <summary>
    /// Performs a cubic Hermite smooth-step interpolation between two matrices,
    /// applied row by row.
    /// </summary>
    /// <param name="start">The matrix returned when <paramref name="amount"/> is <c>0</c>.</param>
    /// <param name="end">The matrix returned when <paramref name="amount"/> is <c>1</c>.</param>
    /// <param name="amount">The interpolation factor.</param>
    /// <returns>The interpolated matrix.</returns>
    /// <remarks>
    /// Unlike <see cref="Lerp"/>, the smooth-step curve has zero first derivative at
    /// both endpoints, producing ease-in/ease-out transitions.
    /// </remarks>
    public static Matrix SmoothStep(ref readonly Matrix start, ref readonly Matrix end, float amount)
    {
        Matrix result = new();

        result.Row1 = Vector4D.SmoothStep(start.Row1, end.Row1, amount);
        result.Row2 = Vector4D.SmoothStep(start.Row2, end.Row2, amount);
        result.Row3 = Vector4D.SmoothStep(start.Row3, end.Row3, amount);
        result.Row4 = Vector4D.SmoothStep(start.Row4, end.Row4, amount);
        return result;
    }

    /// <summary>
    /// Returns the transpose of the given matrix, produced by swapping its rows and columns.
    /// </summary>
    /// <param name="value">The matrix to transpose.</param>
    /// <returns>The transposed matrix.</returns>
    public static Matrix Transpose(ref readonly Matrix value)
    {
        return Matrix4x4.Transpose(value);
    }

    /// <summary>
    /// Returns an orthogonalized copy of the given matrix using the Gram-Schmidt
    /// process on its rows.
    /// </summary>
    /// <param name="value">The matrix to orthogonalize.</param>
    /// <returns>
    /// A matrix whose rows are mutually orthogonal; the rows are not normalized, so the
    /// result is generally not orthonormal.
    /// </returns>
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

    /// <summary>
    /// Returns an orthonormalized copy of the given matrix using the Gram-Schmidt
    /// process on its rows.
    /// </summary>
    /// <param name="value">The matrix to orthonormalize.</param>
    /// <returns>A matrix whose rows are mutually orthogonal and of unit length.</returns>
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
    /// <summary>
    /// Reduces the given matrix to upper-triangular form using Gaussian elimination
    /// with partial pivoting.
    /// </summary>
    /// <param name="value">The matrix to reduce.</param>
    /// <returns>
    /// An equivalent upper-triangular matrix; elements below the main diagonal are
    /// driven toward zero within <see cref="MathUtilities.ZeroTolerance"/>.
    /// </returns>
    /// <remarks>
    /// The elimination uses SIMD (hardware accelerated) arithmetic when available and
    /// falls back to scalar arithmetic otherwise.
    /// </remarks>
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

    /// <summary>
    /// Reduces the given matrix to lower-triangular form.
    /// </summary>
    /// <param name="value">The matrix to reduce.</param>
    /// <returns>An equivalent lower-triangular matrix.</returns>
    /// <remarks>
    /// Implemented by transposing the input, computing the upper-triangular form of the
    /// result and transposing back.
    /// </remarks>
    public static Matrix LowerTriangularForm(ref readonly Matrix value)
    {
        Matrix t = Transpose(in value);
        Matrix u = UpperTriangularForm(in t);
        return Transpose(in u);
    }

    /// <summary>
    /// Reduces the given matrix to row-echelon form using Gaussian elimination with
    /// partial pivoting.
    /// </summary>
    /// <param name="value">The matrix to reduce.</param>
    /// <returns>
    /// The row-echelon form of <paramref name="value"/>, in which each leading entry of
    /// a nonzero row is <c>1</c> and all entries below it are <c>0</c>.
    /// </returns>
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


    /// <summary>
    /// Creates a billboard transformation matrix that orients an object so that it
    /// always faces the camera.
    /// </summary>
    /// <param name="objectPosition">The position of the object being billboarded.</param>
    /// <param name="cameraPosition">The position of the camera.</param>
    /// <param name="cameraUpVector">The camera's up vector.</param>
    /// <param name="cameraForwardVector">
    /// The camera's forward vector, used as a fallback direction when the object and
    /// camera coincide.
    /// </param>
    /// <returns>The billboard transformation matrix.</returns>
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
    /// <summary>
    /// Creates a left-handed view matrix that positions a camera at <paramref name="eye"/>
    /// looking toward <paramref name="target"/>.
    /// </summary>
    /// <param name="eye">The position of the camera.</param>
    /// <param name="target">The point the camera looks at.</param>
    /// <param name="up">The up direction of the camera.</param>
    /// <returns>The left-handed view matrix.</returns>
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

    /// <summary>
    /// Creates a right-handed view matrix that positions a camera at <paramref name="eye"/>
    /// looking toward <paramref name="target"/>.
    /// </summary>
    /// <param name="eye">The position of the camera.</param>
    /// <param name="target">The point the camera looks at.</param>
    /// <param name="up">The up direction of the camera.</param>
    /// <returns>The right-handed view matrix.</returns>
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

    /// <summary>
    /// Creates a left-handed orthographic projection matrix centered on the view volume.
    /// </summary>
    /// <param name="width">The width of the view volume.</param>
    /// <param name="height">The height of the view volume.</param>
    /// <param name="znear">The minimum Z (near) clipping plane distance.</param>
    /// <param name="zfar">The maximum Z (far) clipping plane distance.</param>
    /// <returns>The left-handed orthographic projection matrix.</returns>
    public static Matrix OrthoLH(float width, float height, float znear, float zfar)
    {
        float halfWidth = width * 0.5f;
        float halfHeight = height * 0.5f;
        Matrix result;

        result = OrthoOffCenterLH(-halfWidth, halfWidth, -halfHeight, halfHeight, znear, zfar);

        return result;
    }

    /// <summary>
    /// Creates a right-handed orthographic projection matrix centered on the view volume.
    /// </summary>
    /// <param name="width">The width of the view volume.</param>
    /// <param name="height">The height of the view volume.</param>
    /// <param name="znear">The minimum Z (near) clipping plane distance.</param>
    /// <param name="zfar">The maximum Z (far) clipping plane distance.</param>
    /// <returns>The right-handed orthographic projection matrix.</returns>
    public static Matrix OrthoRH(float width, float height, float znear, float zfar)
    {
        float halfWidth = width * 0.5f;
        float halfHeight = height * 0.5f;

        Matrix result = OrthoOffCenterRH(-halfWidth, halfWidth, -halfHeight, halfHeight, znear, zfar);

        return result;

    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    /// <summary>
    /// Creates a customized left-handed orthographic projection matrix from explicit
    /// view-volume bounds.
    /// </summary>
    /// <param name="left">The minimum X coordinate of the view volume.</param>
    /// <param name="right">The maximum X coordinate of the view volume.</param>
    /// <param name="bottom">The minimum Y coordinate of the view volume.</param>
    /// <param name="top">The maximum Y coordinate of the view volume.</param>
    /// <param name="znear">The minimum Z (near) clipping plane distance.</param>
    /// <param name="zfar">The maximum Z (far) clipping plane distance.</param>
    /// <returns>The left-handed orthographic projection matrix.</returns>
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
    /// <summary>
    /// Creates a customized right-handed orthographic projection matrix from explicit
    /// view-volume bounds.
    /// </summary>
    /// <param name="left">The minimum X coordinate of the view volume.</param>
    /// <param name="right">The maximum X coordinate of the view volume.</param>
    /// <param name="bottom">The minimum Y coordinate of the view volume.</param>
    /// <param name="top">The maximum Y coordinate of the view volume.</param>
    /// <param name="znear">The minimum Z (near) clipping plane distance.</param>
    /// <param name="zfar">The maximum Z (far) clipping plane distance.</param>
    /// <returns>The right-handed orthographic projection matrix.</returns>
    /// <remarks>
    /// Built from <see cref="OrthoOffCenterLH"/> by negating the depth-axis values,
    /// which flips the Z axis for right-handed coordinates.
    /// </remarks>
    private static Matrix OrthoOffCenterRH(float left, float right, float bottom, float top, float znear, float zfar)
    {
        Matrix result = OrthoOffCenterLH(left, right, bottom, top, znear, zfar);

        result.Row3.Z *= -1.0f;

        return result;
    }

    /// <summary>
    /// Creates a left-handed perspective projection matrix with the given view volume.
    /// </summary>
    /// <param name="width">The width of the view volume at the near clipping plane.</param>
    /// <param name="height">The height of the view volume at the near clipping plane.</param>
    /// <param name="znear">The minimum Z (near) clipping plane distance.</param>
    /// <param name="zfar">The maximum Z (far) clipping plane distance.</param>
    /// <returns>The left-handed perspective projection matrix.</returns>
    public static Matrix PerspectiveLH(float width, float height, float znear, float zfar)
    {
        float halfWidth = width * 0.5f;
        float halfHeight = height * 0.5f;
        Matrix result;
        result = PerspectiveOffCenterLH(-halfWidth, halfWidth, -halfHeight, halfHeight, znear, zfar);

        return result;

    }

    /// <summary>
    /// Creates a right-handed perspective projection matrix with the given view volume.
    /// </summary>
    /// <param name="width">The width of the view volume at the near clipping plane.</param>
    /// <param name="height">The height of the view volume at the near clipping plane.</param>
    /// <param name="znear">The minimum Z (near) clipping plane distance.</param>
    /// <param name="zfar">The maximum Z (far) clipping plane distance.</param>
    /// <returns>The right-handed perspective projection matrix.</returns>
    public static Matrix PerspectiveRH(float width, float height, float znear, float zfar)
    {
        float halfWidth = width * 0.5f;
        float halfHeight = height * 0.5f;

        Matrix result = PerspectiveOffCenterRH(-halfWidth, halfWidth, -halfHeight, halfHeight, znear, zfar);

        return result;
    }

    /// <summary>
    /// Creates a left-handed perspective projection matrix based on a field of view.
    /// </summary>
    /// <param name="fov">The field of view, along the Y axis, in radians.</param>
    /// <param name="aspect">The aspect ratio, usually viewport width divided by viewport height.</param>
    /// <param name="znear">The minimum Z (near) clipping plane distance.</param>
    /// <param name="zfar">The maximum Z (far) clipping plane distance.</param>
    /// <returns>The left-handed perspective projection matrix.</returns>
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

    /// <summary>
    /// Creates a right-handed perspective projection matrix based on a field of view.
    /// </summary>
    /// <param name="fov">The field of view, along the Y axis, in radians.</param>
    /// <param name="aspect">The aspect ratio, usually viewport width divided by viewport height.</param>
    /// <param name="znear">The minimum Z (near) clipping plane distance.</param>
    /// <param name="zfar">The maximum Z (far) clipping plane distance.</param>
    /// <returns>The right-handed perspective projection matrix.</returns>
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

    /// <summary>
    /// Creates a customized left-handed perspective projection matrix from explicit
    /// view-volume bounds.
    /// </summary>
    /// <param name="left">The minimum X coordinate of the view volume.</param>
    /// <param name="right">The maximum X coordinate of the view volume.</param>
    /// <param name="bottom">The minimum Y coordinate of the view volume.</param>
    /// <param name="top">The maximum Y coordinate of the view volume.</param>
    /// <param name="znear">The minimum Z (near) clipping plane distance.</param>
    /// <param name="zfar">The maximum Z (far) clipping plane distance.</param>
    /// <returns>The left-handed perspective projection matrix.</returns>
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

    /// <summary>
    /// Creates a customized right-handed perspective projection matrix from explicit
    /// view-volume bounds.
    /// </summary>
    /// <param name="left">The minimum X coordinate of the view volume.</param>
    /// <param name="right">The maximum X coordinate of the view volume.</param>
    /// <param name="bottom">The minimum Y coordinate of the view volume.</param>
    /// <param name="top">The maximum Y coordinate of the view volume.</param>
    /// <param name="znear">The minimum Z (near) clipping plane distance.</param>
    /// <param name="zfar">The maximum Z (far) clipping plane distance.</param>
    /// <returns>The right-handed perspective projection matrix.</returns>
    /// <remarks>
    /// Built from <see cref="PerspectiveOffCenterLH"/> by negating the third row, which
    /// flips the depth axis for right-handed coordinates.
    /// </remarks>
    public static Matrix PerspectiveOffCenterRH(float left, float right, float bottom, float top, float znear, float zfar)
    {

        Matrix result;
        result = PerspectiveOffCenterLH(left, right, bottom, top, znear, zfar);
        result.Row3 = (Vector4)result.Row3 * -1.0f;

        return result;
    }

    /// <summary>
    /// Creates a matrix that reflects points across the specified plane.
    /// </summary>
    /// <param name="plane">The plane to reflect across.</param>
    /// <returns>The reflection matrix.</returns>
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

    /// <summary>
    /// Creates a matrix that projects (flattens) geometry onto the specified plane,
    /// producing a shadow relative to the given light source.
    /// </summary>
    /// <param name="light">
    /// The light source as a <see cref="Vector4D"/>; a directional light uses
    /// <c>W == 0</c>, while a positional light uses <c>W == 1</c>.
    /// </param>
    /// <param name="plane">The plane onto which the geometry is projected.</param>
    /// <returns>The shadow projection matrix.</returns>
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

    /// <summary>
    /// Creates a scaling matrix from the given scale vector.
    /// </summary>
    /// <param name="scale">The scale factors along the X, Y and Z axes.</param>
    /// <returns>The scaling matrix.</returns>
    public static Matrix Scaling(ref readonly Vector3D scale)
    {
        return Matrix4x4.CreateScale(scale);
    }
    /// <summary>
    /// Creates a scaling matrix from the given per-axis scale factors.
    /// </summary>
    /// <param name="x">The scale factor along the X axis.</param>
    /// <param name="y">The scale factor along the Y axis.</param>
    /// <param name="z">The scale factor along the Z axis.</param>
    /// <returns>The scaling matrix.</returns>
    public static Matrix Scaling(float x, float y, float z)
    {
        var vector = new Vector3D(x, y, z);
        return Scaling(in vector);   
    }

    /// <summary>
    /// Creates a uniform scaling matrix that scales equally along all three axes.
    /// </summary>
    /// <param name="scale">The scale factor applied to the X, Y and Z axes.</param>
    /// <returns>The uniform scaling matrix.</returns>
    public static Matrix Scaling(float scale)
    {
        var vector = new Vector3D(scale);
        return Scaling(in vector);
    }

    /// <summary>
    /// Creates a matrix that rotates about the X axis by the given angle.
    /// </summary>
    /// <param name="angle">The rotation angle, in radians.</param>
    /// <returns>The rotation matrix.</returns>
    public static Matrix RotationX(float angle)
    {
 return Matrix4x4.CreateRotationX(angle);
    }

    /// <summary>
    /// Creates a matrix that rotates about the Y axis by the given angle.
    /// </summary>
    /// <param name="angle">The rotation angle, in radians.</param>
    /// <returns>The rotation matrix.</returns>
    public static Matrix RotationY(float angle)
    {
        return Matrix4x4.CreateRotationY(angle);
    }

    /// <summary>
    /// Creates a matrix that rotates about the Z axis by the given angle.
    /// </summary>
    /// <param name="angle">The rotation angle, in radians.</param>
    /// <returns>The rotation matrix.</returns>
    public static Matrix RotationZ(float angle)
    {

        return Matrix4x4.CreateRotationZ(angle);
    }

    /// <summary>
    /// Creates a matrix that rotates about an arbitrary axis by the given angle.
    /// </summary>
    /// <param name="axis">The axis to rotate about; expected to be normalized.</param>
    /// <param name="angle">The rotation angle, in radians.</param>
    /// <returns>The rotation matrix.</returns>
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
    /// <summary>
    /// Creates a rotation matrix from the given quaternion.
    /// </summary>
    /// <param name="rotation">The quaternion describing the rotation; expected to be normalized.</param>
    /// <returns>The equivalent rotation matrix.</returns>
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

    /// <summary>
    /// Creates a transformation matrix that applies the given scaling, rotation and
    /// translation, in that conceptual order.
    /// </summary>
    /// <param name="scaling">The scale factors along the X, Y and Z axes.</param>
    /// <param name="rotation">The rotation quaternion; expected to be normalized.</param>
    /// <param name="translation">The translation offset.</param>
    /// <returns>The combined transformation matrix.</returns>
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
    /// <summary>
    /// Creates a rotation matrix from the given yaw, pitch and roll angles.
    /// </summary>
    /// <param name="yaw">The yaw angle, in radians, around the Y axis.</param>
    /// <param name="pitch">The pitch angle, in radians, around the X axis.</param>
    /// <param name="roll">The roll angle, in radians, around the Z axis.</param>
    /// <returns>The rotation matrix.</returns>
    public static Matrix RotationYawPitchRoll(float yaw, float pitch, float roll)
    {
        var quaternion = Quaternion.RotationYawPitchRoll(yaw, pitch, roll);
        return RotationQuaternion(in quaternion);
    }

    /// <summary>
    /// Creates a translation matrix from the given translation vector.
    /// </summary>
    /// <param name="value">The translation offsets along the X, Y and Z axes.</param>
    /// <returns>The translation matrix.</returns>
    public static Matrix Translation(ref readonly Vector3D value)
    {
        return Translation(value.X, value.Y, value.Z);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    /// <summary>
    /// Creates a translation matrix from the given per-axis translation offsets.
    /// </summary>
    /// <param name="x">The translation along the X axis.</param>
    /// <param name="y">The translation along the Y axis.</param>
    /// <param name="z">The translation along the Z axis.</param>
    /// <returns>The translation matrix.</returns>
    public static Matrix Translation(float x, float y, float z)
    {
        Matrix result;
        result = Identity;
        result.Row4 = new Vector4D(x, y, z, 0);
        return result;
    }

    /// <summary>
    /// Creates an affine transformation matrix composed of a uniform scale, a rotation
    /// and a translation.
    /// </summary>
    /// <param name="scaling">The uniform scale factor.</param>
    /// <param name="rotation">The rotation quaternion; expected to be normalized.</param>
    /// <param name="translation">The translation offset.</param>
    /// <returns>The affine transformation matrix.</returns>
    public static Matrix AffineTransformation(float scaling, ref readonly Quaternion rotation, ref readonly Vector3D translation)
    {
        return Scaling(scaling) * RotationQuaternion(in rotation) * Translation(in translation);
    }

    /// <summary>
    /// Creates an affine transformation matrix that rotates about a specified center
    /// point, then applies a uniform scale and a translation.
    /// </summary>
    /// <param name="scaling">The uniform scale factor.</param>
    /// <param name="rotationCenter">The point about which the rotation is applied.</param>
    /// <param name="rotation">The rotation quaternion; expected to be normalized.</param>
    /// <param name="translation">The translation offset.</param>
    /// <returns>The affine transformation matrix.</returns>
    public static Matrix AffineTransformation(float scaling, ref readonly Vector3D rotationCenter, ref readonly Quaternion rotation, ref readonly Vector3D translation)
    {
        var rotationCenterResult = -rotationCenter;
        return Scaling(scaling) * Translation(in rotationCenterResult) * RotationQuaternion(in rotation) *
            Translation(in rotationCenter) * Translation(in translation);
    }

    /// <summary>
    /// Creates a two-dimensional affine transformation matrix composed of a uniform
    /// scale, a rotation about the Z axis and a translation.
    /// </summary>
    /// <param name="scaling">The uniform scale factor applied to X and Y.</param>
    /// <param name="rotation">The rotation angle, in radians, around the Z axis.</param>
    /// <param name="translation">The two-dimensional translation offset.</param>
    /// <returns>The affine transformation matrix.</returns>
    public static Matrix AffineTransformation2D(float scaling, float rotation,ref readonly Vector2D translation)
    {
        var translation3d = (Vector3D)translation;
        return Scaling(scaling, scaling, 1.0f) * RotationZ(rotation) * Translation(in translation3d);
    }

    /// <summary>
    /// Creates a transformation matrix that combines centered scaling (optionally along
    /// rotated axes), centered rotation and translation.
    /// </summary>
    /// <param name="scalingCenter">The point about which the scaling is applied.</param>
    /// <param name="scalingRotation">
    /// The orientation of the scaling axes; expected to be normalized.
    /// </param>
    /// <param name="scaling">The scale factors along the X, Y and Z axes.</param>
    /// <param name="rotationCenter">The point about which the rotation is applied.</param>
    /// <param name="rotation">The rotation quaternion; expected to be normalized.</param>
    /// <param name="translation">The translation offset.</param>
    /// <returns>The combined transformation matrix.</returns>
    public static Matrix Transformation(ref readonly Vector3D scalingCenter, ref readonly Quaternion scalingRotation, ref readonly Vector3D scaling, ref readonly Vector3D rotationCenter, ref readonly Quaternion rotation, ref readonly Vector3D translation)
    {
        Matrix sr = RotationQuaternion(in scalingRotation);
        var negativeScalingCenter = -scalingCenter;
        var negativeRotationCenter = -rotationCenter;
        return Translation(in negativeScalingCenter) * Transpose(in sr) * Scaling(in scaling) * sr * Translation(in scalingCenter) * Translation(in negativeRotationCenter) *
            RotationQuaternion(in rotation) * Translation(in rotationCenter) * Translation(in translation);
    }
}

