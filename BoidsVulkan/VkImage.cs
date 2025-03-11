using Silk.NET.Vulkan;
using VkAllocatorSystem;

namespace BoidsVulkan;

public class VkImage
{
    public Image Image { get; private set; }

    public Format Format { get; private set; }

    public ImageType Type { get; private set; }


    public VkImage(Image image, Format format, ImageType type)
    {
        Image = image;
        Format = format;
        Type = type;
    }
}

public class VkTexture : IDisposable
{
    public VkImage Image { get; private set; }
    public DeviceMemory Memory => _node.Memory;
    public ulong Size => _size;
    private ulong _size;
    private IVkAllocator _allocator;
    private AllocationNode _node;
    private VkContext _ctx;
    private VkDevice _device;
    private bool disposedValue;
    public unsafe VkTexture(ImageType imageType, Extent3D extent, uint mipLevels, uint arrayLayers,
                    Format format,
                    ImageTiling tiling,
                    ImageLayout initialLayout,
                    ImageUsageFlags usageFlags,
                    SampleCountFlags samples,
                    SharingMode sharingMode,
                    IVkAllocator allocator)
    {
        _allocator = allocator;
        _ctx = allocator.Ctx;
        _device = allocator.Device;
        var imageInfo = new ImageCreateInfo()
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
            SharingMode = sharingMode
        };
        if (_ctx.Api.CreateImage(_device.Device, ref imageInfo, null, out var imageUnmanaged) != Result.Success)
        {
            throw new Exception("Failed to create texture");
        }

        Image = new VkImage(imageUnmanaged, format, imageType);
        _ctx.Api.GetImageMemoryRequirements(_device.Device, imageUnmanaged, out var reqs);
        _node = allocator.Allocate(reqs);
        _ctx.Api.BindImageMemory(_device.Device, imageUnmanaged, _node.Memory, _node.Offset);
        _size = reqs.Size;
    }
    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            _allocator.Deallocate(_node);
            unsafe
            {
                _ctx.Api.DestroyImage(_device.Device, Image.Image, null);
            }
            disposedValue = true;
        }
    }

    public VkMappedMemory<byte> Map(ulong size)
    {
        return new VkMappedMemory<byte>(_ctx, _device, _node.Memory, _node.Offset, size, MemoryMapFlags.None);
    }

    ~VkTexture()
    {
        Dispose(disposing: false);
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
