using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;

namespace Auralis.Core.Mathematics;

/// <summary>
/// Represents a plane in three-dimensional space, defined by a normal vector and a signed distance from the origin.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct Plane
{
    /// <summary>
    /// The normal vector of the plane.
    /// </summary>
    public Vector3D Normal;
    /// <summary>
    /// The signed distance from the origin along the <see cref="F:Auralis.Core.Mathematics.Plane.Normal"/> component.
    /// </summary>
    public float D;


    /// <summary>
    /// Initializes a new instance of the <see cref="T:Auralis.Core.Mathematics.Plane"/> structure whose normal components and distance are all set to the same value.
    /// </summary>
    public Plane(float value)
    {
        Normal.X = Normal.Y = Normal.Z = D = value;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="T:Auralis.Core.Mathematics.Plane"/> structure with the equation ax + by + cz + d = 0 using the given coefficients.
    /// </summary>
    public Plane(float a, float b, float c, float d)
    {
        Normal.X = a;
        Normal.Y = b;
        Normal.Z = c;
        D = d;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="T:Auralis.Core.Mathematics.Plane"/> structure from a point on the plane and its normal; the distance is the dot product of the normal and the point.
    /// </summary>
    public Plane(Vector3D point, Vector3D normal)
    {
        Normal = normal;
        D = -Vector3D.Dot(normal, point);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="T:Auralis.Core.Mathematics.Plane"/> structure with the specified normal and distance from the origin.
    /// </summary>
    public Plane(Vector3D value, float d)
    {
        Normal = value;
        D = d;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="T:Auralis.Core.Mathematics.Plane"/> structure from three coplanar points, computing the normalized normal via the cross product and the distance from the first point.
    /// </summary>
    public Plane(Vector3D point1, Vector3D point2, Vector3D point3)
    {
        float x1 = point2.X - point1.X;
        float y1 = point2.Y - point1.Y;
        float z1 = point2.Z - point1.Z;
        float x2 = point3.X - point1.X;
        float y2 = point3.Y - point1.Y;
        float z2 = point3.Z - point1.Z;
        float yz = (y1 * z2) - (z1 * y2);
        float xz = (z1 * x2) - (x1 * z2);
        float xy = (x1 * y2) - (y1 * x2);
        float invPyth = 1.0f / MathF.Sqrt((yz * yz) + (xz * xz) + (xy * xy));

        Normal.X = yz * invPyth;
        Normal.Y = xz * invPyth;
        Normal.Z = xy * invPyth;
        D = -((Normal.X * point1.X) + (Normal.Y * point1.Y) + (Normal.Z * point1.Z));
    }
    /// <summary>
    /// Defines an implicit conversion of a <see cref="T:Auralis.Core.Mathematics.Plane"/> into a <see cref="T:System.Numerics.Plane"/>.
    /// </summary>
    public static implicit operator System.Numerics.Plane(Plane value)
    {
        return new System.Numerics.Plane(value.Normal, value.D);
    }

    /// <summary>
    /// Defines an implicit conversion of a <see cref="T:System.Numerics.Plane"/> into a <see cref="T:Auralis.Core.Mathematics.Plane"/>.
    /// </summary>
    public static implicit operator Plane(System.Numerics.Plane value)
    {
        return new Plane(value.Normal, value.D);
    }
    /// <summary>
    /// Initializes a new instance of the <see cref="T:Auralis.Core.Mathematics.Plane"/> structure from a read-only span of four values (normal X, Y, Z and the distance).
    /// </summary>
    public Plane(ReadOnlySpan<float> values)
    {
        if (values.Length != 4)
            throw new ArgumentOutOfRangeException(nameof(values), "There must be four and only four input values for Plane.");

        Normal.X = values[0];
        Normal.Y = values[1];
        Normal.Z = values[2];
        D = values[3];
    }

    /// <summary>
    /// Gets or sets the component at the specified index. Indices for a plane run from 0 to 3, mapping to Normal.X, Normal.Y, Normal.Z and D respectively.
    /// </summary>
    public float this[int index]
    {
        get
        {
            switch (index)
            {
                case 0: return Normal.X;
                case 1: return Normal.Y;
                case 2: return Normal.Z;
                case 3: return D;
            }

            throw new ArgumentOutOfRangeException(nameof(index), "Indices for Plane run from 0 to 3, inclusive.");
        }

        set
        {
            switch (index)
            {
                case 0: Normal.X = value; break;
                case 1: Normal.Y = value; break;
                case 2: Normal.Z = value; break;
                case 3: D = value; break;
                default: throw new ArgumentOutOfRangeException(nameof(index), "Indices for Plane run from 0 to 3, inclusive.");
            }
        }
    }

    /// <summary>
    /// Negates the plane, flipping the side of the plane that the normal points to.
    /// </summary>
    public void Negate()
    {
        Normal.X = -Normal.X;
        Normal.Y = -Normal.Y;
        Normal.Z = -Normal.Z;
        D = -D;
    }

    /// <summary>
    /// Normalizes the plane in place so that its normal becomes a unit vector.
    /// </summary>
    public void Normalize()
    {
        float magnitude = 1.0f / MathF.Sqrt((Normal.X * Normal.X) + (Normal.Y * Normal.Y) + (Normal.Z * Normal.Z));

        Normal.X *= magnitude;
        Normal.Y *= magnitude;
        Normal.Z *= magnitude;
        D *= magnitude;
    }

    /// <summary>
    /// Returns the plane components (Normal.X, Normal.Y, Normal.Z, D) as a new array.
    /// </summary>
    public float[] ToArray()
    {
        return [Normal.X, Normal.Y, Normal.Z, D];
    }


}
