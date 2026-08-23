using Auralis.Engine.Compoents;
using System;
using System.Collections.Generic;
using System.Text;

namespace Auralis.Engine;

public class Entity : ComponentBase
{
    public TransformComponent Transform { get; set; } = new TransformComponent();
    public string? Description { get; set; }
    private readonly Dictionary<Guid, EntityComponent> _components;
    public Entity()
    {
        Id = Guid.NewGuid();
        _components = [];
    }
    public void Destroy(Entity entity)
    {
        entity.OnDestroy();
    }
}
