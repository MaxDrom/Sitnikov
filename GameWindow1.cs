using System.Runtime.InteropServices;
using Autofac.Features.AttributeFilters;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Silk.NET.Windowing;
using Sitnikov.BoidsVulkan;
using Sitnikov.BoidsVulkan.Builders;
using Sitnikov.BoidsVulkan.VkAllocatorSystem;

namespace Sitnikov;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct PushConstant
{
    public Vector2D<float> xrange;
    public Vector2D<float> yrange;
}


public sealed partial class GameWindow : IDisposable
{
    private readonly VkAllocator _allocator;
    private readonly VkCommandBuffer[] _buffers;

    private readonly ColorSpaceKHR _colorSpace;

    private readonly VkCommandPool _commandPool;
    private readonly VkCommandBuffer _copyBuffer;
    private readonly VkFence _copyFence;
    private readonly VkSemaphore _copyFinishedSemaphore;
    private readonly VkContext _ctx;
    private readonly VkDevice _device;
    private readonly VkFence[] _fences;

    private readonly Format _format;

    private const int FramesInFlight = 2;
    private readonly VkGraphicsPipeline _graphicsPipeline;
    private readonly VkSemaphore[] _imageAvailableSemaphores;
    private readonly VkBuffer<uint> _indexBuffer;
    private readonly VkBuffer<Instance> _instanceBuffer;
    private readonly VkBuffer<Instance> _quadInstanceBuffer;
    private readonly VkBuffer<Vertex> _quadVertexBuffer;
    private readonly VkSemaphore[] _renderFinishedSemaphores;
    private readonly VkRenderPass _renderPass;
    private readonly VkAllocator _stagingAllocator;
    private readonly VkSwapchainContext _swapchainCtx;
    private readonly VkBuffer<Vertex> _vertexBuffer;
    private bool _disposedValue;
    private int _fps;
    private VkFrameBuffer[] _frameBuffers;
    private int _frameIndex;
    private VkSwapchain _swapchain;
    private VkTexture _textureBuffer;
    private VkImageView _textureBufferView;
    private double _totalFrameTime;
    private List<VkImageView> _views;
    private IParticleSystem _particleSystem;
    private WindowOptions _windowOptions;
    private readonly SitnikovConfig _config;
    private readonly IWindow _window;
    private readonly VkCommandPool _commandPoolTransfer;

