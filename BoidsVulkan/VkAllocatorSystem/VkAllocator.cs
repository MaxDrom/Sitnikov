using BoidsVulkan;
using Silk.NET.Vulkan;

namespace VkAllocatorSystem;

public class AllocationNode(DeviceMemory deviceMemory, ulong offset)
{
    public DeviceMemory Memory { get; init; } = deviceMemory;

    public ulong Offset { get; init; } = offset;
    internal AllocationNode Next { get; set; }
}

public interface IVkAllocatorFactory
{
    VkAllocator Create(VkContext ctx,
        VkDevice device,
        MemoryPropertyFlags requiredProperties,
        MemoryHeapFlags preferredFlags);
}

public abstract class VkAllocator : IDisposable
{
    private bool _disposedValue;
    protected MemoryHeapFlags PreferredFlags;

    protected MemoryPropertyFlags RequiredProperties;

    public VkAllocator(VkContext ctx,
        VkDevice device,
        MemoryPropertyFlags requiredProperties,
        MemoryHeapFlags preferredFlags)
    {
        Ctx = ctx;
        Device = device;
        RequiredProperties = requiredProperties;
        PreferredFlags = preferredFlags;
    }

    public VkContext Ctx { get; }

    public VkDevice Device { get; }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public abstract AllocationNode Allocate(
        MemoryRequirements requirements);

    public abstract void Deallocate(AllocationNode node);
    public abstract void Free();

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            Free();
            _disposedValue = true;
        }
    }

    ~VkAllocator()
    {
        Dispose(false);
    }
}

public interface IVkSingleTypeAllocator
{
    VkContext Ctx { get; }
    VkDevice Device { get; }

    bool TryAllocate(ulong size,
        ulong alignment,
        out AllocationNode node);

    void Deallocate(AllocationNode node);
}