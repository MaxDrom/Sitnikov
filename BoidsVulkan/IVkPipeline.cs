using Silk.NET.Vulkan;

namespace Sitnikov.BoidsVulkan;

public interface IVkPipeline
{
    Pipeline InternalPipeline { get; }
    PipelineBindPoint BindPoint { get; }
    PipelineLayout PipelineLayout { get; }
}