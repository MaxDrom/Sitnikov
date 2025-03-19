using Sitnikov.BoidsVulkan;

namespace Sitnikov;

public interface IParticleSystem : IDisposable
{
    VkBuffer<Instance> Buffer { get; }
    IEnumerable<Instance> DataOnCpu { get; }
    
    Task Update(double delta, double totalTime);
}