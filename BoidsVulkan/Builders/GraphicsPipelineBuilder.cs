using System.Runtime.CompilerServices;
using Silk.NET.Vulkan;

namespace BoidsVulkan;

public sealed class GraphicsPipelineBuider
{

    private List<VkSetLayout> _setLayouts;
    private Dictionary<ShaderStageFlags, VkShaderInfo> _shaderInfos = new();
    private bool _logicOpEnable;
    private PipelineColorBlendAttachmentState[] _colorBlends;
    private List<VertexInputBindingDescription> _vertexInputBindingDescriptions = null;
    private List<VertexInputAttributeDescription> _vertexInputAttributeDescriptions = null;
    private List<DynamicState> _dynamicStates = [];
    private VkRenderPass _renderPass;
    private Viewport _viewport;
    private Rect2D _scissor;

    private PipelineInputAssemblyStateCreateInfo _inputAssemblyState;

    private bool _isRasterization = false;
    private PipelineRasterizationStateCreateInfo _rasterizationSettings;
    private uint? _sampleMask = null;
    private bool _isMultisampling = false;
    private PipelineMultisampleStateCreateInfo _multisamplingSettings;

    private bool _isDepthStencil = false;
    private PipelineDepthStencilStateCreateInfo _pipelineDepthStencilSettings;


    public class GraphicsPipelineBuilderOptional(GraphicsPipelineBuider scope)
    {
        GraphicsPipelineBuider _scope = scope;
        public GraphicsPipelineBuilderOptional WithDynamicStages(List<DynamicState> dynamicStates)
        {
            _scope._dynamicStates = dynamicStates;
            return this;
        }

        public GraphicsPipelineBuilderOptional WithFixedFunctions(Action<FixedFunctionBuilder> f)
        {
            f(new FixedFunctionBuilder(_scope));
            return this;
        }

        public GraphicsPipelineBuiderInputAssembly WithVertexInput(Action<AttributeAggregator> f)
        {
            _scope._vertexInputBindingDescriptions = [];
            _scope._vertexInputAttributeDescriptions = [];
            f(new AttributeAggregator(_scope));
            return new GraphicsPipelineBuiderInputAssembly(_scope);
        }
    }




    public GraphicsPipelineBuilderOptional ForRenderPass(VkRenderPass renderPass)
    {
        _renderPass = renderPass;

        return new GraphicsPipelineBuilderOptional(this);
    }


    public class AttributeAggregator(GraphicsPipelineBuider scope)
    {
        GraphicsPipelineBuider _scope = scope;

        public AttributeAggregator AddBindingFor<T>(int binding, VertexInputRate inputRate)
            where T : unmanaged, IVertexData<T>
        {
            var (attributeDescr, bindingDescr) = default(T).CreateVertexInputDescription(binding, inputRate);
            _scope._vertexInputBindingDescriptions.Add(bindingDescr);
            _scope._vertexInputAttributeDescriptions.AddRange(attributeDescr);
            return this;
        }
    }

    public class GraphicsPipelineBuiderInputAssembly(GraphicsPipelineBuider scope)
    {
        GraphicsPipelineBuider _scope = scope;

        public GraphicsPipelineBuiderViewport WithInputAssembly(PrimitiveTopology topology, bool primitiveRestartEnable = false)
        {

            var inputAssemblyState = new PipelineInputAssemblyStateCreateInfo()
            {
                SType = StructureType.PipelineInputAssemblyStateCreateInfo,
                Topology = topology,
                PrimitiveRestartEnable = primitiveRestartEnable
            };
            _scope._inputAssemblyState = inputAssemblyState;
            return new GraphicsPipelineBuiderViewport(_scope);
        }
    }

    public class GraphicsPipelineBuiderViewport(GraphicsPipelineBuider scope)
    {
        GraphicsPipelineBuider _scope = scope;

        public GraphicsPipelineBuiderStages WithViewportAndScissor(Viewport viewport, Rect2D scissor)
        {
            _scope._viewport = viewport;
            _scope._scissor = scissor;
            return new GraphicsPipelineBuiderStages(_scope);
        }
    }

    public class GraphicsPipelineBuiderStages(GraphicsPipelineBuider scope)
    {
        GraphicsPipelineBuider _scope = scope;

        public GraphicsPipelineBuiderLayout WithPipelineStages(Action<StageBuilder> f)
        {
            var aggregator = new StageBuilder(_scope);
            f(aggregator);
            return new GraphicsPipelineBuiderLayout(_scope);
        }
    }

    public class GraphicsPipelineBuiderLayout(GraphicsPipelineBuider scope)
    {
        GraphicsPipelineBuider _scope = scope;

        public GraphicsPipelineBuiderFinal WithLayout(IEnumerable<VkSetLayout> setLayouts)
        {
            _scope._setLayouts = [.. setLayouts];
            return new GraphicsPipelineBuiderFinal(_scope);
        }
    }

    public class FixedFunctionBuilder(GraphicsPipelineBuider scope)
    {
        GraphicsPipelineBuider _scope = scope;

        public FixedFunctionBuilder Rasterization(Func<RasterizationBuilder, RasterizationBuilder> f)
        {
            _scope._isRasterization = true;
            f(new RasterizationBuilder(_scope));
            _scope._rasterizationSettings.SType = StructureType.PipelineRasterizationStateCreateInfo;
            return this;
        }

        public FixedFunctionBuilder ColorBlending(IEnumerable<PipelineColorBlendAttachmentState> attachmentStates, bool logicOpEnable = false)
        {
            _scope._colorBlends = [.. attachmentStates];
            _scope._logicOpEnable = logicOpEnable;
            return this;
        }

