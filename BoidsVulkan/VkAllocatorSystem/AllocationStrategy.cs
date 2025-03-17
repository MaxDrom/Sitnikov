using Silk.NET.Vulkan;

namespace Sitnikov.BoidsVulkan.VkAllocatorSystem;

public abstract class AllocationStrategy(
    VkContext ctx,
    VkDevice device,
    MemoryType memoryType
)
{
    protected VkContext Ctx = ctx;
    protected VkDevice Device = device;
    protected MemoryType MemoryType = memoryType;

    public abstract bool TryAllocate(ulong size,
        ulong alignment,
        out AllocationNode allocationNode);

    public abstract bool TryDeallocate(AllocationNode allocationNode);
}

public interface IAllocationStrategyFactory
{
    AllocationStrategy Create(VkContext ctx,
        VkDevice device,
        MemoryType memoryType);
}