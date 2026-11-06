namespace Auralis.Engine.ECS;

internal interface IComponentPool
{
    void Remove(EntityId entity);
    bool Contains(EntityId entity);
}
