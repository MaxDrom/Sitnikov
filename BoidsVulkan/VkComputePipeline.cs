using Silk.NET.Core.Native;
using Silk.NET.Vulkan;

namespace BoidsVulkan;

public class VkComputePipeline : IVkPipeline, IDisposable
{
    private readonly VkContext _ctx;
    private readonly VkDevice _device;
    private readonly Pipeline _pipeline;
    private readonly VkPiplineLayout _pipelineLayout;
    private bool _disposedValue;

    public unsafe VkComputePipeline(VkContext ctx,
        VkDevice device,
        VkShaderInfo computeShader,
        VkSetLayout[] setLayouts,
        PushConstantRange[] pushConstantRanges)
    {
        _ctx = ctx;
        _device = device;
        _pipelineLayout = new VkPiplineLayout(ctx, device, setLayouts,
            pushConstantRanges);
        var pname = SilkMarshal.StringToPtr(computeShader.EntryPoint);
        var stageInfo = new PipelineShaderStageCreateInfo
        {
            SType = StructureType.PipelineShaderStageCreateInfo,
            Stage = ShaderStageFlags.ComputeBit,
            Module = computeShader.ShaderModule.ShaderModule,
            PName = (byte*)pname
        };

        if (computeShader.SpecializationInfo != null)
        {
            var spec = computeShader.SpecializationInfo!.Value;
            stageInfo.PSpecializationInfo = &spec;
        }

        var computeCreateInfo = new ComputePipelineCreateInfo
        {
            SType = StructureType.ComputePipelineCreateInfo,
            Stage = stageInfo,
            Layout = _pipelineLayout.PipelineLayout
        };
        _ctx.Api.CreateComputePipelines(_device.Device, default, 1u,
            ref computeCreateInfo, null, out _pipeline);
        SilkMarshal.Free(pname);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public Pipeline InternalPipeline => _pipeline;

    public PipelineBindPoint BindPoint => PipelineBindPoint.Compute;

    public PipelineLayout PipelineLayout =>
        _pipelineLayout.PipelineLayout;

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing) _pipelineLayout.Dispose();

            unsafe
            {
                _ctx.Api.DestroyPipeline(_device.Device, _pipeline,
                    null);
            }

            _disposedValue = true;
        }
    }

    ~VkComputePipeline()
    {
        Dispose(false);
    }
}