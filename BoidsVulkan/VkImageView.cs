using Silk.NET.Vulkan;

namespace BoidsVulkan;

public class VkImageView : IDisposable
{
    public ImageView ImageView => _imageView;
    private readonly ImageView _imageView;
    private readonly VkDevice _device;
    private readonly VkContext _ctx;
    private bool disposedValue;

    public VkImageView(VkContext ctx, VkDevice device, VkImage image, ComponentMapping mapping, ImageSubresourceRange subresourceRange, ImageViewType? viewType = null)
    {
        viewType ??= (ImageViewType)image.Type;
        var imageCreateInfo = new ImageViewCreateInfo()
        {
            SType = StructureType.ImageViewCreateInfo,
            Image = image.Image,
            Format = image.Format,
            ViewType = viewType.Value,
            Components = mapping,
            SubresourceRange = subresourceRange
        };
        _ctx = ctx;
        _device = device;
        unsafe
        {
            if (ctx.Api.CreateImageView(device.Device, ref imageCreateInfo, null, out _imageView) != Result.Success)
                throw new Exception("Failed to create image view");
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            unsafe
            {
                _ctx.Api.DestroyImageView(_device.Device, _imageView, null);
            }
            disposedValue = true;
        }
    }

    ~VkImageView()
    {
        Dispose(disposing: false);
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}