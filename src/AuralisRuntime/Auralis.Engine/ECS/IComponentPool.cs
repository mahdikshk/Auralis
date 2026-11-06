namespace Auralis.Engine.ECS;

internal interface IComponentPool
{
    bool Remove(EntityId entity);
    bool Contains(EntityId entity);
}
