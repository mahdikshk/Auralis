using System;
using System.Collections.Generic;
using System.Text;

namespace Auralis.Core.Mathematics;

public struct Size2DF
{
    public static readonly Size2DF Zero = new(0, 0);
    public static readonly Size2DF Empty = Zero;

    public Size2DF(float width, float height)
    {
        Width = width;
        Height = height;
    }

    public float Width;
    public float Height;
}
