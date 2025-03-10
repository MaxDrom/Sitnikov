using Silk.NET.Vulkan;

namespace BoidsVulkan;

public class VkImage
{
    public Image Image {get; private set;}

    public Format Format {get; private set;}

    public ImageViewType ViewType {get; private set;}

    public VkImage(Image image, Format format, ImageViewType viewType)
    {
        Image = image;
        Format = format;
        ViewType = viewType;
    }

}
