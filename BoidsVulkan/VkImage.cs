using Silk.NET.Vulkan;
using Sitnikov.BoidsVulkan.VkAllocatorSystem;

namespace Sitnikov.BoidsVulkan;

public class VkImage
{
    public VkImage(Image image, Format format, ImageType type)
    {
        Image = image;
        Format = format;
        Type = type;
    }

    public Image Image { get; }

    public Format Format { get; private set; }

    public ImageType Type { get; private set; }
}

public class VkTexture : IDisposable
{
    private readonly VkAllocator _allocator;
    private readonly VkContext _ctx;
    private readonly VkDevice _device;
    private readonly AllocationNode _node;
    private bool _disposedValue;

    public unsafe VkTexture(ImageType imageType,
        Extent3D extent,
        uint mipLevels,
        uint arrayLayers,
        Format format,
        ImageTiling tiling,
        ImageLayout initialLayout,
        ImageUsageFlags usageFlags,
        SampleCountFlags samples,
        SharingMode sharingMode,
        VkAllocator allocator)
    {
        _allocator = allocator;
        _ctx = allocator.Ctx;
        _device = allocator.Device;
        Extent = extent;
        var imageInfo = new ImageCreateInfo
        {
            SType = StructureType.ImageCreateInfo,
            ImageType = imageType,
            Extent = extent,
            MipLevels = mipLevels,
            ArrayLayers = arrayLayers,
            Format = format,
            Tiling = tiling,
            InitialLayout = initialLayout,
            Usage = usageFlags,
            Samples = samples,
            SharingMode = sharingMode,
        };
        if (_ctx.Api.CreateImage(_device.Device, ref imageInfo, null,
                out var imageUnmanaged) != Result.Success)
            throw new Exception("Failed to create texture");

        Image = new VkImage(imageUnmanaged, format, imageType);
        _ctx.Api.GetImageMemoryRequirements(_device.Device,
            imageUnmanaged, out var reqs);
        _node = allocator.Allocate(reqs);
        _ctx.Api.BindImageMemory(_device.Device, imageUnmanaged,
            _node.Memory, _node.Offset);
        Size = reqs.Size;
    }

    public Extent3D Extent { get; private set; }
    public VkImage Image { get; }

    public DeviceMemory Memory => _node.Memory;

    public ulong Size { get; }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            _allocator.Deallocate(_node);
            unsafe
            {
                _ctx.Api.DestroyImage(_device.Device, Image.Image,
                    null);
            }

            _disposedValue = true;
        }
    }

    public VkMappedMemory<byte> Map(ulong size)
    {
        return new VkMappedMemory<byte>(_ctx, _device, _node.Memory,
            _node.Offset, size, MemoryMapFlags.None);
    }

    ~VkTexture()
    {
        Dispose(false);
    }
}