    public GameWindow(
        VkContext ctx, VkDevice device,
        DisplayFormat displayFormat, 
        [MetadataFilter("Type", "DeviceLocal")] VkAllocator allocator,
        [MetadataFilter("Type", "HostVisible")] VkAllocator stagingAllocator,
        IParticleSystem particleSystem,
        SitnikovConfig config,
        IWindow window)
    {
        _ctx =ctx;
        _device = device;
        _format = displayFormat.Format;
        _colorSpace = displayFormat.ColorSpace;
        _allocator = allocator;
        _stagingAllocator = stagingAllocator;
        _particleSystem = particleSystem;
        _config =  config;
        _windowOptions = displayFormat.WindowOptions;
        _window = window;
        _swapchainCtx = new VkSwapchainContext(_ctx, _device);
        _commandPool = new VkCommandPool(_ctx, _device,
            CommandPoolCreateFlags.ResetCommandBufferBit,
            _device.GraphicsFamilyIndex);
        
        _commandPoolTransfer = new VkCommandPool(_ctx, _device,
            CommandPoolCreateFlags.ResetCommandBufferBit,
            _device.GraphicsFamilyIndex);
        CreateSwapchain();
        CreateViews();
        
        _vertexBuffer = new VkBuffer<Vertex>(_vertices.Length,
            BufferUsageFlags.VertexBufferBit |
            BufferUsageFlags.TransferDstBit, SharingMode.Exclusive,
            _allocator);
        _indexBuffer = new VkBuffer<uint>(_indices.Length,
            BufferUsageFlags.IndexBufferBit |
            BufferUsageFlags.TransferDstBit, SharingMode.Exclusive,
            _allocator);
        

        var subPass = new VkSubpassInfo(PipelineBindPoint.Graphics, [
            new AttachmentReference
            {
                Attachment = 0,
                Layout = ImageLayout.ColorAttachmentOptimal,
            },
        ]);

        var attachmentDescription = new AttachmentDescription
        {
            Format = _swapchain.Images.First().Format,
            Samples = SampleCountFlags.Count1Bit,
            LoadOp = AttachmentLoadOp.Load,
            StoreOp = AttachmentStoreOp.Store,
            StencilLoadOp = AttachmentLoadOp.DontCare,
            StencilStoreOp = AttachmentStoreOp.DontCare,
            InitialLayout = ImageLayout.General,
            FinalLayout = ImageLayout.TransferSrcOptimal,
        };

        var dependency = new SubpassDependency
        {
            SrcSubpass = ~0u,
            DstSubpass = 0u,
            SrcStageMask =
                PipelineStageFlags.ColorAttachmentOutputBit,
            SrcAccessMask = AccessFlags.None,
            DstAccessMask = AccessFlags.ColorAttachmentWriteBit,
            DstStageMask = PipelineStageFlags
                .ColorAttachmentOutputBit,
        };

        _renderPass = new VkRenderPass(_ctx, _device, [subPass],
            [dependency], [attachmentDescription]);
        _graphicsPipeline = CreateGraphicsPipeline(_ctx, _device,
            _renderPass, _swapchain);

        CreateFrameBuffers();

        
        
        _copyBuffer = _commandPoolTransfer
            .AllocateBuffers(CommandBufferLevel.Primary, 1).First();
        _instanceBuffer = new VkBuffer<Instance>(_particleSystem.Buffer.Size,
            BufferUsageFlags.VertexBufferBit |
            BufferUsageFlags.TransferDstBit, SharingMode.Exclusive,
            _allocator);

        _quadVertexBuffer = new VkBuffer<Vertex>(_quadVertices.Length,
            BufferUsageFlags.VertexBufferBit |
            BufferUsageFlags.TransferDstBit, SharingMode.Exclusive,
            _allocator);
        _quadInstanceBuffer = new VkBuffer<Instance>(1,
            BufferUsageFlags.VertexBufferBit, SharingMode.Exclusive,
            _stagingAllocator);

        CopyDataToBuffer(_quadVertices, _quadVertexBuffer);
        CopyDataToBuffer(_vertices, _vertexBuffer);
        CopyDataToBuffer(_indices, _indexBuffer);
        

        _buffers =
            _commandPool.AllocateBuffers(CommandBufferLevel.Primary,
                _views.Count);

        for (var i = 0; i < _views.Count; i++)
            RecordBuffer(_buffers[i], i);

        _fences = new VkFence[FramesInFlight];
        _imageAvailableSemaphores = new VkSemaphore[FramesInFlight];
        _renderFinishedSemaphores = new VkSemaphore[FramesInFlight];
        _copyFence = new VkFence(_ctx, _device);
        _copyFinishedSemaphore = new VkSemaphore(_ctx, _device);
        for (var i = 0; i < FramesInFlight; i++)
        {
            _fences[i] = new VkFence(_ctx, _device);
            _imageAvailableSemaphores[i] =
                new VkSemaphore(_ctx, _device)
                {
                    Flag = PipelineStageFlags
                        .ColorAttachmentOutputBit,
                };
            _renderFinishedSemaphores[i] =
                new VkSemaphore(_ctx, _device);
        }


        
        _frameIndex = 0;
        _totalFrameTime = 0d;
        _fps = 0;
        _window.Render += OnRender;
        _window.Closing +=
            () => _ctx.Api.DeviceWaitIdle(_device.Device);
        _window.Resize += OnResize;
        _window.Update += x => OnUpdate(x).GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        Dispose(true);
    }

    private void Dispose(bool disposing)
    {
        if (_disposedValue) return;
        if (disposing)
        {
            _indexBuffer.Dispose();
            foreach (var framebuffer in _frameBuffers)
                framebuffer.Dispose();

            foreach (var view in _views) view.Dispose();

            _copyFence.Dispose();
            foreach (var fence in _fences) fence.Dispose();

            foreach (var sem in _imageAvailableSemaphores)
                sem.Dispose();

            foreach (var sem in _renderFinishedSemaphores)
                sem.Dispose();

            _commandPool.Dispose();
            _commandPoolTransfer.Dispose();
            _quadVertexBuffer.Dispose();
            _quadInstanceBuffer.Dispose();
            _instanceBuffer.Dispose();
            _copyFinishedSemaphore.Dispose();
            _graphicsPipeline.Dispose();
            _vertexBuffer.Dispose();
            _textureBufferView.Dispose();
            _textureBuffer.Dispose();
            _renderPass.Dispose();
            _swapchain.Dispose();
            _swapchainCtx.Dispose();
        }

        _disposedValue = true;
    }

