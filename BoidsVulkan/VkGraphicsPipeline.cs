using Silk.NET.Core.Native;
using Silk.NET.Vulkan;

namespace BoidsVulkan;

public class VkGraphicsPipeline : IDisposable, IVkPipeline
{
    public Pipeline InternalPipeline =>_pipline;

    public PipelineBindPoint BindPoint => PipelineBindPoint.Graphics;

    public PipelineLayout PipelineLayout => _pipelineLayout.PipelineLayout;

    private readonly VkContext _ctx;
    private readonly VkDevice _device;
    private readonly Pipeline _pipline;
    private VkPiplineLayout _pipelineLayout;
    private bool disposedValue;

    public unsafe VkGraphicsPipeline(VkContext ctx, VkDevice device,
        Dictionary<ShaderStageFlags, VkShaderInfo> stageInfos,
        IEnumerable<VkSetLayout> setLayouts,
        VkRenderPass renderPass,
        int subpassIndex,
        IEnumerable<DynamicState> dynamicStates,
        VkVertexInputStateCreateInfo vertexInputStateCreateInfo,
        Viewport viewport,
        Rect2D scissor,
        PipelineInputAssemblyStateCreateInfo inputAssemblyState,
        PipelineRasterizationStateCreateInfo* prasterizationSettings = null,
        PipelineMultisampleStateCreateInfo* pmultisamplingSettings = null,
        PipelineColorBlendStateCreateInfo* pcolorBlendSettings = null,
        PipelineDepthStencilStateCreateInfo* ppipelineDepthStencilSettings = null
    )
    {
        _ctx = ctx;
        _device = device;
        _pipelineLayout = new VkPiplineLayout(ctx, device, setLayouts);
        unsafe
        {
            var tmp = new List<PipelineShaderStageCreateInfo>();
            var handlers = new List<nint>();
            foreach ((var stageFlag, var shaderInfo) in stageInfos)
            {
                var pname = SilkMarshal.StringToPtr(shaderInfo.EntryPoint);
                var createStageInfo = new PipelineShaderStageCreateInfo()
                {
                    SType = StructureType.PipelineShaderStageCreateInfo,
                    Stage = stageFlag,
                    Module = shaderInfo.ShaderModule.ShaderModule,
                    PName = (byte*)pname
                };
                if (shaderInfo.SpecializationInfo != null)
                {
                    var specs = shaderInfo.SpecializationInfo!.Value;
                    createStageInfo.PSpecializationInfo = &specs;
                }

                tmp.Add(createStageInfo);
                handlers.Add(pname);
            }
            PipelineDynamicStateCreateInfo dynamicStateCreateInfo = new()
            {
                SType = StructureType.PipelineDynamicStateCreateInfo,
                DynamicStateCount = (uint)dynamicStates.Count()
            };

            fixed (DynamicState* pDynamicStates = dynamicStates.ToArray())
            {
                dynamicStateCreateInfo.PDynamicStates = pDynamicStates;

                fixed (PipelineShaderStageCreateInfo* stages = tmp.ToArray())
                {
                    var unmVertexInputStateCreateInfo = vertexInputStateCreateInfo.PipelineVertexInputStateCreateInfo;
                    PipelineRasterizationStateCreateInfo* prasterizationState = null;
                    PipelineViewportStateCreateInfo viewportInfo = new ()
                    {
                        SType = StructureType.PipelineViewportStateCreateInfo,
                        ViewportCount = 1,
                        ScissorCount = 1,
                        PViewports = &viewport,
                        PScissors = &scissor
                    };

                    GraphicsPipelineCreateInfo createInfo = new()
                    {
                        SType = StructureType.GraphicsPipelineCreateInfo,
                        StageCount = (uint)tmp.Count,
                        PStages = stages,
                        PVertexInputState = &unmVertexInputStateCreateInfo,
                        PRasterizationState = prasterizationSettings,
                        PMultisampleState = pmultisamplingSettings,
                        PColorBlendState = pcolorBlendSettings,
                        PDepthStencilState = ppipelineDepthStencilSettings,
                        PDynamicState = &dynamicStateCreateInfo,
                        RenderPass = renderPass.RenderPass,
                        Subpass = (uint)subpassIndex,
                        Layout = _pipelineLayout.PipelineLayout,
                        PInputAssemblyState = &inputAssemblyState,
                        PViewportState = &viewportInfo
                    };

                    if (_ctx.Api.CreateGraphicsPipelines(_device.Device, default, 1, in createInfo, null, out _pipline) != Result.Success)
                        throw new Exception("Failed to create graphics pipeline");
                }
            }
            foreach(var handler in handlers)
                SilkMarshal.Free(handler);
        }

    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if(disposing)
                _pipelineLayout.Dispose();
            unsafe
            {
                _ctx.Api.DestroyPipeline(_device.Device, _pipline, null);
            }
            disposedValue = true;
        }
    }

    ~VkGraphicsPipeline()
    {
        Dispose(disposing: false);
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}