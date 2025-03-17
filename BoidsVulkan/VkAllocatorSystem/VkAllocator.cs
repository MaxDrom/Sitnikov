using BoidsVulkan;
using Silk.NET.Vulkan;

namespace VkAllocatorSystem;

public class AllocationNode(DeviceMemory deviceMemory, ulong offset)
{
    public DeviceMemory Memory {get; init;} = deviceMemory;
    public ulong Offset {get; init;} = offset;
    internal AllocationNode Next {get; set;}
}

public interface IVkAllocatorFactory
{
    IVkAllocator Create(VkContext ctx, VkDevice device, MemoryPropertyFlags requiredProperties, MemoryHeapFlags preferredFlags);
}

public abstract class IVkAllocator : IDisposable
{
    private bool disposedValue;

    protected MemoryPropertyFlags requiredProperties;
    protected MemoryHeapFlags preferredFlags;
    private VkContext _ctx;
    private VkDevice _device;

    public VkContext Ctx => _ctx;
    public VkDevice Device => _device;
    public abstract AllocationNode Allocate(MemoryRequirements requirements);
    public abstract void Deallocate(AllocationNode node);
    public abstract void Free();

    public IVkAllocator(VkContext ctx, VkDevice device, MemoryPropertyFlags requiredProperties, MemoryHeapFlags preferredFlags)
    {
        _ctx = ctx;
        _device = device;
        this.requiredProperties = requiredProperties;
        this.preferredFlags = preferredFlags;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            Free();
            disposedValue = true;
        }
    }

    ~IVkAllocator()
    {
        Dispose(disposing: false);
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}

public interface IVkSingleTypeAllocator
{
    VkContext Ctx {get;}
    VkDevice Device {get;}
    bool TryAllocate(ulong size, ulong alignment, out AllocationNode node);
    void Deallocate(AllocationNode node);
}