    private void CopyDataToBuffer<T>(T[] data, VkBuffer<T> buffer)
        where T : unmanaged
    {
        using var stagingBuffer = new VkBuffer<T>(data.Length,
            BufferUsageFlags.TransferSrcBit, SharingMode.Exclusive,
            _stagingAllocator);
        using (var mapped = stagingBuffer.Map(0, data.Length))
        {
            for (var i = 0; i < data.Length; i++) mapped[i] = data[i];
        }

        using (var recording =
               _copyBuffer.Begin(CommandBufferUsageFlags
                   .OneTimeSubmitBit))
        {
            recording.CopyBuffer(stagingBuffer, buffer, 0, 0,
                stagingBuffer.Size);
        }

        _copyBuffer.Submit(_device.TransferQueue, VkFence.NullHandle,
            [], []);
        _ctx.Api.QueueWaitIdle(_device.TransferQueue);
    }

    private void CleanUpSwapchain()
    {
        foreach (var image in _views) image.Dispose();

        foreach (var framebuffer in _frameBuffers)
            framebuffer.Dispose();

        _swapchain.Dispose();
        _textureBuffer.Dispose();
    }

    private void CreateSwapchain()
    {
        unsafe
        {
            uint n;
            _ctx.SurfaceApi.GetPhysicalDeviceSurfacePresentModes(
                _device.PhysicalDevice, _ctx.Surface, &n, null);

            var presentModes = stackalloc PresentModeKHR[(int)n];
            _ctx.SurfaceApi.GetPhysicalDeviceSurfacePresentModes(
                _device.PhysicalDevice, _ctx.Surface, &n,
                presentModes);
            var presentMode = presentModes[0];
            var score = 0;
            Dictionary<PresentModeKHR, int> desired = new()
            {
                [PresentModeKHR.MailboxKhr] = 10,
                [PresentModeKHR.ImmediateKhr] = 5,
                [PresentModeKHR.FifoKhr] = 1,
            };
            for (var i = 0; i < n; i++)
                if (desired.TryGetValue(presentModes[i],
                        out var ss) && ss > score)
                {
                    presentMode = presentModes[i];
                    score = ss;
                }

            _ctx.SurfaceApi.GetPhysicalDeviceSurfaceCapabilities(
                _device.PhysicalDevice, _ctx.Surface,
                out var capabilities);
            _swapchain = new VkSwapchain(_ctx, _ctx.Surface,
                _swapchainCtx, [
                    _device.PresentFamilyIndex,
                    _device.GraphicsFamilyIndex,
                ], capabilities.MinImageCount + 1, _format,
                _colorSpace,
                new Extent2D((uint)_windowOptions.Size.X,
                    (uint)_windowOptions.Size.Y), presentMode,
                imageUsageFlags: ImageUsageFlags.TransferDstBit |
                                 ImageUsageFlags.ColorAttachmentBit);
        }
    }

    private void CreateViews()
    {
        _textureBuffer = new VkTexture(ImageType.Type2D,
            new Extent3D(_swapchain.Extent.Width,
                _swapchain.Extent.Height, 1), 1, 1, _format,
            ImageTiling.Optimal, ImageLayout.Undefined,
            ImageUsageFlags.ColorAttachmentBit |
            ImageUsageFlags.TransferSrcBit |
            ImageUsageFlags.SampledBit | ImageUsageFlags.StorageBit |
            ImageUsageFlags.TransferDstBit,
            SampleCountFlags.Count1Bit, SharingMode.Exclusive,
            _allocator);

        var stagingBuffer = new VkBuffer<byte>(_textureBuffer.Size,
            BufferUsageFlags.TransferSrcBit, SharingMode.Exclusive,
            _stagingAllocator);
        using (var mapped =
               stagingBuffer.Map(0, _textureBuffer.Size))
        {
            for (var i = 0u; i < mapped.Length; i++) mapped[i] = 0;
        }

        var copyRegion = new BufferImageCopy
        {
            BufferOffset = 0,
            BufferRowLength = 0,
            BufferImageHeight = 0,
            ImageSubresource =
                new ImageSubresourceLayers
                {
                    AspectMask = ImageAspectFlags.ColorBit,
                    MipLevel = 0,
                    BaseArrayLayer = 0,
                    LayerCount = 1,
                },
            ImageOffset = new Offset3D(0, 0, 0),
            ImageExtent = _textureBuffer.Extent,
        };
        var copyBuffer = _commandPoolTransfer
            .AllocateBuffers(CommandBufferLevel.Primary, 1).First();
        using (var recording =
               copyBuffer.Begin(CommandBufferUsageFlags
                   .OneTimeSubmitBit))
        {
            var barrier = new ImageMemoryBarrier
            {
                SType = StructureType.ImageMemoryBarrier,
                OldLayout = ImageLayout.Undefined,
                NewLayout = ImageLayout.General,
                Image = _textureBuffer.Image.Image,
                SubresourceRange = new ImageSubresourceRange
                {
                    AspectMask = ImageAspectFlags.ColorBit,
                    BaseMipLevel = 0,
                    LevelCount = 1,
                    BaseArrayLayer = 0,
                    LayerCount = 1,
                },
                SrcAccessMask = 0,
                DstAccessMask = AccessFlags.TransferWriteBit,
            };

            recording.PipelineBarrier(PipelineStageFlags.TopOfPipeBit,
                PipelineStageFlags.TransferBit, DependencyFlags.None,
                imageMemoryBarriers: [barrier]);

            _ctx.Api.CmdCopyBufferToImage(copyBuffer.Buffer,
                stagingBuffer.Buffer, _textureBuffer.Image.Image,
                ImageLayout.General,
                new ReadOnlySpan<BufferImageCopy>(ref copyRegion));
        }

        copyBuffer.Submit(_device.TransferQueue, VkFence.NullHandle,
            [], []);
        _ctx.Api.QueueWaitIdle(_device.TransferQueue);

        _views = [];
        var mapping = new ComponentMapping
        {
            A = ComponentSwizzle.Identity,
            B = ComponentSwizzle.Identity,
            R = ComponentSwizzle.Identity,
            G = ComponentSwizzle.Identity,
        };

        var subresourceRange = new ImageSubresourceRange
        {
            AspectMask = ImageAspectFlags.ColorBit,
            BaseArrayLayer = 0,
            BaseMipLevel = 0,
            LayerCount = 1,
            LevelCount = 1,
        };
        foreach (var image in _swapchain.Images)
            _views.Add(new VkImageView(_ctx, _device, image, mapping,
                subresourceRange));

        _textureBufferView = new VkImageView(_ctx, _device,
            _textureBuffer.Image, mapping, subresourceRange);
    }

