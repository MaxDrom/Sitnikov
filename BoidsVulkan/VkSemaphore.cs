using Silk.NET.Vulkan;
using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace BoidsVulkan;

public class VkSemaphore : IDisposable
{
    private readonly VkContext _ctx;
    private readonly VkDevice _device;
    private readonly Semaphore _semaphore;

    private bool _disposedValue;

    public VkSemaphore(VkContext ctx, VkDevice device)
    {
        _ctx = ctx;
        _device = device;
        SemaphoreCreateInfo createInfo = new()
        {
            SType = StructureType.SemaphoreCreateInfo
        };

        unsafe
        {
            if (_ctx.Api.CreateSemaphore(_device.Device,
                    ref createInfo, null, out _semaphore) !=
                Result.Success)
                throw new Exception("Failed to create semaphore");
        }
    }

    public PipelineStageFlags Flag { get; set; }

    internal Semaphore Semaphore => _semaphore;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            unsafe
            {
                _ctx.Api.DestroySemaphore(_device.Device, _semaphore,
                    null);
            }

            _disposedValue = true;
        }
    }

    ~VkSemaphore()
    {
        Dispose(false);
    }
}