using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace Auralis.Core.Mathematics;

/// <summary>
/// Represents a two-dimensional integer point.
/// </summary>
public struct Point
{
    /// <summary>
    /// A static instance of the <see cref="T:Auralis.Core.Mathematics.Point"/> structure with its coordinates set to (0, 0).
    /// </summary>
    public static readonly Point Zero = new(0, 0);
    /// <summary>
    /// Initializes a new instance of the <see cref="T:Auralis.Core.Mathematics.Point"/> structure with the specified coordinates.
    /// </summary>
    public Point(int x, int y)
    {
        X = x;
        Y = y;
    }

    /// <summary>
    /// The X-coordinate of the point.
    /// </summary>
    public int X;
    /// <summary>
    /// The Y-coordinate of the point.
    /// </summary>
    public int Y;


    /// <summary>
    /// Defines an explicit conversion of a <see cref="T:Auralis.Core.Mathematics.Vector2D"/> into a <see cref="T:Auralis.Core.Mathematics.Point"/>, truncating the components to integers.
    /// </summary>
    public static explicit operator Point(Vector2D value)
    {
        return new Point((int)value.X, (int)value.Y);
    }

    /// <summary>
    /// Defines an implicit conversion of a <see cref="T:Auralis.Core.Mathematics.Point"/> into a <see cref="T:Auralis.Core.Mathematics.Vector2D"/>.
    /// </summary>
    public static implicit operator Vector2D(Point value)
    {
        return new Vector2D(value.X, value.Y);
    }
}
