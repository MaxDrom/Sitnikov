using Silk.NET.Vulkan;

namespace BoidsVulkan;

public interface IVkPipeline
{
    Pipeline InternalPipeline{get;}
    PipelineBindPoint BindPoint{get;}
}