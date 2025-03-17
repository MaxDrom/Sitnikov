using Silk.NET.Vulkan;

namespace BoidsVulkan;

public class VkFrameBuffer : IDisposable
{
    private readonly VkContext _ctx;
    private readonly VkDevice _device;
    private readonly Framebuffer _frameBuffer;
    private bool _disposedValue;

    public VkFrameBuffer(VkContext ctx,
        VkDevice device,
        VkRenderPass renderPass,
        uint width,
        uint height,
        uint layers,
        IEnumerable<VkImageView> attachments)
    {
        _ctx = ctx;
        _device = device;

        FramebufferCreateInfo createInfo = new()
        {
            SType = StructureType.FramebufferCreateInfo,
            RenderPass = renderPass.RenderPass,
            Width = width,
            Height = height,
            Layers = layers,
            AttachmentCount = (uint)attachments.Count()
        };
        unsafe
        {
            fixed (ImageView* pAttachments = attachments
                       .Select(z => z.ImageView).ToArray())
            {
                createInfo.PAttachments = pAttachments;

                if (_ctx.Api.CreateFramebuffer(_device.Device,
                        ref createInfo, null, out _frameBuffer) !=
                    Result.Success)
                    throw new Exception(
                        "Failed to create framebuffer");
            }
        }
    }

    public Framebuffer Framebuffer => _frameBuffer;

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
                _ctx.Api.DestroyFramebuffer(_device.Device,
                    _frameBuffer, null);
            }

            _disposedValue = true;
        }
    }

    ~VkFrameBuffer()
    {
        Dispose(false);
    }
}