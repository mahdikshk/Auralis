using System;
using System.Collections.Generic;
using System.Drawing;
using System.Numerics;
using System.Text;

namespace Auralis.Core.Mathematics;

/// <summary>
/// Stores the position and dimensions of an axis-aligned rectangle in two-dimensional integer space.
/// </summary>
public struct Rectangle
{
    /// <summary>
    /// A static instance of the <see cref="T:Auralis.Core.Mathematics.Rectangle"/> structure representing an empty rectangle at (0, 0) with zero width and height.
    /// </summary>
    public static readonly Rectangle Empty = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="T:Auralis.Core.Mathematics.Rectangle"/> structure with all components set to their default values.
    /// </summary>
    public Rectangle()
    {

    }

    /// <summary>
    /// Initializes a new instance of the <see cref="T:Auralis.Core.Mathematics.Rectangle"/> structure with the specified position and size.
    /// </summary>
    public Rectangle(int x, int y, int width, int height)
    {
        this.X = x;
        this.Y = y;
        this.Width = width;
        this.Height = height;
    }

    /// <summary>
    /// Gets or sets the X-coordinate of the left edge of the rectangle.
    /// </summary>
    public int Left
    {
        readonly get { return X; }
        set { X = value; }
    }
    /// <summary>
    /// Gets or sets the Y-coordinate of the top edge of the rectangle.
    /// </summary>
    public int Top
    {
        readonly get { return Y; }
        set { Y = value; }
    }

    /// <summary>
    /// Gets the X-coordinate of the right edge of the rectangle.
    /// </summary>
    public readonly int Right
    {
        get { return X + Width; }
    }

    /// <summary>
    /// Gets the Y-coordinate of the bottom edge of the rectangle.
    /// </summary>
    public readonly int Bottom
    {
        get { return Y + Height; }
    }

    /// <summary>
    /// The X-coordinate of the top-left corner of the rectangle.
    /// </summary>
    public int X;

    /// <summary>
    /// The Y-coordinate of the top-left corner of the rectangle.
    /// </summary>
    public int Y;

    /// <summary>
    /// The width of the rectangle.
    /// </summary>
    public int Width;

    /// <summary>
    /// The height of the rectangle.
    /// </summary>
    public int Height;

    /// <summary>
    /// Gets or sets the top-left corner of the rectangle as a <see cref="T:Auralis.Core.Mathematics.Point"/>.
    /// </summary>
    public Point Location
    {
        readonly get
        {
            return new Point(X, Y);
        }
        set
        {
            X = value.X;
            Y = value.Y;
        }
    }
    /// <summary>
    /// Gets the center point of the rectangle.
    /// </summary>
    public readonly Point Center
    {
        get
        {
            return new Point(X + (Width / 2), Y + (Height / 2));
        }
    }

    /// <summary>
    /// Gets a value indicating whether the rectangle is empty, i.e. all of its components are zero.
    /// </summary>
    public readonly bool IsEmpty
    {
        get
        {
            return (Width == 0) && (Height == 0) && (X == 0) && (Y == 0);
        }
    }

    /// <summary>
    /// Gets or sets the size of the rectangle as a <see cref="T:Auralis.Core.Mathematics.Size2D"/>.
    /// </summary>
    public Size2D Size
    {
        readonly get
        {
            return new Size2D(Width, Height);
        }
        set
        {
            Width = value.Width;
            Height = value.Height;
        }
    }

    /// <summary>
    /// Gets the top-left corner of the rectangle.
    /// </summary>
    public readonly Point TopLeft { get { return new Point(Left, Top); } }

    /// <summary>
    /// Gets the top-right corner of the rectangle.
    /// </summary>
    public readonly Point TopRight { get { return new Point(Right, Top); } }

    /// <summary>
    /// Gets the bottom-left corner of the rectangle.
    /// </summary>
    public readonly Point BottomLeft { get { return new Point(Left, Bottom); } }

    /// <summary>
    /// Gets the bottom-right corner of the rectangle.
    /// </summary>
    public readonly Point BottomRight { get { return new Point(Right, Bottom); } }


