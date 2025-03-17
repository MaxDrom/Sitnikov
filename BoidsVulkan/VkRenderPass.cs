using Silk.NET.Vulkan;

namespace BoidsVulkan;

public class VkRenderPass : IDisposable
{
    private readonly VkContext _ctx;
    private readonly VkDevice _device;
    private readonly RenderPass _renderPass;
    private bool _disposedValue;

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
            var psubpassDescr =
                stackalloc SubpassDescription[subpassInfos.Length];
            var clrAttachmentLength = subpassInfos
                .Select(z => z.ColorAttachmentReferences.Length)
                .Sum();
            var clrAttachmentBuffer =
                stackalloc AttachmentReference[clrAttachmentLength];
            var clrAttachmentBufferCopy = clrAttachmentBuffer;
            foreach (var subpassInfo in subpassInfos)
            {
                for (var i = 0;
                     i < subpassInfo.ColorAttachmentReferences.Length;
                     i++)
                    clrAttachmentBufferCopy[i] = subpassInfo
                        .ColorAttachmentReferences[i];

                clrAttachmentBufferCopy += subpassInfo
                    .ColorAttachmentReferences.Length;
            }

            for (var i = 0; i < subpassInfos.Length; i++)
            {
                psubpassDescr[i] = new SubpassDescription
                {
                    PipelineBindPoint = subpassInfos[i].BindPoint,
                    ColorAttachmentCount =
                        (uint)subpassInfos[i]
                            .ColorAttachmentReferences.Length,
                    PColorAttachments = clrAttachmentBuffer,
                };
                clrAttachmentBuffer += subpassInfos[i]
                    .ColorAttachmentReferences.Length;
            }

            var renderPassCreateInfo = new RenderPassCreateInfo
            {
                SType = StructureType.RenderPassCreateInfo,
                PSubpasses = psubpassDescr,
                SubpassCount = (uint)subpassInfos.Length,
                AttachmentCount =
                    (uint)attachmentDescriptions.Count(),
            };

            fixed (SubpassDependency* pdeps =
                       subpassDependencies.ToArray())
            {
                fixed (AttachmentDescription* pattach =
                           attachmentDescriptions.ToArray())
                {
                    renderPassCreateInfo.PAttachments = pattach;
                    renderPassCreateInfo.DependencyCount =
                        (uint)subpassDependencies.Count();
                    renderPassCreateInfo.PDependencies = pdeps;
                    if (_ctx.Api.CreateRenderPass(_device.Device,
                            ref renderPassCreateInfo, null,
                            out _renderPass) != Result.Success)
                        throw new Exception(
                            "Failed to create render pass");
                }
            }
        }
    }

    public RenderPass RenderPass => _renderPass;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            unsafe
            {
                _ctx.Api.DestroyRenderPass(_device.Device,
                    _renderPass, null);
            }

            _disposedValue = true;
        }
    }

    ~VkRenderPass()
    {
        Dispose(false);
    }
}