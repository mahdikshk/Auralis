using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace Auralis.Core;

public static partial class MemoryUtilities
{
    private static void ThrowAlignmentIsNotPowerOfTwo(this int alignment,
        [CallerArgumentExpression(nameof(alignment))] string? parameterName = null)
    {
        throw new ArgumentException("The alignment must be a positive power of 2.", parameterName);
    }
}

