# Auralis ECS

The ECS lives in the `Auralis.Engine.ECS` namespace and is additive to the
legacy `Auralis.Engine.Entity` / `EntityComponent` API.

## Core concepts

- `World` owns entity lifetimes and component pools.
- `EntityId` is a value-type handle containing an index and generation. A stale
  handle cannot access a newly-created entity that reuses the same index.
- Components are plain data types (structs or classes); no base class is required.
- `SystemScheduler` executes `ISystem` implementations in deterministic order.
  Dependencies are expressed as system types passed to `Add`.

## Example

```csharp
using Auralis.Engine.ECS;

public struct Velocity
{
    public float X;
    public float Y;
}

public sealed class MovementSystem : ISystem
{
    public void Update(World world, float deltaTime)
    {
        foreach (var (entity, position, velocity) in world.Query<Position, Velocity>())
        {
            position.X += velocity.X * deltaTime;
            position.Y += velocity.Y * deltaTime;
            world.SetComponent(entity, position);
        }
    }
}

var world = new World();
var player = world.CreateEntity();
world.AddComponent(player, new Position());
world.AddComponent(player, new Velocity { X = 2, Y = 0 });

var systems = new SystemScheduler();
systems.Add(new MovementSystem());
systems.Update(world, 1f);
```

## Current implementation notes

This first implementation favors a small, predictable API and correctness over
premature optimization. Component pools currently use dictionaries and queries
scan live entity slots. A later performance pass can replace the storage backend
with sparse sets or archetype/chunk storage without changing the entity handle
or world-facing API.

A `World` is not thread-safe. Keep structural changes and system execution on
the engine thread unless the caller provides synchronization.
