using System.Drawing;
using BoidsVulkan;
using Silk.NET.Vulkan;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using VkAllocatorSystem;


namespace SymplecticIntegrators;

public class ParticleSystem : IDisposable
{
    private int _N = 1024;
    private VkContext _ctx;
    private VkDevice _device;

    private IVkAllocator _allocator;
    private IVkAllocator _staggingAllocator;
    private VkBuffer _staggingBuffer;
    private VkComputePipeline _computePipeline;
    private VkCommandBuffer _cmdBuffer;
    private VkCommandBuffer _cmdBufferCopy;
    private VkDescriptorPool _descriptorPool;
    private VkTexture _eccentricityTexture;
    private VkImageView _eccentricityView;
    private DescriptorSet _descriptorSet;

    private bool _disposedValue;

    public ParticleSystem(VkContext ctx, VkDevice device, VkCommandPool commandPool, IVkAllocator allocator, IVkAllocator staggingAllocator)
    {
        _ctx = ctx;
        _device = device;

        _allocator = allocator;
        _staggingAllocator = staggingAllocator;

        _staggingBuffer = new VkBuffer(sizeof(float) * 1024 * 1024,
                                BufferUsageFlags.TransferDstBit, SharingMode.Exclusive, _staggingAllocator);
        var subresourceRange = new ImageSubresourceRange()
        {
            AspectMask = ImageAspectFlags.ColorBit,
            BaseMipLevel = 0,
            LevelCount = 1,
            BaseArrayLayer = 0,
            LayerCount = 1
        };

        using var shaderModule = new VkShaderModule(ctx, _device, "BoidsVulkan/shader_objects/ecc.comp.spv");
        DescriptorSetLayoutBinding binding = new()
        {
            Binding = 0,
            DescriptorType = DescriptorType.StorageImage,
            DescriptorCount = 1,
            StageFlags = ShaderStageFlags.ComputeBit
        };
        using var layout = new VkSetLayout(_ctx, _device, [binding]);

        _computePipeline = new VkComputePipeline(ctx, device, new VkShaderInfo(shaderModule, "main"),
                            [layout]);
        var buffers = commandPool.AllocateBuffers(CommandBufferLevel.Primary, 2);
        _cmdBuffer = buffers[0];
        _cmdBufferCopy = buffers[1];

        _eccentricityTexture = new(ImageType.Type2D, new(1024, 1024, 1), 1, 1, Format.R32Sfloat,
                                    ImageTiling.Optimal, ImageLayout.Undefined, ImageUsageFlags.StorageBit | ImageUsageFlags.TransferSrcBit, SampleCountFlags.Count1Bit, SharingMode.Exclusive, _allocator);
        _eccentricityView = new(ctx, device, _eccentricityTexture.Image, default,
                                subresourceRange);


        _descriptorPool = new VkDescriptorPool(ctx, device,
        [
            new DescriptorPoolSize(DescriptorType.StorageImage, 1)
        ], 1);


        _descriptorSet = _descriptorPool.AllocateDescriptors([layout])[0];
        var imageInfo = new DescriptorImageInfo(imageView: _eccentricityView.ImageView,
                                                imageLayout: ImageLayout.General);
        new VkDescriptorSetUpdater(ctx, device)
        .AppendWrite(_descriptorSet, 0, DescriptorType.StorageImage, [imageInfo])
        .Update();


        using (var recording = _cmdBuffer.Begin(CommandBufferUsageFlags.None))
        {
            recording.BindPipline(_computePipeline);
            ImageMemoryBarrier barrier = new()
            {
                SType = StructureType.ImageMemoryBarrier,
                OldLayout = ImageLayout.Undefined,
                NewLayout = ImageLayout.General,
                SrcAccessMask = AccessFlags.None,
                DstAccessMask = AccessFlags.ShaderWriteBit,
                Image = _eccentricityTexture.Image.Image,
                SubresourceRange = subresourceRange
            };

            recording.PipelineBarrier(PipelineStageFlags.TopOfPipeBit,
                                    PipelineStageFlags.ComputeShaderBit,
                                    DependencyFlags.None,
                                    imageMemoryBarriers: [barrier]);

            recording.BindDescriptorSets(PipelineBindPoint.Compute, _computePipeline.PipelineLayout, [_descriptorSet]);
            _ctx.Api.CmdDispatch(_cmdBuffer.InternalBuffer, 1024 / 32, 1024 / 32, 1);

            ImageMemoryBarrier barrier2 = new()
            {
                SType = StructureType.ImageMemoryBarrier,
                OldLayout = ImageLayout.General,
                NewLayout = ImageLayout.TransferSrcOptimal,
                SrcAccessMask = AccessFlags.ShaderWriteBit,
                DstAccessMask = AccessFlags.TransferReadBit,
                Image = _eccentricityTexture.Image.Image,
                SubresourceRange = subresourceRange
            };

            recording.PipelineBarrier(PipelineStageFlags.ComputeShaderBit,
                                    PipelineStageFlags.TransferBit,
                                    DependencyFlags.None,
                                    imageMemoryBarriers: [barrier2]);
        }

        using (var recording = _cmdBufferCopy.Begin(CommandBufferUsageFlags.None))
        {
            var region = new BufferImageCopy
            {
                BufferOffset = 0,
                BufferRowLength = 0, 
                BufferImageHeight = 0,
                ImageSubresource = new ImageSubresourceLayers
                {
                    AspectMask = ImageAspectFlags.ColorBit,
                    MipLevel = 0,
                    BaseArrayLayer = 0,
                    LayerCount = 1
                },
                ImageOffset = new Offset3D(0, 0, 0),
                ImageExtent = new Extent3D((uint)1024, (uint)1024, 1)
            };

            _ctx.Api.CmdCopyImageToBuffer(
                _cmdBufferCopy.InternalBuffer,
                _eccentricityTexture.Image.Image,
                ImageLayout.TransferSrcOptimal,
                _staggingBuffer.Buffer,
                1,
                ref region
            );
        }
    }

    public unsafe void Compute()
    {
        _cmdBuffer.Submit(_device.ComputeQueue, VkFence.NullHandle, [], []);
        _ctx.Api.QueueWaitIdle(_device.ComputeQueue);

        _cmdBufferCopy.Submit(_device.TransferQueue, VkFence.NullHandle, [], []);
        _ctx.Api.QueueWaitIdle(_device.TransferQueue);

        Rgba32[] data = new Rgba32[1024 * 1024];

        using (var mapped = _staggingBuffer.Map<float>(0, 1024 * 1024))
        {
            for (var i = 0; i < 1024 * 1024; i++)
            {
                data[i] = new(mapped[i], 0, 0);
            }
        }

        using var image = SixLabors.ImageSharp.Image.LoadPixelData<Rgba32>(data, 1024, 1024);
        image.SaveAsPng("ecc.png");
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                _staggingBuffer.Dispose();
                _descriptorPool.Dispose();
                _eccentricityView.Dispose();
                _eccentricityTexture.Dispose();
                _computePipeline.Dispose();
            }
            _disposedValue = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}