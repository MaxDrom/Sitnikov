using BoidsVulkan;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using VkAllocatorSystem;
namespace SymplecticIntegrators;
public interface IParticleSystem : IDisposable
{
    Task Update(double delta, double totalTime);
    VkBuffer<Instance> Buffer { get; }
}

public interface IParticleSystemFactory
{
    IParticleSystem Create(VkContext ctx, VkDevice device, VkCommandPool commandPool, IVkAllocator allocator, IVkAllocator staggingAllocator,
                           Instance[] initialData);
}