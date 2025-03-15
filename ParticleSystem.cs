using BoidsVulkan;
using Silk.NET.Vulkan;
using VkAllocatorSystem;

namespace SymplecticIntegrators;


public class ParticleSystem : IDisposable
{
    private int _N = 1024;
    private VkContext _ctx;
    private VkDevice _device;

    private IVkAllocator _allocator;
    private IVkAllocator _staggingAllocator;
    private VkComputePipeline _computePipeline;
    private VkCommandBuffer _cmdBuffer;
    private VkCommandBuffer _cmdBufferCopy;
    private VkDescriptorPool _descriptorPool;
    private VkTexture _eccenticityTexture;


    private bool _disposedValue;

    public ParticleSystem(VkContext ctx, VkDevice device, VkCommandPool commandPool, IVkAllocator allocator, IVkAllocator staggingAllocator)
    {
        _ctx = ctx;
        _device = device;

        _allocator = allocator;
        _staggingAllocator = staggingAllocator;
        
        using var shaderModule = new VkShaderModule(ctx, _device, "BoidsVulkan/shader_objects/ecc.comp.spv");
        DescriptorSetLayoutBinding binding = new ()
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

        _eccenticityTexture = new(ImageType.Type2D, new(1024, 1024, 1), 1, 1, Format.R32Sfloat,
                                    ImageTiling.Optimal, ImageLayout.Undefined, ImageUsageFlags.StorageBit, SampleCountFlags.Count1Bit, SharingMode.Exclusive, _allocator);
        _descriptorPool = new VkDescriptorPool(ctx, device, 
        [
            new DescriptorPoolSize(DescriptorType.StorageImage, 1)
        ], 1);
        
        
        // using(var recording = _cmdBuffer.Begin(CommandBufferUsageFlags.None))
        // {
        //     _ctx.Api.CmdBindDescriptorSets(_cmdBuffer.InternalBuffer, 
        //                                     PipelineBindPoint.Compute,
        //                                     _computePipeline.PipelineLayout,
        //                                     0,
        //                                     )
        // }
    }

    public void Compute()
    {

    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                _descriptorPool.Dispose();
                _eccenticityTexture.Dispose();
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