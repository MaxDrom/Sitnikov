using Sitnikov.BoidsVulkan;
using Sitnikov.BoidsVulkan.VkAllocatorSystem;

namespace Sitnikov;

public interface IParticleSystem : IDisposable
{
    VkBuffer<Instance> Buffer { get; }
    Task Update(double delta, double totalTime);
}

public interface IParticleSystemFactory
{
    IParticleSystem Create(VkContext ctx,
        VkDevice device,
        VkCommandPool commandPool,
        VkAllocator allocator,
        VkAllocator staggingAllocator,
        Instance[] initialData);
}