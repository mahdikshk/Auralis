using System;
using System.Collections.Generic;
using System.Text;

namespace Auralis.Core.Mathematics;

/// <summary>
/// Represents a two-dimensional single-precision floating-point size.
/// </summary>
public struct Size2DF
{
    /// <summary>
    /// A static instance of the <see cref="T:Auralis.Core.Mathematics.Size2DF"/> structure with its dimensions set to (0, 0).
    /// </summary>
    public static readonly Size2DF Zero = new(0, 0);
    /// <summary>
    /// A static instance of the <see cref="T:Auralis.Core.Mathematics.Size2DF"/> structure representing an empty size; equivalent to <see cref="F:Auralis.Core.Mathematics.Size2DF.Zero"/>.
    /// </summary>
    public static readonly Size2DF Empty = Zero;

    /// <summary>
    /// Initializes a new instance of the <see cref="T:Auralis.Core.Mathematics.Size2DF"/> structure with the specified dimensions.
    /// </summary>
    public Size2DF(float width, float height)
    {
        Width = width;
        Height = height;
    }

    /// <summary>
    /// The width component of the size.
    /// </summary>
    public float Width;
    /// <summary>
    /// The height component of the size.
    /// </summary>
    public float Height;
}
