using Silk.NET.Vulkan;

namespace BoidsVulkan;


public class VkSubpassInfo
{
    public AttachmentReference[] ColorAttachmentReferences {get; private set;}
    public PipelineBindPoint BindPoint {get; private set;}
    public VkSubpassInfo(PipelineBindPoint bindPoint,IEnumerable<AttachmentReference> colorAttachmentReferences)
    {
        ColorAttachmentReferences = colorAttachmentReferences.ToArray();
        BindPoint = bindPoint;
    }
}