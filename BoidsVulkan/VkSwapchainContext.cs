using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace BoidsVulkan;

public unsafe class VkSwapchainContext : IDisposable
{
    private readonly VkContext _ctx;
    private readonly VkDevice _device;
    private readonly KhrSwapchain _swapchainApi;
    private bool _disposedValue;

    public VkSwapchainContext(VkContext ctx, VkDevice device)
    {
        _ctx = ctx;
        _device = device;
        _ctx.Api.TryGetDeviceExtension(_ctx.Instance, device.Device,
            out _swapchainApi);
    }

    public KhrSwapchain Api => _swapchainApi;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public void CreateUnmanagedSwapchain(
        ref SwapchainCreateInfoKHR createInfo,
        out SwapchainKHR swapchain)
    {
        if (_swapchainApi.CreateSwapchain(_device.Device,
                ref createInfo, null, out swapchain) !=
            Result.Success)
            throw new Exception("Failed to create swapchain!");
    }

    public void DestroySwapchain(SwapchainKHR swapchain)
    {
        _swapchainApi.DestroySwapchain(_device.Device, swapchain,
            null);
    }

    public Image[] GetSwapchainImages(SwapchainKHR swapchain)
    {
        uint n;
        _swapchainApi.GetSwapchainImages(_device.Device, swapchain,
            &n, null);
        var result = new Image[n];
        fixed (Image* presult = result)
        {
            if (_swapchainApi.GetSwapchainImages(_device.Device,
                    swapchain, &n, presult) != Result.Success)
                throw new Exception(
                    "Failed create swapchain images!");
        }

        return result;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing) _swapchainApi.Dispose();

            _disposedValue = true;
        }
    }

    public void QueuePresent(Queue queue,
        IEnumerable<uint> imageIndexes,
        IEnumerable<VkSwapchain> swapchains,
        IEnumerable<VkSemaphore> semaphores)
    {
        var swapchainCount = swapchains.Count();
        var pswapchainbuf = stackalloc SwapchainKHR[swapchainCount];
        var semaphoresCount = semaphores.Count();
        var psemaphorebuf = stackalloc Semaphore[semaphoresCount];
        var imagesCount = imageIndexes.Count();
        var pimagebuf = stackalloc uint[imagesCount];
        var tmp = pswapchainbuf;
        foreach (var swapchain in swapchains)
        {
            tmp[0] = swapchain.Swapchain;
            tmp++;
        }

        var tmp2 = psemaphorebuf;
        foreach (var semaphore in semaphores)
        {
            tmp2[0] = semaphore.Semaphore;
            tmp2++;
        }

        var tmp3 = pimagebuf;
        foreach (var image in imageIndexes)
        {
            tmp3[0] = image;
            tmp3++;
        }

        // var imageIndex = 0u;
        var presentInfo = new PresentInfoKHR
        {
            SType = StructureType.PresentInfoKhr,
            SwapchainCount = (uint)swapchainCount,
            PSwapchains = pswapchainbuf,
            WaitSemaphoreCount = (uint)semaphoresCount,
            PWaitSemaphores = psemaphorebuf,
            PImageIndices = pimagebuf,
        };
        _swapchainApi.QueuePresent(queue, ref presentInfo);
    }
}