    private void CreateFrameBuffers()
    {
        _frameBuffers =
        [
            new VkFrameBuffer(_ctx, _device, _renderPass,
                (uint)_windowOptions.Size.X,
                (uint)_windowOptions.Size.Y, 1, [_textureBufferView]),
        ];
    }

    private static VkGraphicsPipeline CreateGraphicsPipeline(
        VkContext ctx,
        VkDevice device,
        VkRenderPass renderPass,
        VkSwapchain swapchain)
    {
        using var vertModule = new VkShaderModule(ctx, device,
            "BoidsVulkan/shader_objects/base.vert.spv");
        using var fragModule = new VkShaderModule(ctx, device,
            "BoidsVulkan/shader_objects/base.frag.spv");

        var viewport = new Viewport
        {
            X = 0.0f,
            Y = 0.0f,
            Width = swapchain.Extent.Width,
            Height = swapchain.Extent.Height,
        };

        Rect2D scissor = new(new Offset2D(0, 0), swapchain.Extent);

        PipelineColorBlendAttachmentState colorBlend = new()
        {
            ColorWriteMask =
                ColorComponentFlags.RBit |
                ColorComponentFlags.GBit |
                ColorComponentFlags.BBit |
                ColorComponentFlags.ABit,
            BlendEnable = true,
            ColorBlendOp = BlendOp.Add,
            SrcAlphaBlendFactor = BlendFactor.One,
            DstAlphaBlendFactor = BlendFactor.Zero,
            AlphaBlendOp = BlendOp.Add,
            SrcColorBlendFactor = BlendFactor.SrcAlpha,
            DstColorBlendFactor = BlendFactor.OneMinusSrcAlpha,
        };
        var pushConstantRange = new PushConstantRange(
            ShaderStageFlags.VertexBit, 0,
            (uint)Marshal.SizeOf<PushConstant>());

        var pipeline = new GraphicsPipelineBuilder()
            .ForRenderPass(renderPass)
            .WithDynamicStages([
                DynamicState.Viewport, DynamicState.Scissor,
            ]).WithFixedFunctions(z =>
                z.ColorBlending([colorBlend]).Rasterization(y =>
                        y.WithSettings(PolygonMode.Fill,
                            CullModeFlags.BackBit,
                            FrontFace.Clockwise,
                            1.0f))
                    .Multisampling(SampleCountFlags.Count1Bit))
            .WithVertexInput(z => z
                .AddBindingFor<Vertex>(0, VertexInputRate.Vertex)
                .AddBindingFor<Instance>(1, VertexInputRate.Instance))
            .WithInputAssembly(PrimitiveTopology.TriangleList)
            .WithViewportAndScissor(viewport, scissor)
            .WithPipelineStages(z =>
                z.Vertex(new VkShaderInfo(vertModule, "main"))
                    .Fragment(new VkShaderInfo(fragModule, "main")))
            .WithLayouts([], [pushConstantRange])
            .Build(ctx, device, 0);
        return pipeline;
    }
}