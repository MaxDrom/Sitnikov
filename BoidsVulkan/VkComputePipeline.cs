using Silk.NET.Core.Native;
using Silk.NET.Vulkan;

namespace BoidsVulkan;

public class VkComputePipeline: IVkPipeline, IDisposable
{
    private VkContext _ctx;
    private VkDevice _device;
    private Pipeline _pipeline;
    private VkPiplineLayout _pipelineLayout;
    private bool disposedValue;

    public unsafe VkComputePipeline(VkContext ctx, VkDevice device, VkShaderInfo computeShader, IEnumerable<VkSetLayout> setLayouts)
    {
        _ctx = ctx;
        _device = device;
         _pipelineLayout = new VkPiplineLayout(ctx, device, setLayouts);
        var pname = SilkMarshal.StringToPtr(computeShader.EntryPoint);
        var stageInfo = new PipelineShaderStageCreateInfo()
        {
            SType = StructureType.PipelineShaderStageCreateInfo,
            Stage = ShaderStageFlags.ComputeBit,
            Module = computeShader.ShaderModule.ShaderModule,
            PName = (byte *)pname
        };

        if(computeShader.SpecializationInfo != null)
        {
            var spec = computeShader.SpecializationInfo!.Value;
            stageInfo.PSpecializationInfo = &spec;
        }

        var computeCreateInfo = new ComputePipelineCreateInfo()
        {
            SType = StructureType.ComputePipelineCreateInfo,
            Stage = stageInfo,
            Layout = _pipelineLayout.PipelineLayout
        };
        _ctx.Api.CreateComputePipelines(_device.Device, default, 1u, ref computeCreateInfo, null, out _pipeline);
        SilkMarshal.Free(pname);
    }

    public Pipeline InternalPipeline => _pipeline;

    public PipelineBindPoint BindPoint => PipelineBindPoint.Compute;

    public PipelineLayout PipelineLayout => _pipelineLayout.PipelineLayout;

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if(disposing)
            {
                _pipelineLayout.Dispose();
            }
            unsafe
            {
                _ctx.Api.DestroyPipeline(_device.Device, _pipeline, null);
            }
            disposedValue = true;
        }
    }

    ~VkComputePipeline()
    {
        Dispose(disposing: false);
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}