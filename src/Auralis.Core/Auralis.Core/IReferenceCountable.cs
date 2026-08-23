using System;
using System.Collections.Generic;
using System.Text;

namespace Auralis.Core;

public interface IReferenceCountable
{
    int ReferenceCount { get; }
    int AddReference();
    int ReleaseReference();
}
