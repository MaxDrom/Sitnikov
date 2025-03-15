using Silk.NET.Vulkan;

namespace BoidsVulkan;

public class VkDescriptorPool : IDisposable
{
    internal DescriptorPool DescriptorPool => _descriptorPool;
    private DescriptorPool _descriptorPool;
    private VkContext _ctx;
    private VkDevice _device;
    private bool disposedValue;

    public unsafe VkDescriptorPool(VkContext ctx, VkDevice device, DescriptorPoolSize[] poolSizes, uint maxSets)
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
            if (_ctx.Api.CreateDescriptorPool(_device.Device, ref createInfo, null, out _descriptorPool) != Result.Success)
                throw new Exception("Failed to create descriptor pool");
        }
    }

    public unsafe DescriptorSet[] AllocateDescriptors(VkSetLayout setLayout, int n)
    {
        var result = new DescriptorSet[n];
        fixed (DescriptorSet* presult = result)
        {
            var psetLayouts = stackalloc DescriptorSetLayout[n];
            for (var i = 0; i < n; i++)
                psetLayouts[i] = setLayout.SetLayout;

            var allocateInfo = new DescriptorSetAllocateInfo()
            {
                SType = StructureType.DescriptorSetAllocateInfo,
                DescriptorSetCount = (uint)n,
                PSetLayouts = psetLayouts,
                DescriptorPool = _descriptorPool
            };
            _ctx.Api.AllocateDescriptorSets(_device.Device, ref allocateInfo, presult);
        }
        return result;
    }

    public unsafe DescriptorSet[] AllocateDescriptors(VkSetLayout[] setLayouts)
    {
        var result = new DescriptorSet[setLayouts.Length];
        fixed (DescriptorSet* presult = result)
        {
            var psetLayouts = stackalloc DescriptorSetLayout[setLayouts.Length];
            for (var i = 0; i < setLayouts.Length; i++)
                psetLayouts[i] = setLayouts[i].SetLayout;

            var allocateInfo = new DescriptorSetAllocateInfo()
            {
                SType = StructureType.DescriptorSetAllocateInfo,
                DescriptorSetCount = (uint)setLayouts.Length,
                PSetLayouts = psetLayouts,
                DescriptorPool = _descriptorPool
            };
            _ctx.Api.AllocateDescriptorSets(_device.Device, ref allocateInfo, presult);
        }
        return result;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            unsafe
            {
                _ctx.Api.DestroyDescriptorPool(_device.Device, _descriptorPool, null);
            }
            disposedValue = true;
        }
    }

    ~VkDescriptorPool()
    {
        Dispose(disposing: false);
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}