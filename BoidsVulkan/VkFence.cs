using Silk.NET.Vulkan;

namespace BoidsVulkan;

public class VkFence : IDisposable
{
    public Fence InternalFence => _fence;
    public static VkFence NullHandle => new(new Fence(), true);
    private readonly VkContext _ctx;
    private readonly VkDevice _device;
    private readonly Fence _fence;
    private bool disposedValue;

    public VkFence(VkContext ctx, VkDevice device)
    {
        _ctx = ctx;
        _device = device;
        FenceCreateInfo createInfo = new()
        {
            SType = StructureType.FenceCreateInfo,
            Flags = FenceCreateFlags.SignaledBit
        };

        unsafe
        {
            if (_ctx.Api.CreateFence(_device.Device, ref createInfo, null, out _fence) != Result.Success)
                throw new Exception("Failed to create fence");
        }
    }

    private VkFence(Fence fence, bool disposed)
    {
        _fence = fence;
        disposedValue = disposed;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            unsafe
            {
                _ctx.Api.DestroyFence(_device.Device, _fence, null);
            }
            disposedValue = true;
        }
    }
    public void Reset()
    {
        _ctx.Api.ResetFences(_device.Device, 1, in _fence);
    }
    
    public async Task WaitFor(ulong timeout = ulong.MaxValue)
    {
        await Task.Run(()=>_ctx.Api.WaitForFences(_device.Device, 1, in _fence, true, timeout));
        //_ctx.Api.ResetFences(_device.Device, 1, in _fence);
    }

    ~VkFence()
    {
        Dispose(disposing: false);
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}