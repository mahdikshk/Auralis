using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace Auralis.Core.Mathematics;

public struct Point
{
    public static readonly Point Zero = new(0, 0);
    public Point(int x, int y)
    {
        X = x;
        Y = y;
    }

    public int X;
    public int Y;


    public static explicit operator Point(Vector2D value)
    {
        return new Point((int)value.X, (int)value.Y);
    }

    public static implicit operator Vector2D(Point value)
    {
        return new Vector2D(value.X, value.Y);
    }
}
