using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace Auralis.Core.Mathematics;

/// <summary>
/// Represents a two-dimensional integer size.
/// </summary>
public struct Size2D
{
    /// <summary>
    /// A static instance of the <see cref="T:Auralis.Core.Mathematics.Size2D"/> structure with its dimensions set to (0, 0).
    /// </summary>
    public static readonly Size2D Zero = new(0, 0);
    /// <summary>
    /// A static instance of the <see cref="T:Auralis.Core.Mathematics.Size2D"/> structure representing an empty size; equivalent to <see cref="F:Auralis.Core.Mathematics.Size2D.Zero"/>.
    /// </summary>
    public static readonly Size2D Empty = Zero;

    /// <summary>
    /// Initializes a new instance of the <see cref="T:Auralis.Core.Mathematics.Size2D"/> structure with the specified dimensions.
    /// </summary>
    public Size2D(int width, int height)
    {
        Width = width;
        Height = height;
    }

    /// <summary>
    /// The width component of the size.
    /// </summary>
    public int Width;

    /// <summary>
    /// The height component of the size.
    /// </summary>
    public int Height;
}
