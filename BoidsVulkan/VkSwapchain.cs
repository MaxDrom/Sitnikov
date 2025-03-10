using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;

namespace BoidsVulkan;

public class VkSwapchain : IDisposable
{
    public Extent2D Extent { get; private set; }
    public VkImage[] Images => _images;
    internal SwapchainKHR Swapchain => _swapchain;
    private readonly VkContext _ctx;
    private readonly SwapchainKHR _swapchain;
    private readonly VkSwapchainContext _swapchainCtx;

    private readonly VkImage[] _images;
    private bool disposedValue;

    public VkSwapchain(VkContext ctx, SurfaceKHR surface, VkSwapchainContext swapchainCtx, IEnumerable<uint> familyIndicies, uint imageCount,
        Format imageFormat, ColorSpaceKHR imageColorSpace, Extent2D imageExtent,
        PresentModeKHR presentMode,
        bool clipped = true,
        CompositeAlphaFlagsKHR compositeAlpha = CompositeAlphaFlagsKHR.OpaqueBitKhr,
        uint imageArrayLayers = 1u,
        ImageUsageFlags imageUsageFlags = ImageUsageFlags.ColorAttachmentBit,
        VkSwapchain oldSwapchain = null
        )
    {

        _ctx = ctx;
        var swapchainCreateInfo = new SwapchainCreateInfoKHR
        {
            SType = StructureType.SwapchainCreateInfoKhr,
            Surface = surface,
            MinImageCount = imageCount,
            ImageFormat = imageFormat,
            ImageColorSpace = imageColorSpace,
            ImageExtent = imageExtent,
            ImageArrayLayers = imageArrayLayers,
            ImageUsage = imageUsageFlags,
            PreTransform = SurfaceTransformFlagsKHR.IdentityBitKhr,
            CompositeAlpha = compositeAlpha,
            PresentMode = presentMode,
            Clipped = clipped,
            OldSwapchain = oldSwapchain == null ? _swapchain : SilkMarshal.NullRef<SwapchainKHR>()
        };
        var hashSet = familyIndicies.ToHashSet();
        unsafe
        {
            fixed (uint* pfamilyIndicies = hashSet.ToArray())
            {
                if (hashSet.Count == 1)
                {
                    swapchainCreateInfo.ImageSharingMode = SharingMode.Exclusive;
                    swapchainCreateInfo.QueueFamilyIndexCount = 0;
                    swapchainCreateInfo.PQueueFamilyIndices = null;
                }
                else
                {
                    swapchainCreateInfo.ImageSharingMode = SharingMode.Concurrent;
                    swapchainCreateInfo.QueueFamilyIndexCount = (uint)hashSet.Count;
                    swapchainCreateInfo.PQueueFamilyIndices = pfamilyIndicies;
                }

                swapchainCtx.CreateUnmanagedSwapchain(ref swapchainCreateInfo,
                        out _swapchain);
            }
        }
        _swapchainCtx = swapchainCtx;
        Extent = imageExtent;
        _images = _swapchainCtx.GetSwapchainImages(_swapchain)
                .Select(z => new VkImage(z, imageFormat, ImageType.Type2D))
                .ToArray();
    }

    public Result AcquireNextImage(VkDevice device, VkSemaphore semaphore, out uint uIndex, VkFence fence = null)
    {
        var ffence = new Fence();
        uint tmp;
        Result result;
        if (fence != null)
            ffence = fence.InternalFence;
        unsafe
        {
            result = _swapchainCtx.Api.AcquireNextImage(device.Device, _swapchain, ulong.MaxValue, semaphore.Semaphore, ffence, &tmp);
        }
        uIndex = tmp;
        return result;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            _swapchainCtx.DestroySwapchain(_swapchain);
            disposedValue = true;
        }
    }

    ~VkSwapchain()
    {
        Dispose(disposing: false);
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}