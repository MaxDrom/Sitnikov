using System.Drawing;
using System.Runtime.InteropServices;
using BoidsVulkan;
using Silk.NET.Vulkan;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using VkAllocatorSystem;

namespace SymplecticIntegrators;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
struct UpdateData
{
    public int N;
    public float delta;
    public float ecc;
    public float T;
}

public class ParticleSystemGPUFactory : IParticleSystemFactory
{
    private double _e;
    private float[] _steps;

    public ParticleSystemGPUFactory(double e, int order)
    {
        _e = e;
        var tracer = new Tracer<float>();
        var integrator = YoshidaIntegrator<float, Vector<float>>.BuildFromLeapfrog(tracer.dV, tracer.dT, order);
        var (q, p) = integrator.Step(new Vector<float>([0]), new Vector<float>([0]), 1);
        
        var steps = tracer.GetReducedSteps(q[0], p[0]);
        var current = 0;
        var result = new List<float>();
        for(var i = 0; i<steps.Count; i++)
        {
            if((current+1)%2 != (int)steps[i].Item2)
            {
                result.Add(0);
                current++;
            }
            result.Add(steps[i].Item1);
            current++;
        }
        if(current%2 != 0)
            result.Add(0);
        _steps = result.ToArray();
    }
    public IParticleSystem Create(VkContext ctx, VkDevice device, VkCommandPool commandPool, IVkAllocator allocator, IVkAllocator staggingAllocator, Instance[] initialData)
    {
        return new ParticleSystemGPU(ctx, device, commandPool, allocator, staggingAllocator, _e, initialData, _steps);
    }
}

public class ParticleSystemGPU : IParticleSystem
{
    private int _N;
    private double _e;
    private VkContext _ctx;
    private VkDevice _device;

    private IVkAllocator _allocator;
    private IVkAllocator _staggingAllocator;
    private VkComputePipeline _computePipeline;
    private VkCommandBuffer _cmdBuffer;
    private VkCommandBuffer _cmdBufferCopy;
    private VkDescriptorPool _descriptorPool;
    private VkTexture _eccentricityTexture;
    private VkImageView _eccentricityView;
    private DescriptorSet _descriptorSet;
    private VkBuffer<Instance> _instanceBuffer;
    private VkBuffer<float> _integratorBuffer;
    private VkFence _fence;
    private bool _disposedValue;

    public VkBuffer<Instance> Buffer => _instanceBuffer;

