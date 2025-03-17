using Silk.NET.Vulkan;

namespace Sitnikov.BoidsVulkan;

public class VkCommandPool : IDisposable
{
    private readonly List<VkCommandBuffer> _allocatedBuffers = new();

    private readonly CommandPool _cmdPool;
    private readonly VkContext _ctx;
    private readonly VkDevice _device;
    private bool _disposedValue;

    public VkCommandPool(VkContext ctx,
        VkDevice device,
        CommandPoolCreateFlags flags,
        uint queueFamilyIndex)
    {
        _ctx = ctx;
        _device = device;

        CommandPoolCreateInfo info = new()
        {
            SType = StructureType.CommandPoolCreateInfo,
            Flags = flags,
            QueueFamilyIndex = queueFamilyIndex,
        };

        unsafe
        {
            if (_ctx.Api.CreateCommandPool(_device.Device, ref info,
                    null, out _cmdPool) != Result.Success)
                throw new Exception("Failed to create command pool");
        }
    }

    public CommandPool CmdPool => _cmdPool;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public unsafe VkCommandBuffer[] AllocateBuffers(
        CommandBufferLevel level,
        int n)
    {
        var result = new VkCommandBuffer[n];
        var tmp = stackalloc CommandBuffer[n];

        CommandBufferAllocateInfo info = new()
        {
            SType = StructureType.CommandBufferAllocateInfo,
            CommandPool = _cmdPool,
            Level = level,
            CommandBufferCount = (uint)n,
        };

        _ctx.Api.AllocateCommandBuffers(_device.Device, &info, tmp);

        for (var i = 0; i < n; i++)
        {
            result[i] = new VkCommandBuffer(_ctx, _device,
                this, tmp[i]);
            _allocatedBuffers.Add(result[i]);
        }

        return result;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            unsafe
            {
                fixed (CommandBuffer* pbuffers = _allocatedBuffers
                           .Select(z => z.Buffer).ToArray())
                {
                    _ctx.Api.FreeCommandBuffers(_device.Device,
                        _cmdPool, (uint)_allocatedBuffers.Count,
                        pbuffers);
                }

                _ctx.Api.DestroyCommandPool(_device.Device, _cmdPool,
                    null);
            }

            _disposedValue = true;
        }
    }

    ~VkCommandPool()
    {
        Dispose(false);
    }
}