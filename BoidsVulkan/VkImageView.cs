using Silk.NET.Vulkan;

namespace BoidsVulkan;

public class VkImageView : IDisposable
{
    private readonly VkContext _ctx;
    private readonly VkDevice _device;
    private readonly ImageView _imageView;
    private bool _disposedValue;

    public VkImageView(VkContext ctx,
        VkDevice device,
        VkImage image,
        ComponentMapping mapping,
        ImageSubresourceRange subresourceRange,
        ImageViewType? viewType = null)
    {
        viewType ??= (ImageViewType)image.Type;
        var imageCreateInfo = new ImageViewCreateInfo
        {
            SType = StructureType.ImageViewCreateInfo,
            Image = image.Image,
            Format = image.Format,
            ViewType = viewType.Value,
            Components = mapping,
            SubresourceRange = subresourceRange,
        };
        _ctx = ctx;
        _device = device;
        unsafe
        {
            if (ctx.Api.CreateImageView(device.Device,
                    ref imageCreateInfo, null, out _imageView) !=
                Result.Success)
                throw new Exception("Failed to create image view");
        }
    }

    public ImageView ImageView => _imageView;

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
                _ctx.Api.DestroyImageView(_device.Device, _imageView,
                    null);
            }

            _disposedValue = true;
        }
    }

    ~VkImageView()
    {
        Dispose(false);
    }
}