    public ParticleSystemGPU(VkContext ctx, VkDevice device, VkCommandPool commandPool, IVkAllocator allocator,
                          IVkAllocator staggingAllocator, double e, Instance[] initials, float[] timeSteps)
    {
        _ctx = ctx;
        _device = device;
        _allocator = allocator;
        _staggingAllocator = staggingAllocator;
        _fence = new VkFence(_ctx, _device);
        _e = e;
        _N = initials.Length;

        _instanceBuffer = new VkBuffer<Instance>(_N,
                                                BufferUsageFlags.StorageBufferBit
                                                | BufferUsageFlags.TransferDstBit
                                                | BufferUsageFlags.TransferSrcBit,
                                                SharingMode.Exclusive,
                                                _allocator);

        _integratorBuffer = new VkBuffer<float>(timeSteps.Length,
                                                BufferUsageFlags.StorageBufferBit
                                                | BufferUsageFlags.TransferDstBit
                                                | BufferUsageFlags.TransferSrcBit,
                                                SharingMode.Exclusive,
                                                _allocator);
        using var staggingBufferIntegrator = new VkBuffer<float>(timeSteps.Length, BufferUsageFlags.TransferSrcBit,
                                                            SharingMode.Exclusive,
                                                            _allocator);

        using (var mapped = staggingBufferIntegrator.Map(0, timeSteps.Length))
        {
            for (var i = 0; i < timeSteps.Length; i++)
                mapped[i] = timeSteps[i];
        }


        using var staggingBuffer = new VkBuffer<Instance>(_N, BufferUsageFlags.TransferSrcBit,
                                                            SharingMode.Exclusive,
                                                            _allocator);

        using (var mapped = staggingBuffer.Map(0, _N))
        {
            for (var i = 0; i < _N; i++)
                mapped[i] = initials[i];
        }

        var subresourceRange = new ImageSubresourceRange()
        {
            AspectMask = ImageAspectFlags.ColorBit,
            BaseMipLevel = 0,
            LevelCount = 1,
            BaseArrayLayer = 0,
            LayerCount = 1
        };

        using var shaderModule = new VkShaderModule(ctx, _device, "BoidsVulkan/shader_objects/base.comp.spv");
        DescriptorSetLayoutBinding[] bindings = [new() { Binding = 0, DescriptorType = DescriptorType.StorageImage,
                                                     DescriptorCount = 1, StageFlags = ShaderStageFlags.ComputeBit },
                                                     new(){Binding = 1, DescriptorType = DescriptorType.StorageBuffer,
                                                     DescriptorCount = 1, StageFlags = ShaderStageFlags.ComputeBit },
                                                     new(){Binding = 2, DescriptorType = DescriptorType.StorageBuffer,
                                                     DescriptorCount = 1, StageFlags = ShaderStageFlags.ComputeBit }];
        using var layout = new VkSetLayout(_ctx, _device, bindings);
        PushConstantRange pushConstant = new(ShaderStageFlags.ComputeBit, 0, (uint)Marshal.SizeOf<UpdateData>());
        _computePipeline =
            new VkComputePipeline(ctx, device, new VkShaderInfo(shaderModule, "main"), [layout], [pushConstant]);
        var buffers = commandPool.AllocateBuffers(CommandBufferLevel.Primary, 2);
        _cmdBuffer = buffers[0];
        _cmdBufferCopy = buffers[1];

        LoadTexture();

        using (var recording = _cmdBufferCopy.Begin(CommandBufferUsageFlags.OneTimeSubmitBit))
        {
            recording.CopyBuffer(staggingBuffer, _instanceBuffer, 0, 0, staggingBuffer.Size);
            recording.CopyBuffer(staggingBufferIntegrator, _integratorBuffer, 0, 0, staggingBufferIntegrator.Size);
        }
        _cmdBufferCopy.Submit(device.TransferQueue, VkFence.NullHandle, [], []);
        _ctx.Api.QueueWaitIdle(device.TransferQueue);
        _cmdBufferCopy.Reset(CommandBufferResetFlags.None);

        _eccentricityView = new(ctx, device, _eccentricityTexture.Image, default, subresourceRange);

        _descriptorPool =
            new VkDescriptorPool(ctx, device, [new DescriptorPoolSize(DescriptorType.StorageImage, 1),
            new DescriptorPoolSize(DescriptorType.StorageBuffer, 2)], 1);

        _descriptorSet = _descriptorPool.AllocateDescriptors([layout])[0];
        var imageInfo =
            new DescriptorImageInfo(imageView: _eccentricityView.ImageView, imageLayout: ImageLayout.General);
        var bufferInfo1 = new DescriptorBufferInfo(_instanceBuffer.Buffer, 0, _instanceBuffer.Size);
        var bufferInfo2 = new DescriptorBufferInfo(_integratorBuffer.Buffer, 0, _integratorBuffer.Size);

        new VkDescriptorSetUpdater(ctx, device)
            .AppendWrite(_descriptorSet, 0, DescriptorType.StorageImage, [imageInfo])
            .AppendWrite(_descriptorSet, 1, DescriptorType.StorageBuffer, [bufferInfo1, bufferInfo2])
            .Update();
    }

