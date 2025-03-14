using System.Runtime.InteropServices;
using BoidsVulkan;
using Silk.NET.Vulkan;

namespace VkAllocatorSystem;

public abstract class AllocationStrategy(VkContext ctx, VkDevice device, MemoryType memoryType)
{
    protected VkContext _ctx = ctx;
    protected VkDevice _device = device;
    protected MemoryType _memoryType = memoryType;

    public abstract bool TryAllocate(ulong size, ulong alignment, out AllocationNode allocationNode);
    public abstract bool TryDeallocate(AllocationNode allocationNode);
}

public interface IAllocationStrategyFactory
{
    AllocationStrategy Create(VkContext ctx, VkDevice device, MemoryType memoryType);
}
