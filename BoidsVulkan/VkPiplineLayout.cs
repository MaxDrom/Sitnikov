using Silk.NET.Vulkan;
namespace BoidsVulkan;

public class VkPiplineLayout : IDisposable
{
    public PipelineLayout PipelineLayout => _pipelineLayout;
    private readonly VkContext _ctx;
    private readonly VkDevice _device;
    private readonly PipelineLayout _pipelineLayout;
    private bool disposedValue;

    public VkPiplineLayout(VkContext ctx, VkDevice device, IEnumerable<VkSetLayout> setLayouts)
    {
        var setLayoutsArray = setLayouts.Select(z => z.SetLayout).ToArray();
        _ctx = ctx;
        _device = device;
        unsafe
        {
            PipelineLayoutCreateInfo createInfo = new()
            {
                SType = StructureType.PipelineLayoutCreateInfo,
                PushConstantRangeCount = 0,
                PPushConstantRanges = null,
                SetLayoutCount = (uint)setLayoutsArray.Length
            };

            fixed (DescriptorSetLayout* pSetLayouts = setLayoutsArray)
            {
                createInfo.PSetLayouts = pSetLayouts;

                if (_ctx.Api.CreatePipelineLayout(_device.Device, ref createInfo,
                    null, out _pipelineLayout) != Result.Success)
                    throw new Exception("Failed to create pipeline layout");
            }
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