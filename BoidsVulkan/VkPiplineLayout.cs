using Silk.NET.Vulkan;
namespace BoidsVulkan;

public class VkPiplineLayout : IDisposable
{
    public PipelineLayout PipelineLayout => _pipelineLayout;
    private readonly VkContext _ctx;
    private readonly VkDevice _device;
    private readonly PipelineLayout _pipelineLayout;
    private bool disposedValue;

    public VkPiplineLayout(VkContext ctx, VkDevice device, VkSetLayout[] setLayouts, PushConstantRange[] pushConstantRanges)
    {
        var setLayoutsArray = setLayouts;
        _ctx = ctx;
        _device = device;
        unsafe
        {
            DescriptorSetLayout* psetLayouts = stackalloc DescriptorSetLayout[setLayouts.Length];
            for (var i = 0; i < setLayouts.Length; i++)
                psetLayouts[i] = setLayouts[i].SetLayout;

            PushConstantRange* pPushConstantRanges = stackalloc PushConstantRange[pushConstantRanges.Length];
            for(var i = 0; i<pushConstantRanges.Length; i++)
                pPushConstantRanges[i] = pushConstantRanges[i];
            
            PipelineLayoutCreateInfo createInfo = new()
            {
                SType = StructureType.PipelineLayoutCreateInfo,
                PushConstantRangeCount = (uint)pushConstantRanges.Length,
                PPushConstantRanges = pPushConstantRanges,
                SetLayoutCount = (uint)setLayoutsArray.Length,
                PSetLayouts = psetLayouts
            };

            if (_ctx.Api.CreatePipelineLayout(_device.Device, ref createInfo,
                null, out _pipelineLayout) != Result.Success)
                throw new Exception("Failed to create pipeline layout");

        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            unsafe
            {
                _ctx.Api.DestroyPipelineLayout(_device.Device, _pipelineLayout, null);
            }
            disposedValue = true;
        }
    }

    ~VkPiplineLayout()
    {
        Dispose(disposing: false);
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}