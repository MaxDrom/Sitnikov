using Silk.NET.Vulkan;

namespace BoidsVulkan;

public class UniformDescription : Attribute
{
    public UniformDescription(int binding,
        ShaderStageFlags shaderStageFlags,
        DescriptorType descriptorType,
        int descriptorCount)
    {
        Binding = binding;
        ShaderStageFlags = shaderStageFlags;
        DescriptorType = descriptorType;
        DescriptorCount = descriptorCount;
    }

    public int Binding { get; set; }

    public int DescriptorCount { get; set; }

    public ShaderStageFlags ShaderStageFlags { get; set; }

    public DescriptorType DescriptorType { get; set; }
}