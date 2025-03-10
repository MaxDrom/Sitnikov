using Silk.NET.Vulkan;

namespace BoidsVulkan;

public class VkSemaphore : IDisposable
{
    public PipelineStageFlags Flag {get; set;}
    internal Silk.NET.Vulkan.Semaphore Semaphore => _semaphore;
    private readonly VkContext _ctx;
    private readonly VkDevice _device;
    private readonly Silk.NET.Vulkan.Semaphore _semaphore;

    private bool disposedValue;

    public VkSemaphore(VkContext ctx, VkDevice device)
    {
        _ctx = ctx;
        _device = device;
        SemaphoreCreateInfo createInfo = new()
        {
            SType = StructureType.SemaphoreCreateInfo,
        };

        unsafe
        {
            if (_ctx.Api.CreateSemaphore(_device.Device, ref createInfo, null, out _semaphore) != Result.Success)
                throw new Exception("Failed to create semaphore");
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            unsafe
            {
                _ctx.Api.DestroySemaphore(_device.Device, _semaphore, null);
            }
            disposedValue = true;
        }
    }

    ~VkSemaphore()
    {
        Dispose(disposing: false);
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}