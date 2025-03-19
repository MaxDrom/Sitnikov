using Silk.NET.Vulkan;

namespace Sitnikov.BoidsVulkan;

public class VkRenderPass : IDisposable
{
    private readonly VkContext _ctx;
    private readonly VkDevice _device;
    private readonly RenderPass _renderPass;
    private bool _disposedValue;

    public VkRenderPass(VkContext ctx,
        VkDevice device,
        VkSubpassInfo[] subpassInfos,
        SubpassDependency[] subpassDependencies,
        AttachmentDescription[] attachmentDescriptions)
    {
        _ctx = ctx;
        _device = device;

        unsafe
        {
            var pSubPassDescription =
                stackalloc SubpassDescription[subpassInfos.Length];
            var clrAttachmentLength = subpassInfos
                .Select(z => z.ColorAttachmentReferences.Length)
                .Sum();
            var colorAttachmentBuffer =
                stackalloc AttachmentReference[clrAttachmentLength];
            var clrAttachmentBufferCopy = colorAttachmentBuffer;
            foreach (var subPassInfo in subpassInfos)
            {
                for (var i = 0;
                     i < subPassInfo.ColorAttachmentReferences.Length;
                     i++)
                    clrAttachmentBufferCopy[i] = subPassInfo
                        .ColorAttachmentReferences[i];

                clrAttachmentBufferCopy += subPassInfo
                    .ColorAttachmentReferences.Length;
            }

            for (var i = 0; i < subpassInfos.Length; i++)
            {
                pSubPassDescription[i] = new SubpassDescription
                {
                    PipelineBindPoint = subpassInfos[i].BindPoint,
                    ColorAttachmentCount =
                        (uint)subpassInfos[i]
                            .ColorAttachmentReferences.Length,
                    PColorAttachments = colorAttachmentBuffer,
                };
                colorAttachmentBuffer += subpassInfos[i]
                    .ColorAttachmentReferences.Length;
            }

            var renderPassCreateInfo = new RenderPassCreateInfo
            {
                SType = StructureType.RenderPassCreateInfo,
                PSubpasses = pSubPassDescription,
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