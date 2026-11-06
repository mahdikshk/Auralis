namespace Auralis.Engine.ECS;

/// <summary>
/// Stable entity handle. The generation prevents stale handles from becoming valid
/// when an entity index is reused.
/// </summary>
public readonly record struct EntityId(int Index, uint Generation)
{
    public static EntityId Invalid => new(-1, 0);
    public bool IsValid => Index >= 0 && Generation != 0;
    public override string ToString() => IsValid ? $"Entity({Index}:{Generation})" : "Entity(Invalid)";
}
