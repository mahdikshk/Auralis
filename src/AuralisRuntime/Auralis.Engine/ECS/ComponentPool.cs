namespace Auralis.Engine.ECS;

/// <summary>
/// Type-erased storage contract used by <see cref="World"/>.
/// </summary>
internal sealed class ComponentPool<T> : IComponentPool where T : notnull
{
    private readonly Dictionary<EntityId, T> _components = new();

    public int Count => _components.Count;
    public bool Contains(EntityId entity) => _components.ContainsKey(entity);
    public void Remove(EntityId entity) => _components.Remove(entity);

    public void Add(EntityId entity, T component)
    {
        ArgumentNullException.ThrowIfNull(component);
        if (!_components.TryAdd(entity, component))
            throw new InvalidOperationException($"Entity {entity} already has component {typeof(T).Name}.");
    }

    public void Set(EntityId entity, T component)
    {
        ArgumentNullException.ThrowIfNull(component);
        _components[entity] = component;
    }

    public T Get(EntityId entity) =>
        _components.TryGetValue(entity, out var component)
            ? component
            : throw new KeyNotFoundException($"Entity {entity} does not have component {typeof(T).Name}.");

    public bool TryGet(EntityId entity, out T component) =>
        _components.TryGetValue(entity, out component!);
}
