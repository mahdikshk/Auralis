namespace Auralis.Engine.ECS;

/// <summary>
/// Runs systems in deterministic dependency order. A system's dependencies are
/// the system types that must complete before it runs.
/// </summary>
public sealed class SystemScheduler
{
    private sealed record Entry(ISystem System, Type[] Dependencies);

    private readonly List<Entry> _entries = new();
    private IReadOnlyList<ISystem>? _orderedSystems;

    public void Add(ISystem system, params Type[] runsAfter)
    {
        ArgumentNullException.ThrowIfNull(system);
        ArgumentNullException.ThrowIfNull(runsAfter);

        var type = system.GetType();
        if (_entries.Any(entry => entry.System.GetType() == type))
            throw new InvalidOperationException($"System {type.Name} is already registered.");

        if (runsAfter.Any(dependency => dependency is null || !typeof(ISystem).IsAssignableFrom(dependency)))
            throw new ArgumentException("Dependencies must be ISystem types.", nameof(runsAfter));

        _entries.Add(new Entry(system, runsAfter.Distinct().ToArray()));
        _orderedSystems = null;
    }

    public bool Remove<TSystem>() where TSystem : ISystem
    {
        var removed = _entries.RemoveAll(entry => entry.System is TSystem) > 0;
        if (removed) _orderedSystems = null;
        return removed;
    }

    public void Update(World world, float deltaTime)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (!float.IsFinite(deltaTime) || deltaTime < 0)
            throw new ArgumentOutOfRangeException(nameof(deltaTime), "Delta time must be finite and non-negative.");

        foreach (var system in _orderedSystems ??= BuildExecutionOrder())
            system.Update(world, deltaTime);
    }

    private IReadOnlyList<ISystem> BuildExecutionOrder()
    {
        var byType = _entries.ToDictionary(entry => entry.System.GetType());
        var temporary = new HashSet<Type>();
        var permanent = new HashSet<Type>();
        var ordered = new List<ISystem>(_entries.Count);

        void Visit(Type type)
        {
            if (permanent.Contains(type)) return;
            if (!temporary.Add(type))
                throw new InvalidOperationException($"System dependency cycle detected at {type.Name}.");

            var entry = byType[type];
            foreach (var dependency in entry.Dependencies)
            {
                if (!byType.ContainsKey(dependency))
                    throw new InvalidOperationException($"System {type.Name} depends on unregistered system {dependency.Name}.");
                Visit(dependency);
            }

            temporary.Remove(type);
            permanent.Add(type);
            ordered.Add(entry.System);
        }

        // Registration order is the stable tie-breaker for independent systems.
        foreach (var entry in _entries)
            Visit(entry.System.GetType());

        return ordered;
    }
}
