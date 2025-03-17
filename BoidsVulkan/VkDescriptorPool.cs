using Silk.NET.Vulkan;

namespace BoidsVulkan;

public class VkDescriptorPool : IDisposable
{
    private readonly VkContext _ctx;
    private readonly DescriptorPool _descriptorPool;
    private readonly VkDevice _device;
    private bool _disposedValue;

    public unsafe VkDescriptorPool(VkContext ctx,
        VkDevice device,
        DescriptorPoolSize[] poolSizes,
        uint maxSets)
    {
        _ctx = ctx;
        _device = device;
        fixed (DescriptorPoolSize* ppoolSizes = poolSizes)
        {
            DescriptorPoolCreateInfo createInfo = new()
            {
                SType = StructureType.DescriptorPoolCreateInfo,
                PoolSizeCount = (uint)poolSizes.Length,
                PPoolSizes = ppoolSizes,
                MaxSets = maxSets
            };
            if (_ctx.Api.CreateDescriptorPool(_device.Device,
                    ref createInfo, null, out _descriptorPool) !=
                Result.Success)
                throw new Exception(
                    "Failed to create descriptor pool");
        }
    }

    internal DescriptorPool DescriptorPool => _descriptorPool;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public unsafe DescriptorSet[] AllocateDescriptors(
        VkSetLayout setLayout,
        int n)
    {
        var result = new DescriptorSet[n];
        fixed (DescriptorSet* presult = result)
        {
            var psetLayouts = stackalloc DescriptorSetLayout[n];
            for (var i = 0; i < n; i++)
                psetLayouts[i] = setLayout.SetLayout;

            var allocateInfo = new DescriptorSetAllocateInfo
            {
                SType = StructureType.DescriptorSetAllocateInfo,
                DescriptorSetCount = (uint)n,
                PSetLayouts = psetLayouts,
                DescriptorPool = _descriptorPool
            };
            _ctx.Api.AllocateDescriptorSets(_device.Device,
                ref allocateInfo, presult);
        }

        return result;
    }

    public unsafe DescriptorSet[] AllocateDescriptors(
        VkSetLayout[] setLayouts)
    {
        var result = new DescriptorSet[setLayouts.Length];
        fixed (DescriptorSet* presult = result)
        {
            var psetLayouts =
                stackalloc DescriptorSetLayout[setLayouts.Length];
            for (var i = 0; i < setLayouts.Length; i++)
                psetLayouts[i] = setLayouts[i].SetLayout;

            var allocateInfo = new DescriptorSetAllocateInfo
            {
                SType = StructureType.DescriptorSetAllocateInfo,
                DescriptorSetCount = (uint)setLayouts.Length,
                PSetLayouts = psetLayouts,
                DescriptorPool = _descriptorPool
            };
            _ctx.Api.AllocateDescriptorSets(_device.Device,
                ref allocateInfo, presult);
        }

        return result;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            unsafe
            {
                _ctx.Api.DestroyDescriptorPool(_device.Device,
                    _descriptorPool, null);
            }

            _disposedValue = true;
        }
    }

    ~VkDescriptorPool()
    {
        Dispose(false);
    }
}