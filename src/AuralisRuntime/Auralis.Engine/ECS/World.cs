namespace Auralis.Engine.ECS;

/// <summary>
/// Owns entity lifetimes and component storage for one simulation world.
/// A World is not thread-safe; mutate it from the engine thread or synchronize externally.
/// </summary>
public sealed class World
{
    private sealed class EntitySlot
    {
        public uint Generation = 1;
        public bool IsAlive;
    }

    private readonly List<EntitySlot> _slots = new();
    private readonly Stack<int> _freeIndices = new();
    private readonly Dictionary<Type, IComponentPool> _pools = new();

    public int EntityCount { get; private set; }

    public EntityId CreateEntity()
    {
        int index;
        EntitySlot slot;

        if (_freeIndices.TryPop(out index))
        {
            slot = _slots[index];
            slot.IsAlive = true;
        }
        else
        {
            index = _slots.Count;
            slot = new EntitySlot { IsAlive = true };
            _slots.Add(slot);
        }

        EntityCount++;
        return new EntityId(index, slot.Generation);
    }

    public bool IsAlive(EntityId entity) =>
        entity.Index >= 0 &&
        entity.Index < _slots.Count &&
        _slots[entity.Index].IsAlive &&
        _slots[entity.Index].Generation == entity.Generation;

    public bool DestroyEntity(EntityId entity)
    {
        if (!IsAlive(entity))
            return false;

        foreach (var pool in _pools.Values)
            pool.Remove(entity);

        var slot = _slots[entity.Index];
        slot.IsAlive = false;
        slot.Generation = slot.Generation == uint.MaxValue ? 1 : slot.Generation + 1;
        _freeIndices.Push(entity.Index);
        EntityCount--;
        return true;
    }

    public void AddComponent<T>(EntityId entity, T component) where T : notnull
    {
        EnsureAlive(entity);
        GetOrCreatePool<T>().Add(entity, component);
    }

    /// <summary>Adds a component or replaces the existing value.</summary>
    public void SetComponent<T>(EntityId entity, T component) where T : notnull
    {
        EnsureAlive(entity);
        GetOrCreatePool<T>().Set(entity, component);
    }

    public T GetComponent<T>(EntityId entity) where T : notnull
    {
        EnsureAlive(entity);
        return GetPool<T>().Get(entity);
    }

    public bool TryGetComponent<T>(EntityId entity, out T component) where T : notnull
    {
        if (!IsAlive(entity) || !_pools.TryGetValue(typeof(T), out var pool))
        {
            component = default!;
            return false;
        }

        return ((ComponentPool<T>)pool).TryGet(entity, out component);
    }

    public bool HasComponent<T>(EntityId entity) where T : notnull =>
        IsAlive(entity) &&
        _pools.TryGetValue(typeof(T), out var pool) &&
        pool.Contains(entity);

    public bool RemoveComponent<T>(EntityId entity) where T : notnull
    {
        EnsureAlive(entity);
        return _pools.TryGetValue(typeof(T), out var pool) && pool.RemoveAndReport(entity);
    }

    /// <summary>Enumerates live entities containing every requested component.</summary>
    public IEnumerable<EntityId> Query<T1>() where T1 : notnull
    {
        for (var i = 0; i < _slots.Count; i++)
        {
            var slot = _slots[i];
            if (slot.IsAlive)
            {
                var entity = new EntityId(i, slot.Generation);
                if (HasComponent<T1>(entity))
                    yield return entity;
            }
        }
    }

    public IEnumerable<(EntityId Entity, T1 First, T2 Second)> Query<T1, T2>()
        where T1 : notnull where T2 : notnull
    {
        for (var i = 0; i < _slots.Count; i++)
        {
            var slot = _slots[i];
            if (!slot.IsAlive) continue;

            var entity = new EntityId(i, slot.Generation);
            if (TryGetComponent<T1>(entity, out var first) &&
                TryGetComponent<T2>(entity, out var second))
                yield return (entity, first, second);
        }
    }

    private ComponentPool<T> GetOrCreatePool<T>() where T : notnull
    {
        if (!_pools.TryGetValue(typeof(T), out var pool))
        {
            pool = new ComponentPool<T>();
            _pools.Add(typeof(T), pool);
        }

        return (ComponentPool<T>)pool;
    }

    private ComponentPool<T> GetPool<T>() where T : notnull =>
        _pools.TryGetValue(typeof(T), out var pool)
            ? (ComponentPool<T>)pool
            : throw new KeyNotFoundException($"No component pool registered for {typeof(T).Name}.");

    private void EnsureAlive(EntityId entity)
    {
        if (!IsAlive(entity))
            throw new InvalidOperationException($"Entity handle {entity} is invalid or has been destroyed.");
    }
}
