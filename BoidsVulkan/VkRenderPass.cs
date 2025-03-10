using Silk.NET.Vulkan;

namespace BoidsVulkan;

public class VkRenderPass : IDisposable
{
    public RenderPass RenderPass => _renderPass;
    private readonly VkContext _ctx;
    private readonly VkDevice _device;
    private readonly RenderPass _renderPass;
    private bool disposedValue;

    public VkRenderPass(VkContext ctx,
        VkDevice device,
        VkSubpassInfo[] subpassInfos,
        IEnumerable<SubpassDependency> subpassDependencies,
        IEnumerable<AttachmentDescription> attachmentDescriptions)
    {
        _ctx = ctx;
        _device = device;

        unsafe
        {
            var psubpassDescr = stackalloc SubpassDescription[subpassInfos.Length];
            var clrAttachmentLength = subpassInfos.Select(z => z.ColorAttachmentReferences.Length).Sum();
            var clrAttachmentBuffer = stackalloc AttachmentReference[clrAttachmentLength];
            var clrAttachmentBufferCopy = clrAttachmentBuffer;
            foreach (var subpassInfo in subpassInfos)
            {
                for (var i = 0; i < subpassInfo.ColorAttachmentReferences.Length; i++)
                    clrAttachmentBufferCopy[i] = subpassInfo.ColorAttachmentReferences[i];

                clrAttachmentBufferCopy += subpassInfo.ColorAttachmentReferences.Length;
            }


            for (var i = 0; i < subpassInfos.Length; i++)
            {
                psubpassDescr[i] = new SubpassDescription()
                {
                    PipelineBindPoint = subpassInfos[i].BindPoint,
                    ColorAttachmentCount = (uint)subpassInfos[i].ColorAttachmentReferences.Length,
                    PColorAttachments = clrAttachmentBuffer
                };
                clrAttachmentBuffer += subpassInfos[i].ColorAttachmentReferences.Length;
            }
            var RenderPassCreateInfo = new RenderPassCreateInfo()
            {
                SType = StructureType.RenderPassCreateInfo,
                PSubpasses = psubpassDescr,
                SubpassCount = (uint)subpassInfos.Length,
                AttachmentCount = (uint)attachmentDescriptions.Count(),
            };

            fixed (SubpassDependency* pdeps = subpassDependencies.ToArray())
            {
                fixed (AttachmentDescription* pattach = attachmentDescriptions.ToArray())
                {
                    RenderPassCreateInfo.PAttachments = pattach;
                    RenderPassCreateInfo.DependencyCount = (uint)subpassDependencies.Count();
                    RenderPassCreateInfo.PDependencies = pdeps;
                    if (_ctx.Api.CreateRenderPass(_device.Device, ref RenderPassCreateInfo,
                                null, out _renderPass) != Result.Success)
                        throw new Exception("Failed to create render pass");
                }
            }
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            unsafe
            {
                _ctx.Api.DestroyRenderPass(_device.Device, _renderPass, null);
            }
            disposedValue = true;
        }
    }

    ~VkRenderPass()
    {
        Dispose(disposing: false);
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}