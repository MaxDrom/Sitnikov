using System.Runtime.InteropServices;
using Autofac.Features.AttributeFilters;
using Silk.NET.Vulkan;
using Sitnikov.BoidsVulkan;
using Sitnikov.BoidsVulkan.VkAllocatorSystem;
using Sitnikov.symplecticIntegrators;
using SixLabors.ImageSharp.PixelFormats;
using Image = SixLabors.ImageSharp.Image;

namespace Sitnikov;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct UpdateData
{
    public int N;
    public float delta;
    public float ecc;
    public float T;
}

public class ParticleSystemGpu : IParticleSystem
{
    public VkBuffer<Instance> Buffer { get; }
    public IEnumerable<Instance> DataOnCpu
    {
        get
        {
            _copyFence.Reset();
            _cmdBufferCopy.Reset(CommandBufferResetFlags.None);
            using (var recording = _cmdBufferCopy.Begin(CommandBufferUsageFlags.OneTimeSubmitBit))
            {
                recording.CopyBuffer(Buffer, _stagingBuffer, 0, 0, Buffer.Size);
            }
            
            _cmdBufferCopy.Submit(_device.TransferQueue, _copyFence, [], []);
            _copyFence.WaitFor().GetAwaiter().GetResult();

            return _mapped;
        }
    }

