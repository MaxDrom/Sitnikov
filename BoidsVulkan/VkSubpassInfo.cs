using Silk.NET.Vulkan;

namespace Sitnikov.BoidsVulkan;

public class VkSubpassInfo
{
    public VkSubpassInfo(PipelineBindPoint bindPoint,
        IEnumerable<AttachmentReference> colorAttachmentReferences)
    {
        ColorAttachmentReferences =
            colorAttachmentReferences.ToArray();
        BindPoint = bindPoint;
    }

    public AttachmentReference[] ColorAttachmentReferences
    {
        get;
        private set;
    }

    public PipelineBindPoint BindPoint { get; private set; }
}