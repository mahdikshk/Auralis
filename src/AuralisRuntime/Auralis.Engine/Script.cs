using System;
using System.Collections.Generic;
using System.Text;

namespace Auralis.Engine;

public abstract class Script : ComponentBase
{
    public abstract void Update(float deltaTime);
    
}
