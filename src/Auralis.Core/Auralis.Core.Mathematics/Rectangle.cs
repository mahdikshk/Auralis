using System;
using System.Collections.Generic;
using System.Drawing;
using System.Numerics;
using System.Text;

namespace Auralis.Core.Mathematics;

public struct Rectangle
{
    public static readonly Rectangle Empty = new();

    public Rectangle()
    {

    }

    public Rectangle(int x, int y, int width, int height)
    {
        this.X = x;
        this.Y = y;
        this.Width = width;
        this.Height = height;
    }

    public int Left
    {
        readonly get { return X; }
        set { X = value; }
    }
    public int Top
    {
        readonly get { return Y; }
        set { Y = value; }
    }

    public readonly int Right
    {
        get { return X + Width; }
    }

    public readonly int Bottom
    {
        get { return Y + Height; }
    }

    public int X;

    public int Y;

    public int Width;

    public int Height;

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
    public readonly Point Center
    {
        get
        {
            return new Point(X + (Width / 2), Y + (Height / 2));
        }
    }

    public readonly bool IsEmpty
    {
        get
        {
            return (Width == 0) && (Height == 0) && (X == 0) && (Y == 0);
        }
    }

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

    public readonly Point TopLeft { get { return new Point(Left, Top); } }

    public readonly Point TopRight { get { return new Point(Right, Top); } }

    public readonly Point BottomLeft { get { return new Point(Left, Bottom); } }

    public readonly Point BottomRight { get { return new Point(Right, Bottom); } }


    public void Offset(Point amount)
    {
        Offset(amount.X, amount.Y);
    }

    public void Offset(int offsetX, int offsetY)
    {
        X += offsetX;
        Y += offsetY;
    }

    public void Inflate(int horizontalAmount, int verticalAmount)
    {
        X -= horizontalAmount;
        Y -= verticalAmount;
        Width += horizontalAmount * 2;
        Height += verticalAmount * 2;
    }

    public readonly bool Contains(int x, int y)
    {
        return (X <= x) && (x < Right) && (Y <= y) && (y < Bottom);
    }

    public readonly bool Contains(Point value)
    {
        return (X <= value.X) && (value.X < Right) && (Y <= value.Y) && (value.Y < Bottom);
    }

    public readonly bool Contains(Rectangle value)
    {
        return (X <= value.X) && (value.Right <= Right) && (Y <= value.Y) && (value.Bottom <= Bottom);
    }

    public readonly bool Contains(float x, float y)
    {
        return x >= X && x <= Right && y >= Y && y <= Bottom;
    }

    public readonly bool Contains(Vector2D vector2D)
    {
        return Contains(vector2D.X, vector2D.Y);
    }

    public readonly bool Intersects(Rectangle value)
    {
        return (value.X < Right) && (X < value.Right) && (value.Y < Bottom) && (Y < value.Bottom);
    }

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

    public static Rectangle Union(Rectangle rectangle, Point point)
    {
        var rect = new Rectangle(point.X, point.Y, 1, 1);
        Rectangle result = Union(rectangle, rect);
        return result;
    }


    public static Rectangle Union(Rectangle value1, Rectangle value2)
    {
        var left = Math.Min(value1.Left, value2.Left);
        var right = Math.Max(value1.Right, value2.Right);
        var top = Math.Min(value1.Top, value2.Top);
        var bottom = Math.Max(value1.Bottom, value2.Bottom);
        return new Rectangle(left, top, right - left, bottom - top);
    }
}