    private void LoadTexture()
    {
        using var image = SixLabors.ImageSharp.Image.Load<RgbaVector>("ecc.png");
        _eccentricityTexture =
            new(ImageType.Type2D, new((uint)image.Width, (uint)image.Height, 1), 1, 1, Format.R32Sfloat,
                ImageTiling.Optimal, ImageLayout.Undefined, ImageUsageFlags.StorageBit | ImageUsageFlags.TransferDstBit,
                SampleCountFlags.Count1Bit, SharingMode.Exclusive, _allocator);

        Span<RgbaVector> span = new(new RgbaVector[image.Height * image.Width]);
        image.CopyPixelDataTo(span);
        var staggingBuffer = new VkBuffer<float>(image.Height * image.Width,
                                          BufferUsageFlags.TransferSrcBit, SharingMode.Exclusive, _staggingAllocator);
        using (var mapped = staggingBuffer.Map(0, image.Width * image.Height))
        {
            for (var i = 0; i < image.Height * image.Width; i++)
                mapped[i] = span[i].R;
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
                ImageExtent = new Extent3D((uint)image.Width, (uint)image.Height, 1)
            };

            ImageMemoryBarrier barrier = new()
            {
                SType = StructureType.ImageMemoryBarrier,
                DstAccessMask = AccessFlags.TransferWriteBit,
                SrcAccessMask = AccessFlags.None,
                OldLayout = ImageLayout.Undefined,
                NewLayout = ImageLayout.TransferDstOptimal,
                Image = _eccentricityTexture.Image.Image,
                SubresourceRange = new ImageSubresourceRange(
                                                     aspectMask: ImageAspectFlags.ColorBit, baseMipLevel: 0,
                                                     levelCount: 1, baseArrayLayer: 0, layerCount: 1)
            };

            recording.PipelineBarrier(PipelineStageFlags.TopOfPipeBit, PipelineStageFlags.TransferBit, 0,
                                      imageMemoryBarriers: [barrier]);

            _ctx.Api.CmdCopyBufferToImage(_cmdBufferCopy.InternalBuffer, staggingBuffer.Buffer,
                                          _eccentricityTexture.Image.Image, ImageLayout.TransferDstOptimal, 1,
                                          ref region);
        }

        _cmdBufferCopy.Submit(_device.TransferQueue, VkFence.NullHandle, [], []);
        _ctx.Api.QueueWaitIdle(_device.TransferQueue);
        _cmdBufferCopy.Reset(CommandBufferResetFlags.None);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                _fence.Dispose();
                _instanceBuffer.Dispose();
                _integratorBuffer.Dispose();
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

    public async Task Update(double delta, double totalTime)
    {

        _fence.Reset();
        _cmdBuffer.Reset(CommandBufferResetFlags.None);
        using (var recording = _cmdBuffer.Begin(CommandBufferUsageFlags.OneTimeSubmitBit))
        {
            ImageMemoryBarrier barrier = new()
            {
                SType = StructureType.ImageMemoryBarrier,
                DstAccessMask = AccessFlags.ShaderReadBit,
                SrcAccessMask = AccessFlags.None,
                OldLayout = ImageLayout.Undefined,
                NewLayout = ImageLayout.General,
                Image = _eccentricityTexture.Image.Image,
                SubresourceRange = new ImageSubresourceRange(
                                                     aspectMask: ImageAspectFlags.ColorBit,
                                                     baseMipLevel: 0,
                                                     levelCount: 1,
                                                     baseArrayLayer: 0,
                                                     layerCount: 1)
            };
            recording.PipelineBarrier(PipelineStageFlags.TopOfPipeBit,
                                      PipelineStageFlags.ComputeShaderBit,
                                      0,
                                      imageMemoryBarriers: [barrier]);
            recording.BindPipline(_computePipeline);
            recording.BindDescriptorSets(PipelineBindPoint.Compute,
                                        _computePipeline.PipelineLayout,
                                        [_descriptorSet]);



            var updateData = new UpdateData()
            {
                N = _N,
                delta = (float)delta, /// steps,
                T = (float)totalTime + (float)delta,// / steps * i,
                ecc = (float)_e
            };
            _ctx.Api.CmdPushConstants(_cmdBuffer.InternalBuffer,
                                                _computePipeline.PipelineLayout, ShaderStageFlags.ComputeBit,
                                                0, (uint)Marshal.SizeOf<UpdateData>(),
                                                ref updateData);
            _ctx.Api.CmdDispatch(_cmdBuffer.InternalBuffer, (uint)Math.Ceiling(_N / 1024.0), 1, 1);

        }

        _cmdBuffer.Submit(_device.ComputeQueue, _fence, [], []);
        await _fence.WaitFor();

    }
}