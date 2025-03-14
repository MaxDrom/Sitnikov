using Silk.NET.Core.Native;
using Silk.NET.Vulkan;

namespace BoidsVulkan;

public class VkComputePipeline: IVkPipeline
{
    private VkContext _ctx;
    private VkDevice _device;
    private Pipeline _pipeline;
    public unsafe VkComputePipeline(VkContext ctx, VkDevice device, VkShaderInfo computeShader, IEnumerable<VkSetLayout> setLayouts)
    {
        _ctx = ctx;
        _device = device;
        using var setLayoutInfo = new VkPiplineLayout(ctx, device, setLayouts);
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
            Layout = setLayoutInfo.PipelineLayout
        };
        _ctx.Api.CreateComputePipelines(_device.Device, default, 1u, ref computeCreateInfo, null, out _pipeline);
        SilkMarshal.Free(pname);
    }

    public Pipeline InternalPipeline => _pipeline;

    public PipelineBindPoint BindPoint => PipelineBindPoint.Compute;
}