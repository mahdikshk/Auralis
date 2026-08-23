using System;
using System.Collections.Generic;
using System.Text;

namespace Auralis.Engine;

public abstract class EntityComponent
{
    public Entity Entity { get; internal set; }

    public Guid Id { get; set; } = Guid.NewGuid();
}
