using BoidsVulkan;
using Silk.NET.Vulkan;

namespace VkAllocatorSystem;
using Buffer = Silk.NET.Vulkan.Buffer;

public class AllocationNode(DeviceMemory deviceMemory, ulong offset)
{
    public DeviceMemory Memory {get; init;} = deviceMemory;
    public ulong Offset {get; init;} = offset;
    internal AllocationNode Next {get; set;}
}

public class AllocationPool
{

}
public abstract class IVkAllocator : IDisposable
{
    private bool disposedValue;

    public abstract VkContext Ctx {get;}
    public abstract VkDevice Device {get;}
    public abstract AllocationNode Allocate(MemoryRequirements requirements);
    public abstract void Deallocate(AllocationNode node);
    public abstract void Free();

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