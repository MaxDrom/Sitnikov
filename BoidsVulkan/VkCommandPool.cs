using Silk.NET.Vulkan;

namespace BoidsVulkan;

public class VkCommandPool : IDisposable
{

    private List<VkCommandBuffer> _allocatedBuffers = new List<VkCommandBuffer>();
    public CommandPool CmdPool => _cmdPool;
    private readonly VkContext _ctx;
    private readonly VkDevice _device;
    private readonly CommandPool _cmdPool;
    private bool disposedValue;

    public VkCommandPool(VkContext ctx,
                        VkDevice device,
                        CommandPoolCreateFlags flags,
                        uint queueFamilyIndex
                        )
    {
        _ctx = ctx;
        _device = device;

        CommandPoolCreateInfo info = new()
        {
            SType = StructureType.CommandPoolCreateInfo,
            Flags = flags,
            QueueFamilyIndex = queueFamilyIndex
        };

        unsafe
        {
            if (_ctx.Api.CreateCommandPool(_device.Device, ref info, null, out _cmdPool) != Result.Success)
                throw new Exception("Failed to create command pool");
        }
    }

    public unsafe VkCommandBuffer[] AllocateBuffers(CommandBufferLevel level, int n)
    {
        var result = new VkCommandBuffer[n];
        var tmp = stackalloc CommandBuffer[n];

        CommandBufferAllocateInfo info = new()
        {
            SType = StructureType.CommandBufferAllocateInfo,
            CommandPool = _cmdPool,
            Level = level,
            CommandBufferCount = (uint)n
        };

        _ctx.Api.AllocateCommandBuffers(_device.Device, &info, tmp);

        for(var i = 0; i<n; i++)
        {
            result[i] = new VkCommandBuffer(_ctx, _device, this ,tmp[i]);
            _allocatedBuffers.Add(result[i]);
        }

        return result;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            unsafe
            {
                fixed (CommandBuffer* pbuffers = _allocatedBuffers.Select(z => z.InternalBuffer).ToArray())
                {
                    _ctx.Api.FreeCommandBuffers(_device.Device, _cmdPool, (uint)_allocatedBuffers.Count, pbuffers);
                }
                _ctx.Api.DestroyCommandPool(_device.Device, _cmdPool, null);
            }
            disposedValue = true;
        }
    }

    ~VkCommandPool()
    {
        Dispose(disposing: false);
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}