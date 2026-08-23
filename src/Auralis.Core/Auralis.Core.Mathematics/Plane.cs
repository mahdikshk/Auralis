using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;

namespace Auralis.Core.Mathematics;

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct Plane
{
    public Vector3D Normal;
    public float D;


    public Plane(float value)
    {
        Normal.X = Normal.Y = Normal.Z = D = value;
    }

    public Plane(float a, float b, float c, float d)
    {
        Normal.X = a;
        Normal.Y = b;
        Normal.Z = c;
        D = d;
    }

    public Plane(Vector3D point, Vector3D normal)
    {
        Normal = normal;
        D = Vector3D.Dot(normal, point);
    }

    public Plane(Vector3D value, float d)
    {
        Normal = value;
        D = d;
    }

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
    public static implicit operator System.Numerics.Plane(Plane value)
    {
        return new System.Numerics.Plane(value.Normal, value.D);
    }

    public static implicit operator Plane(System.Numerics.Plane value)
    {
        return new Plane(value.Normal, value.D);
    }
    public Plane(ReadOnlySpan<float> values)
    {
        if (values.Length != 4)
            throw new ArgumentOutOfRangeException(nameof(values), "There must be four and only four input values for Plane.");

        Normal.X = values[0];
        Normal.Y = values[1];
        Normal.Z = values[2];
        D = values[3];
    }

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

    public void Negate()
    {
        Normal.X = -Normal.X;
        Normal.Y = -Normal.Y;
        Normal.Z = -Normal.Z;
        D = -D;
    }

    public void Normalize()
    {
        float magnitude = 1.0f / MathF.Sqrt((Normal.X * Normal.X) + (Normal.Y * Normal.Y) + (Normal.Z * Normal.Z));

        Normal.X *= magnitude;
        Normal.Y *= magnitude;
        Normal.Z *= magnitude;
        D *= magnitude;
    }

    public float[] ToArray()
    {
        return [Normal.X, Normal.Y, Normal.Z, D];
    }


}