    private readonly VkAllocator _allocator;
    private readonly VkCommandBuffer _cmdBuffer;
    private readonly VkCommandBuffer _cmdBufferCopy;
    private readonly VkComputePipeline _computePipeline;
    private readonly VkContext _ctx;
    private readonly VkDescriptorPool _descriptorPool;
    private readonly DescriptorSet _descriptorSet;
    private readonly VkDevice _device;
    private readonly double _e;
    private readonly VkImageView _eccentricityView;
    private readonly VkFence _fence;
    private readonly VkBuffer<float> _integratorBuffer;
    private readonly int _n;
    private readonly VkAllocator _stagingAllocator;
    private bool _disposedValue;
    private VkTexture _eccentricityTexture;
    private VkMappedMemory<Instance> _mapped;
    private readonly VkCommandPool _commandPool;
    private readonly VkCommandPool _commandPoolTransfer;
    private readonly VkBuffer<Instance> _stagingBuffer;
    private readonly VkFence _copyFence;
    public ParticleSystemGpu(VkContext ctx,
        VkDevice device,
        [MetadataFilter("Type", "DeviceLocal")]
        VkAllocator allocator,
        [MetadataFilter("Type", "HostVisible")]
        VkAllocator stagingAllocator,
        Instance[] initialData,
        SymplecticFactory symplecticFactory,
        SitnikovConfig config
    )
    {
        _ctx = ctx;
        _device = device;
        _allocator = allocator;
        _stagingAllocator = stagingAllocator;
        _fence = new VkFence(_ctx, _device);
        _copyFence = new VkFence(_ctx, _device);
        //_dataOnCpu = new Instance[initialData.Length];
        _e = config.e;
        _n = initialData.Length;

        var tracer = new Tracer<double>();
        var integrator =
            symplecticFactory(
                tracer.DV, tracer.DT);
        var (q, p) = integrator.Step(new Vector<double>([0]),
            new Vector<double>([0]), 1);

        var steps = tracer.GetReducedSteps(q[0], p[0]);
        var current = 0;
        var result = new List<float>();
        for (var i = 0; i < steps.Count; i++)
        {
            if ((current + 1) % 2 != (int)steps[i].Item2)
            {
                result.Add(0);
                current++;
            }

            result.Add((float)steps[i].Item1);
            current++;
        }

        if (current % 2 != 0) result.Add(0);

        var timeSteps = result.ToArray();

        Buffer = new VkBuffer<Instance>(_n,
            BufferUsageFlags.StorageBufferBit |
            BufferUsageFlags.TransferDstBit |
            BufferUsageFlags.TransferSrcBit, SharingMode.Exclusive,
            _allocator);

        _integratorBuffer = new VkBuffer<float>(timeSteps.Length,
            BufferUsageFlags.StorageBufferBit |
            BufferUsageFlags.TransferDstBit |
            BufferUsageFlags.TransferSrcBit, SharingMode.Exclusive,
            _allocator);
        using var stagingBufferIntegrator = new VkBuffer<float>(
            timeSteps.Length, BufferUsageFlags.TransferSrcBit,
            SharingMode.Exclusive, _stagingAllocator);

        using (var mapped =
               stagingBufferIntegrator.Map(0, timeSteps.Length))
        {
            for (var i = 0; i < timeSteps.Length; i++)
                mapped[i] = timeSteps[i];
        }

        _stagingBuffer = new VkBuffer<Instance>(_n,
            BufferUsageFlags.TransferSrcBit | BufferUsageFlags.TransferDstBit, SharingMode.Exclusive,
            _stagingAllocator);

        using (var mapped = _stagingBuffer.Map(0, _n))
        {
            for (var i = 0; i < _n; i++) mapped[i] = initialData[i];
        }

        var subresourceRange = new ImageSubresourceRange
        {
            AspectMask = ImageAspectFlags.ColorBit,
            BaseMipLevel = 0,
            LevelCount = 1,
            BaseArrayLayer = 0,
            LayerCount = 1,
        };

        using var shaderModule = new VkShaderModule(ctx, _device,
            "BoidsVulkan/shader_objects/base.comp.spv");
        DescriptorSetLayoutBinding[] bindings =
        [
            new()
            {
                Binding = 0,
                DescriptorType = DescriptorType.StorageImage,
                DescriptorCount = 1,
                StageFlags = ShaderStageFlags.ComputeBit,
            },
            new()
            {
                Binding = 1,
                DescriptorType = DescriptorType.StorageBuffer,
                DescriptorCount = 1,
                StageFlags = ShaderStageFlags.ComputeBit,
            },
            new()
            {
                Binding = 2,
                DescriptorType = DescriptorType.StorageBuffer,
                DescriptorCount = 1,
                StageFlags = ShaderStageFlags.ComputeBit,
            },
        ];
        using var layout = new VkSetLayout(_ctx, _device, bindings);
        PushConstantRange pushConstant =
            new(ShaderStageFlags.ComputeBit, 0,
                (uint)Marshal.SizeOf<UpdateData>());
        _computePipeline = new VkComputePipeline(ctx, device,
            new VkShaderInfo(shaderModule, "main"), [layout],
            [pushConstant]);

        _commandPool = new VkCommandPool(_ctx, _device,
            CommandPoolCreateFlags.ResetCommandBufferBit,
            _device.ComputeFamilyIndex);
        _commandPoolTransfer = new VkCommandPool(_ctx, _device,
            CommandPoolCreateFlags.ResetCommandBufferBit,
            _device.TransferFamilyIndex);
        _cmdBuffer = _commandPool.AllocateBuffers(
            CommandBufferLevel.Primary,
            1)[0];
        _cmdBufferCopy = _commandPoolTransfer.AllocateBuffers(
            CommandBufferLevel.Primary,
            1)[0];

        LoadTexture();

        using (var recording =
               _cmdBufferCopy.Begin(CommandBufferUsageFlags
                   .OneTimeSubmitBit))
        {
            recording.CopyBuffer(_stagingBuffer, Buffer, 0, 0,
                _stagingBuffer.Size);
            recording.CopyBuffer(stagingBufferIntegrator,
                _integratorBuffer, 0, 0,
                stagingBufferIntegrator.Size);
        }

        _cmdBufferCopy.Submit(device.TransferQueue,
            VkFence.NullHandle, [], []);
        _ctx.Api.QueueWaitIdle(device.TransferQueue);
        _cmdBufferCopy.Reset(CommandBufferResetFlags.None);

        _eccentricityView = new VkImageView(ctx, device,
            _eccentricityTexture.Image, default, subresourceRange);

        _descriptorPool = new VkDescriptorPool(ctx, device, [
            new DescriptorPoolSize(DescriptorType.StorageImage, 1),
            new DescriptorPoolSize(DescriptorType.StorageBuffer, 2),
        ], 1);

        _descriptorSet =
            _descriptorPool.AllocateDescriptors([layout])[0];
        var imageInfo = new DescriptorImageInfo(
            imageView: _eccentricityView.ImageView,
            imageLayout: ImageLayout.General);
        var bufferInfo1 =
            new DescriptorBufferInfo(Buffer.Buffer, 0, Buffer.Size);
        var bufferInfo2 = new DescriptorBufferInfo(
            _integratorBuffer.Buffer, 0, _integratorBuffer.Size);

        new VkDescriptorSetUpdater(ctx, device)
            .AppendWrite(_descriptorSet, 0,
                DescriptorType.StorageImage, [imageInfo])
            .AppendWrite(_descriptorSet, 1,
                DescriptorType.StorageBuffer,
                [bufferInfo1, bufferInfo2]).Update();

        _mapped = _stagingBuffer.Map(0, initialData.Length);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public async Task Update(double delta, double totalTime)
    {
        _fence.Reset();
        _cmdBuffer.Reset(CommandBufferResetFlags.None);
        using (var recording =
               _cmdBuffer.Begin(CommandBufferUsageFlags
                   .OneTimeSubmitBit))
        {
            ImageMemoryBarrier barrier = new()
            {
                SType = StructureType.ImageMemoryBarrier,
                DstAccessMask = AccessFlags.ShaderReadBit,
                SrcAccessMask = AccessFlags.None,
                OldLayout = ImageLayout.Undefined,
                NewLayout = ImageLayout.General,
                Image = _eccentricityTexture.Image.Image,
                SubresourceRange =
                    new ImageSubresourceRange(
                        ImageAspectFlags.ColorBit, 0, 1, 0, 1),
            };
            recording.PipelineBarrier(PipelineStageFlags.TopOfPipeBit,
                PipelineStageFlags.ComputeShaderBit, 0,
                imageMemoryBarriers: [barrier]);
            recording.BindPipeline(_computePipeline);
            recording.BindDescriptorSets(PipelineBindPoint.Compute,
                _computePipeline.PipelineLayout, [_descriptorSet]);

            var updateData = new UpdateData
            {
                N = _n,
                delta = (float)delta,
                T = (float)(totalTime % (Math.PI * 2)),
                ecc = (float)_e,
            };
            _ctx.Api.CmdPushConstants(_cmdBuffer.Buffer,
                _computePipeline.PipelineLayout,
                ShaderStageFlags.ComputeBit, 0,
                (uint)Marshal.SizeOf<UpdateData>(), ref updateData);
            _ctx.Api.CmdDispatch(_cmdBuffer.Buffer,
                (uint)Math.Ceiling(_n / 1024.0), 1, 1);
        }

        _cmdBuffer.Submit(_device.ComputeQueue, _fence, [], []);
        await _fence.WaitFor();
    }

    private void LoadTexture()
    {
        using var image = Image.Load<RgbaVector>("ecc.png");
        _eccentricityTexture = new VkTexture(ImageType.Type2D,
            new Extent3D((uint)image.Width, (uint)image.Height, 1), 1,
            1, Format.R32Sfloat, ImageTiling.Optimal,
            ImageLayout.Undefined,
            ImageUsageFlags.StorageBit |
            ImageUsageFlags.TransferDstBit,
            SampleCountFlags.Count1Bit, SharingMode.Exclusive,
            _allocator);

        Span<RgbaVector> span =
            new(new RgbaVector[image.Height * image.Width]);
        image.CopyPixelDataTo(span);
        var stagingBuffer = new VkBuffer<float>(
            image.Height * image.Width,
            BufferUsageFlags.TransferSrcBit, SharingMode.Exclusive,
            _stagingAllocator);
        using (var mapped =
               stagingBuffer.Map(0, image.Width * image.Height))
        {
            for (var i = 0; i < image.Height * image.Width; i++)
                mapped[i] = span[i].R;
        }

        using (var recording =
               _cmdBufferCopy.Begin(CommandBufferUsageFlags.None))
        {
            var region = new BufferImageCopy
            {
                BufferOffset = 0,
                BufferRowLength = 0,
                BufferImageHeight = 0,
                ImageSubresource =
                    new ImageSubresourceLayers
                    {
                        AspectMask =
                            ImageAspectFlags.ColorBit,
                        MipLevel = 0,
                        BaseArrayLayer = 0,
                        LayerCount = 1,
                    },
                ImageOffset = new Offset3D(0, 0, 0),
                ImageExtent = new Extent3D((uint)image.Width,
                    (uint)image.Height, 1),
            };

            ImageMemoryBarrier barrier = new()
            {
                SType = StructureType.ImageMemoryBarrier,
                DstAccessMask = AccessFlags.TransferWriteBit,
                SrcAccessMask = AccessFlags.None,
                OldLayout = ImageLayout.Undefined,
                NewLayout = ImageLayout.TransferDstOptimal,
                Image = _eccentricityTexture.Image.Image,
                SubresourceRange =
                    new ImageSubresourceRange(
                        ImageAspectFlags.ColorBit, 0, 1, 0, 1),
            };

            recording.PipelineBarrier(PipelineStageFlags.TopOfPipeBit,
                PipelineStageFlags.TransferBit, 0,
                imageMemoryBarriers: [barrier]);

            _ctx.Api.CmdCopyBufferToImage(_cmdBufferCopy.Buffer,
                stagingBuffer.Buffer,
                _eccentricityTexture.Image.Image,
                ImageLayout.TransferDstOptimal, 1, in region);
        }

        _cmdBufferCopy.Submit(_device.TransferQueue,
            VkFence.NullHandle, [], []);
        _ctx.Api.QueueWaitIdle(_device.TransferQueue);
        _cmdBufferCopy.Reset(CommandBufferResetFlags.None);
    }

    private void Dispose(bool disposing)
    {
        if (_disposedValue) return;
        if (disposing)
        {
            _stagingBuffer.Dispose();
            _commandPool.Dispose();
            _commandPoolTransfer.Dispose();
            _copyFence.Dispose();
            _fence.Dispose();
            Buffer.Dispose();
            _integratorBuffer.Dispose();
            _descriptorPool.Dispose();
            _eccentricityView.Dispose();
            _eccentricityTexture.Dispose();
            _computePipeline.Dispose();
        }

        _disposedValue = true;
    }
}