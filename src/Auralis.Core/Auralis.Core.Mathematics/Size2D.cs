using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace Auralis.Core.Mathematics;

public struct Size2D
{
    public static readonly Size2D Zero = new(0, 0);
    public static readonly Size2D Empty = Zero;

    public Size2D(int width, int height)
    {
        Width = width;
        Height = height;
    }

    public int Width;

    public int Height;
}