    /// <summary>
    /// Moves the rectangle by the amounts specified in the given <see cref="T:Auralis.Core.Mathematics.Point"/>.
    /// </summary>
    public void Offset(Point amount)
    {
        Offset(amount.X, amount.Y);
    }

    /// <summary>
    /// Moves the rectangle by the specified horizontal and vertical amounts.
    /// </summary>
    public void Offset(int offsetX, int offsetY)
    {
        X += offsetX;
        Y += offsetY;
    }

    /// <summary>
    /// Expands the rectangle by the specified horizontal and vertical amounts, growing it evenly in all directions.
    /// </summary>
    public void Inflate(int horizontalAmount, int verticalAmount)
    {
        X -= horizontalAmount;
        Y -= verticalAmount;
        Width += horizontalAmount * 2;
        Height += verticalAmount * 2;
    }

    /// <summary>
    /// Checks whether the rectangle contains the point specified by the given coordinates.
    /// </summary>
    public readonly bool Contains(int x, int y)
    {
        return (X <= x) && (x < Right) && (Y <= y) && (y < Bottom);
    }

    /// <summary>
    /// Checks whether the rectangle contains the specified point.
    /// </summary>
    public readonly bool Contains(Point value)
    {
        return (X <= value.X) && (value.X < Right) && (Y <= value.Y) && (value.Y < Bottom);
    }

    /// <summary>
    /// Checks whether the rectangle entirely contains the specified rectangle.
    /// </summary>
    public readonly bool Contains(Rectangle value)
    {
        return (X <= value.X) && (value.Right <= Right) && (Y <= value.Y) && (value.Bottom <= Bottom);
    }

    /// <summary>
    /// Checks whether the rectangle contains the point specified by the given single-precision coordinates.
    /// </summary>
    public readonly bool Contains(float x, float y)
    {
        return x >= X && x <= Right && y >= Y && y <= Bottom;
    }

    /// <summary>
    /// Checks whether the rectangle contains the specified <see cref="T:Auralis.Core.Mathematics.Vector2D"/>.
    /// </summary>
    public readonly bool Contains(Vector2D vector2D)
    {
        return Contains(vector2D.X, vector2D.Y);
    }

    /// <summary>
    /// Checks whether the rectangle intersects with the specified rectangle.
    /// </summary>
    public readonly bool Intersects(Rectangle value)
    {
        return (value.X < Right) && (X < value.Right) && (value.Y < Bottom) && (Y < value.Bottom);
    }

    /// <summary>
    /// Returns the intersection of two rectangles.
    /// </summary>
    public static Rectangle Intersect(Rectangle value1, Rectangle value2)
    {
        int newLeft = (value1.X > value2.X) ? value1.X : value2.X;
        int newTop = (value1.Y > value2.Y) ? value1.Y : value2.Y;
        int newRight = (value1.Right < value2.Right) ? value1.Right : value2.Right;
        int newBottom = (value1.Bottom < value2.Bottom) ? value1.Bottom : value2.Bottom;
        Rectangle result;
        if ((newRight > newLeft) && (newBottom > newTop))
        {
            result = new Rectangle(newLeft, newTop, newRight - newLeft, newBottom - newTop);
        }
        else
        {
            result = Empty;
        }
        return result;
    }

    /// <summary>
    /// Returns the union of a rectangle and a point, treating the point as a 1x1 rectangle.
    /// </summary>
    public static Rectangle Union(Rectangle rectangle, Point point)
    {
        var rect = new Rectangle(point.X, point.Y, 1, 1);
        Rectangle result = Union(rectangle, rect);
        return result;
    }


    /// <summary>
    /// Returns the smallest rectangle that contains both of the specified rectangles.
    /// </summary>
    public static Rectangle Union(Rectangle value1, Rectangle value2)
    {
        var left = Math.Min(value1.Left, value2.Left);
        var right = Math.Max(value1.Right, value2.Right);
        var top = Math.Min(value1.Top, value2.Top);
        var bottom = Math.Max(value1.Bottom, value2.Bottom);
        return new Rectangle(left, top, right - left, bottom - top);
    }
}