        public FixedFunctionBuilder Multisampling(SampleCountFlags rasterizationSamples,
                                                    float minSampleShading = 1.0f,
                                                    bool sampleShadingEnable = false,
                                                    bool alphaToCoverageEnable = false,
                                                    bool alphaToOneEnable = false,
                                                    uint? sampleMask = null
                                                  )
        {

            PipelineMultisampleStateCreateInfo multisampling = new()
            {
                SType = StructureType.PipelineMultisampleStateCreateInfo,
                SampleShadingEnable = sampleShadingEnable,
                RasterizationSamples = rasterizationSamples,
                MinSampleShading = minSampleShading,
                PSampleMask = null,
                AlphaToCoverageEnable = alphaToCoverageEnable,
                AlphaToOneEnable = alphaToOneEnable
            };
            _scope._sampleMask = sampleMask;
            _scope._isMultisampling = true;
            _scope._multisamplingSettings = multisampling;
            return this;
        }

        public FixedFunctionBuilder DepthStencil(PipelineDepthStencilStateCreateInfo state)
        {
            _scope._isDepthStencil = true;
            _scope._pipelineDepthStencilSettings = state;
            return this;
        }
    }

    public class GraphicsPipelineBuiderFinal(GraphicsPipelineBuider scope)
    {
        private GraphicsPipelineBuider _scope = scope;

        public unsafe VkGraphicsPipeline Build(VkContext ctx, VkDevice device,
                                        int subpassIndex)
        {
            using var vertexInputStateCreateInfo = new VkVertexInputStateCreateInfo(_scope._vertexInputBindingDescriptions, _scope._vertexInputAttributeDescriptions);
            

            uint sampleMask = 0;
            if (_scope._sampleMask != null)
            {
                sampleMask = _scope._sampleMask.Value;
                _scope._multisamplingSettings.PSampleMask = (uint*)Unsafe.AsPointer(ref sampleMask);
            }
            var pRasterization = _scope._isRasterization
                ? (PipelineRasterizationStateCreateInfo*)Unsafe.AsPointer(ref _scope._rasterizationSettings) : null;

            var pMultisapmling = _scope._isMultisampling
                ? (PipelineMultisampleStateCreateInfo*)Unsafe.AsPointer(ref _scope._multisamplingSettings) : null;

            fixed (PipelineColorBlendAttachmentState* pColorBlend = _scope._colorBlends)
            {
                PipelineColorBlendStateCreateInfo colorBlendInfo = new()
                {
                    SType = StructureType.PipelineColorBlendStateCreateInfo,
                    LogicOpEnable = _scope._logicOpEnable,
                    AttachmentCount = (uint)_scope._colorBlends.Length,
                    PAttachments = pColorBlend
                };



                var pDepthStencil = _scope._isDepthStencil
                    ? (PipelineDepthStencilStateCreateInfo*)Unsafe.AsPointer(ref _scope._pipelineDepthStencilSettings) : null;

                var result = new VkGraphicsPipeline(
                        ctx,
                        device,
                        _scope._shaderInfos,
                        _scope._setLayouts,
                        _scope._renderPass,
                        subpassIndex,
                        _scope._dynamicStates,
                        vertexInputStateCreateInfo,
                        _scope._viewport,
                        _scope._scissor,
                        _scope._inputAssemblyState,
                        pRasterization,
                        pMultisapmling,
                        &colorBlendInfo,
                        pDepthStencil
                );

                return result;
            }
        }
    }



    public class StageBuilder
    {
        private GraphicsPipelineBuider _scope;

        internal StageBuilder(GraphicsPipelineBuider scope)
        {
            _scope = scope;
        }
        public StageBuilder Vertex(VkShaderInfo shaderInfo)
        {
            _scope._shaderInfos[ShaderStageFlags.VertexBit] = shaderInfo;
            return this;
        }

        public StageBuilder Fragment(VkShaderInfo shaderInfo)
        {
            _scope._shaderInfos[ShaderStageFlags.FragmentBit] = shaderInfo;
            return this;
        }

        public StageBuilder Geometry(VkShaderInfo shaderInfo)
        {
            _scope._shaderInfos[ShaderStageFlags.GeometryBit] = shaderInfo;
            return this;
        }

        public StageBuilder Tesselation(VkShaderInfo tessControl, VkShaderInfo tessEval)
        {
            _scope._shaderInfos[ShaderStageFlags.TessellationControlBit] = tessControl;
            _scope._shaderInfos[ShaderStageFlags.TessellationEvaluationBit] = tessEval;
            return this;
        }
    }

    public class RasterizationBuilder(GraphicsPipelineBuider scope)
    {
        private GraphicsPipelineBuider _scope = scope;

        public RasterizationBuilder WithRasterizerDiscard()
        {
            _scope._rasterizationSettings.RasterizerDiscardEnable = true;
            return this;
        }

        public RasterizationBuilder WithDepthClampEnable()
        {
            _scope._rasterizationSettings.DepthClampEnable = true;
            return this;
        }

        public RasterizationBuilder WithSettings(PolygonMode polygonMode, CullModeFlags cullMode, FrontFace frontFace, float lineWidth)
        {
            _scope._rasterizationSettings.PolygonMode = polygonMode;
            _scope._rasterizationSettings.LineWidth = lineWidth;
            _scope._rasterizationSettings.CullMode = cullMode;
            _scope._rasterizationSettings.FrontFace = frontFace;
            return this;
        }

        public RasterizationBuilder WithDepthBias(float constantFactor, float clamp, float slopeFactor)
        {
            _scope._rasterizationSettings.DepthBiasEnable = true;
            _scope._rasterizationSettings.DepthBiasConstantFactor = constantFactor;
            _scope._rasterizationSettings.DepthBiasClamp = clamp;
            _scope._rasterizationSettings.DepthBiasSlopeFactor = slopeFactor;
            return this;
        }
    }
}