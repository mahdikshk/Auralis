namespace Auralis.Engine.ECS;

/// <summary>A simulation unit executed by <see cref="SystemScheduler"/>.</summary>
public interface ISystem
{
    void Update(World world, float deltaTime);
}
