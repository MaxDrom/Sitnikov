using System.Runtime.InteropServices;
using Silk.NET.Core.Native;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Windowing;
using VkAllocatorSystem;
using BoidsVulkan;
namespace SymplecticIntegrators;

public partial class GameWindow : IDisposable
{
    VkSwapchain swapchain;
    List<VkImageView> views;
    VkContext ctx;
    VkSwapchainContext swapchainCtx;
    VkDevice device;
    VkFrameBuffer[] framebuffers;
    VkCommandBuffer[] buffers;
    VkCommandBuffer copyBuffer;
    VkRenderPass renderPass;
    VkFence[] fences;
    VkFence copyFence;
    VkSemaphore[] imageAvailableSemaphores;
    VkSemaphore[] renderFinishedSemaphores;
    VkSemaphore copyFinishedSemaphore;
    StupidAllocator allocator;
    StupidAllocator staggingAllocator;
    VkBuffer vertexBuffer;
    VkBuffer indexBuffer;
    VkBuffer instanceBuffer;
    VkBuffer quadVertexBuffer;
    // VkBuffer quadInstanceBuffer;
    VkCommandPool commandPool;
    VkGraphicsPipeline graphicsPipeline;
    VkBuffer staggingVertex;
    VkTexture textureBuffer;
    VkImageView textureBufferView;
    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                indexBuffer.Dispose();
                foreach (var framebuffer in framebuffers)
                    framebuffer.Dispose();
                foreach (var view in views)
                    view.Dispose();
                copyFence.Dispose();
                foreach (var fence in fences)
                    fence.Dispose();
                foreach (var sem in imageAvailableSemaphores)
                    sem.Dispose();
                foreach (var sem in renderFinishedSemaphores)
                    sem.Dispose();

                quadVertexBuffer.Dispose();
                instanceBuffer.Dispose();
                copyFinishedSemaphore.Dispose();
                staggingVertex?.Dispose();
                graphicsPipeline.Dispose();
                commandPool.Dispose();
                vertexBuffer.Dispose();
                textureBufferView.Dispose();
                textureBuffer.Dispose();
                allocator.Dispose();
                staggingAllocator.Dispose();
                renderPass.Dispose();
                swapchain.Dispose();
                device.Dispose();
                swapchainCtx.Dispose();
                ctx.Dispose();
            }

            disposedValue = true;
        }
    }

    WindowOptions windowOptions;
    int frameIndex;
    int framesInFlight = 2;
    double totalFrametime;
    int FPS;
    IWindow window;
    private bool disposedValue;
    Format format = Format.R16G16B16A16Sfloat;
    ColorSpaceKHR colorSpace = ColorSpaceKHR.SpaceSrgbNonlinearKhr;

    public GameWindow(WindowOptions windowOptions, SymplecticIntegrator<double, Vector<double>> integrator, SitnikovConfig config)
    {
        this.integrator = integrator;
        this.windowOptions = windowOptions;
        window = Window.Create(windowOptions);
        var gridX = config.SizeX;
        var gridY = config.SizeY;
        instances = new Instance[gridX * gridY + 1];
        var dx = (float)(config.RangeX.Item2 - config.RangeX.Item1);
        var dy = (float)(config.RangeY.Item2 - config.RangeY.Item1);
        for (var xx = 0; xx < gridX; xx++)
        {
            for (var yy = 0; yy < gridY; yy++)
            {
                instances[xx + yy * gridX] = new()
                {
                    position = new Vector2D<float>((float)config.RangeX.Item1
                                    + xx / (gridX - 1f) * dx, (float)config.RangeY.Item1
                                        + yy / (gridY - 1f) * dy),
                    color = new Vector4D<float>(xx / (gridX - 1f), yy / (gridY - 1f), 1f, 1f),
                    offset = new Vector2D<float>(0, 1)
                };
            }
        }
        instances[gridX * gridY] = new()
        {
            position = Vector2D<float>.Zero,
            color = new Vector4D<float>(0, 0, 0, 1.0f),
            offset = new Vector2D<float>(0, 1)
        };
        window.Initialize();
        if (window.VkSurface is null)
        {
            throw new Exception("Windowing platform doesn't support Vulkan.");
        }
        string[] extensions;
        unsafe
        {
            var pp = window.VkSurface.GetRequiredExtensions(out uint count);
            extensions = new string[count];
            SilkMarshal.CopyPtrToStringArray((nint)pp, extensions);
        }
        ctx = new VkContext(window, extensions);
        var physicalDevice = ctx.Api.GetPhysicalDevices(ctx.Instance).ToArray()[0];
        string deviceName;
        unsafe
        {
            var propery = ctx.Api.GetPhysicalDeviceProperty(physicalDevice);
            deviceName = SilkMarshal.PtrToString((nint)propery.DeviceName)!;
        }
        Console.WriteLine(deviceName);
        device = new VkDevice(ctx, physicalDevice, [], [KhrSwapchain.ExtensionName]);
        swapchainCtx = new VkSwapchainContext(ctx, device);

        SurfaceFormatKHR[] formats;
        unsafe
        {
            uint nn;
            ctx.SurfaceApi.GetPhysicalDeviceSurfaceFormats(physicalDevice, ctx.Surface, &nn, null);
            formats = new SurfaceFormatKHR[nn];
            fixed (SurfaceFormatKHR* pformat = formats)
            {
                ctx.SurfaceApi.GetPhysicalDeviceSurfaceFormats(physicalDevice, ctx.Surface, &nn, pformat);
            }

            foreach (var fformat in formats)
                if (fformat.Format == Format.R16G16B16A16Sfloat)
                {
                    colorSpace = fformat.ColorSpace;
                    break;
                }

            //format = formats[0].Format;
            //colorSpace = formats[0].ColorSpace;

        }

        CreateSwapchain();
        commandPool = new VkCommandPool(ctx, device,
                            CommandPoolCreateFlags.ResetCommandBufferBit,
                            device.GraphicsFamilyIndex);
        staggingAllocator = new StupidAllocator(ctx, device,
                                            MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit,
                                            MemoryHeapFlags.DeviceLocalBit);
        allocator = new StupidAllocator(ctx, device, MemoryPropertyFlags.None, MemoryHeapFlags.DeviceLocalBit);



        CreateViews();
        vertexBuffer = new VkBuffer((ulong)vertices.Length * ((ulong)Marshal.SizeOf<Vertex>()), BufferUsageFlags.VertexBufferBit | BufferUsageFlags.TransferDstBit, SharingMode.Exclusive, allocator);
        indexBuffer = new VkBuffer((ulong)indices.Length * sizeof(uint), BufferUsageFlags.IndexBufferBit | BufferUsageFlags.TransferDstBit, SharingMode.Exclusive, allocator);
        var subpass1 = new VkSubpassInfo(PipelineBindPoint.Graphics, [
                    new AttachmentReference()
            {
                Attachment = 0,
                Layout = ImageLayout.ColorAttachmentOptimal
            }
                ]);

        var attachmentDescription = new AttachmentDescription()
        {
            Format = swapchain.Images.First().Format,
            Samples = SampleCountFlags.Count1Bit,
            LoadOp = AttachmentLoadOp.Load,
            StoreOp = AttachmentStoreOp.Store,
            StencilLoadOp = AttachmentLoadOp.DontCare,
            StencilStoreOp = AttachmentStoreOp.DontCare,
            InitialLayout = ImageLayout.General,
            FinalLayout = ImageLayout.TransferSrcOptimal
        };

        var dependency = new SubpassDependency()
        {
            SrcSubpass = ~0u,
            DstSubpass = 0u,
            SrcStageMask = PipelineStageFlags.ColorAttachmentOutputBit,
            SrcAccessMask = AccessFlags.None,
            DstAccessMask = AccessFlags.ColorAttachmentWriteBit,
            DstStageMask = PipelineStageFlags.ColorAttachmentOutputBit
        };

        renderPass = new VkRenderPass(ctx, device, [subpass1], [dependency], [attachmentDescription]);
        graphicsPipeline = CreateGraphicsPipeline(ctx, device, renderPass, windowOptions, swapchain);
        CreateFramebuffers();

        copyBuffer = commandPool.AllocateBuffers(CommandBufferLevel.Primary, 1).First();
        instanceBuffer = new VkBuffer((ulong)instances.Length * (ulong)Marshal.SizeOf<Instance>(), BufferUsageFlags.VertexBufferBit | BufferUsageFlags.TransferDstBit,
                                        SharingMode.Exclusive, allocator);

        quadVertexBuffer = new VkBuffer((ulong)quadVertices.Length * (ulong)Marshal.SizeOf<Vertex>(),
                                        BufferUsageFlags.VertexBufferBit | BufferUsageFlags.TransferDstBit, SharingMode.Exclusive, allocator);
        // quadInstanceBuffer = new VkBuffer((ulong)instances1.Length * (ulong)Marshal.SizeOf<Instance>(),
        // BufferUsageFlags.VertexBufferBit | BufferUsageFlags.TransferDstBit, SharingMode.Exclusive, allocator);

        CopyDataToBuffer(quadVertices, quadVertexBuffer);
        CopyDataToBuffer(vertices, vertexBuffer);
        CopyDataToBuffer(indices, indexBuffer);
        CopyDataToBuffer(instances, instanceBuffer);
        // CopyDataToBuffer(instances1, quadInstanceBuffer);

        buffers = commandPool.AllocateBuffers(CommandBufferLevel.Primary, views.Count);

        for (var i = 0; i < views.Count; i++)
        {
            RecordBuffer(buffers[i], i);
        }


        fences = new VkFence[framesInFlight];
        imageAvailableSemaphores = new VkSemaphore[framesInFlight];
        renderFinishedSemaphores = new VkSemaphore[framesInFlight];
        copyFence = new VkFence(ctx, device);
        copyFinishedSemaphore = new VkSemaphore(ctx, device);
        for (var i = 0; i < framesInFlight; i++)
        {
            fences[i] = new VkFence(ctx, device);
            imageAvailableSemaphores[i] = new VkSemaphore(ctx, device)
            {
                Flag = PipelineStageFlags.ColorAttachmentOutputBit
            };
            renderFinishedSemaphores[i] = new VkSemaphore(ctx, device);
        }



        frameIndex = 0;
        totalFrametime = 0d;
        FPS = 0;
        window.Render += OnRender;
        window.Closing += () => ctx.Api.DeviceWaitIdle(device.Device);
        window.Resize += OnResize;
        window.Update += (x) => OnUpdate(x).GetAwaiter().GetResult();
    }


    void CopyDataToBuffer<T>(T[] data, VkBuffer buffer)
        where T : unmanaged
    {
        using var staggingBuffer = new VkBuffer((ulong)data.Length * (ulong)Marshal.SizeOf<T>(), BufferUsageFlags.TransferSrcBit, SharingMode.Exclusive, staggingAllocator);
        using (var mapped = staggingBuffer.Map<T>(0, data.Length))
        {
            for (var i = 0; i < data.Length; i++)
                mapped[i] = data[i];
        }

        using (var recording = copyBuffer.Begin(CommandBufferUsageFlags.OneTimeSubmitBit))
        {
            recording.CopyBuffer(staggingBuffer, buffer, 0, 0, staggingBuffer.Size);
        }
        copyBuffer.Submit(device.TransferQueue, VkFence.NullHandle, [], []);
        ctx.Api.QueueWaitIdle(device.TransferQueue);
    }

    void CleanUpSwapchain()
    {
        foreach (var image in views)
            image.Dispose();
        foreach (var framebuffer in framebuffers)
            framebuffer.Dispose();

        swapchain.Dispose();
        textureBuffer.Dispose();
    }

    void CreateSwapchain()
    {
        unsafe
        {
            uint n;
            ctx.SurfaceApi.GetPhysicalDeviceSurfacePresentModes(device.PhysicalDevice, ctx.Surface, &n, null);

            var presentModes = stackalloc PresentModeKHR[(int)n];
            ctx.SurfaceApi.GetPhysicalDeviceSurfacePresentModes(device.PhysicalDevice, ctx.Surface, &n, presentModes);
            var presentMode = presentModes[0];
            var score = 0;
            Dictionary<PresentModeKHR, int> desired = new();
            desired[PresentModeKHR.MailboxKhr] = 10;
            desired[PresentModeKHR.ImmediateKhr] = 5;
            desired[PresentModeKHR.FifoKhr] = 20;
            for (var i = 0; i < n; i++)
            {
                if (desired.TryGetValue(presentModes[i], out var ss) && ss > score)
                {
                    presentMode = presentModes[i];
                    score = ss;
                }
            }
            ctx.SurfaceApi.GetPhysicalDeviceSurfaceCapabilities(device.PhysicalDevice, ctx.Surface, out var capabilities);
            swapchain = new VkSwapchain(ctx, ctx.Surface, swapchainCtx, [device.PresentFamilyIndex, device.GraphicsFamilyIndex], capabilities.MinImageCount + 1,
                                    format,
                                    colorSpace,
                                    new Extent2D((uint)windowOptions.Size.X, (uint)windowOptions.Size.Y),
                                    presentMode,
                                    imageUsageFlags: ImageUsageFlags.TransferDstBit | ImageUsageFlags.ColorAttachmentBit);
        }
    }

    void CreateViews()
    {
        textureBuffer = new VkTexture(ImageType.Type2D, new Extent3D(swapchain.Extent.Width, swapchain.Extent.Height, 1),
                                    1, 1, format, ImageTiling.Optimal, ImageLayout.Undefined, ImageUsageFlags.ColorAttachmentBit | ImageUsageFlags.TransferSrcBit | ImageUsageFlags.SampledBit | ImageUsageFlags.StorageBit | ImageUsageFlags.TransferDstBit, SampleCountFlags.Count1Bit, SharingMode.Exclusive, allocator);

        var staggingBuffer = new VkBuffer(textureBuffer.Size, BufferUsageFlags.TransferSrcBit, SharingMode.Exclusive, staggingAllocator);
        using (var mapped = staggingBuffer.Map<byte>(0, textureBuffer.Size))
        {
            for (var i = 0u; i < mapped.Length; i++)
            {
                mapped[i] = 0;
            }
        }
        var copyRegion = new BufferImageCopy
        {
            BufferOffset = 0,
            BufferRowLength = 0, // 0 = packed data
            BufferImageHeight = 0,

            ImageSubresource = new ImageSubresourceLayers
            {
                AspectMask = ImageAspectFlags.ColorBit,
                MipLevel = 0,
                BaseArrayLayer = 0,
                LayerCount = 1
            },

            ImageOffset = new Offset3D(0, 0, 0),
            ImageExtent = textureBuffer.Extent
        };
        var copyBuffer = commandPool.AllocateBuffers(CommandBufferLevel.Primary, 1).First();
        using (var recording = copyBuffer.Begin(CommandBufferUsageFlags.OneTimeSubmitBit))
        {
            var barrier = new ImageMemoryBarrier
            {
                SType = StructureType.ImageMemoryBarrier,
                OldLayout = ImageLayout.Undefined,
                NewLayout = ImageLayout.General,

                Image = textureBuffer.Image.Image,
                SubresourceRange = new ImageSubresourceRange
                {
                    AspectMask = ImageAspectFlags.ColorBit,
                    BaseMipLevel = 0,
                    LevelCount = 1,
                    BaseArrayLayer = 0,
                    LayerCount = 1
                },
                SrcAccessMask = 0,
                DstAccessMask = AccessFlags.TransferWriteBit
            };
            unsafe
            {
                ctx.Api.CmdPipelineBarrier(copyBuffer.InternalBuffer, PipelineStageFlags.TopOfPipeBit, PipelineStageFlags.TransferBit, 0, 0, null, 0, null, 1, ref barrier);
            }
            ctx.Api.CmdCopyBufferToImage(copyBuffer.InternalBuffer, staggingBuffer.Buffer, textureBuffer.Image.Image, ImageLayout.General, new ReadOnlySpan<BufferImageCopy>(ref copyRegion));
        }
        copyBuffer.Submit(device.TransferQueue, VkFence.NullHandle, [], []);
        ctx.Api.QueueWaitIdle(device.TransferQueue);

        views = new List<VkImageView>();
        var mapping = new ComponentMapping();
        mapping.A = ComponentSwizzle.Identity;
        mapping.B = ComponentSwizzle.Identity;
        mapping.R = ComponentSwizzle.Identity;
        mapping.G = ComponentSwizzle.Identity;

        var subresourceRange = new ImageSubresourceRange();
        subresourceRange.AspectMask = ImageAspectFlags.ColorBit;
        subresourceRange.BaseArrayLayer = 0;
        subresourceRange.BaseMipLevel = 0;
        subresourceRange.LayerCount = 1;
        subresourceRange.LevelCount = 1;
        foreach (var image in swapchain.Images)
            views.Add(new VkImageView(ctx, device, image, mapping, subresourceRange));
        textureBufferView = new VkImageView(ctx, device, textureBuffer.Image, mapping, subresourceRange);
    }

    void CreateFramebuffers()
    {

        framebuffers = [new VkFrameBuffer(ctx, device, renderPass, (uint)windowOptions.Size.X, (uint)windowOptions.Size.Y, 1, [textureBufferView])]; //views.Select(z => new VkFrameBuffer(ctx, device,
                                                                                                                                                     //renderPass, (uint)windowOptions.Size.X, (uint)windowOptions.Size.Y, 1, [z])).ToArray();

    }





    unsafe VkGraphicsPipeline CreateGraphicsPipeline(VkContext ctx, VkDevice device,
                    VkRenderPass renderPass, WindowOptions windowOptions, VkSwapchain swapchain)
    {

        using var vertModule = new VkShaderModule(ctx, device, "BoidsVulkan/shader_objects/base.vert.spv");
        using var fragModule = new VkShaderModule(ctx, device, "BoidsVulkan/shader_objects/base.frag.spv");
        var shaderStages = new Dictionary<ShaderStageFlags, VkShaderInfo>
        {
            [ShaderStageFlags.VertexBit] = new VkShaderInfo(vertModule, "main"),
            [ShaderStageFlags.FragmentBit] = new VkShaderInfo(fragModule, "main")
        };

        using var pipelineLayout = new VkPiplineLayout(ctx, device, []);
        var (attributes, binding) = vertices[0].CreateVertexInputDescription(0);
        var (attributes2, binding2) = instances[0].CreateVertexInputDescription(1, VertexInputRate.Instance);
        var allattribs = attributes.ToList();
        allattribs.AddRange(attributes2);
        using var pipelineVertexInput = new VkVertexInputStateCreateInfo([binding, binding2], allattribs);
        var inputAssemblyState = new PipelineInputAssemblyStateCreateInfo()
        {
            SType = StructureType.PipelineInputAssemblyStateCreateInfo,
            Topology = PrimitiveTopology.TriangleList,
            PrimitiveRestartEnable = false
        };

        var viewport = new Viewport()
        {
            X = 0.0f,
            Y = 0.0f,
            Width = swapchain.Extent.Width,
            Height = swapchain.Extent.Height
        };

        PipelineRasterizationStateCreateInfo pipelineRasterizationState = new()
        {
            SType = StructureType.PipelineRasterizationStateCreateInfo,
            DepthClampEnable = false,
            RasterizerDiscardEnable = false,
            PolygonMode = PolygonMode.Fill,
            LineWidth = 1.0f,
            CullMode = CullModeFlags.BackBit,
            FrontFace = FrontFace.Clockwise,
            DepthBiasEnable = false,
            DepthBiasConstantFactor = 0.0f,
            DepthBiasClamp = 0.0f,
            DepthBiasSlopeFactor = 0.0f
        };

        PipelineMultisampleStateCreateInfo multisampling = new()
        {
            SType = StructureType.PipelineMultisampleStateCreateInfo,
            SampleShadingEnable = false,
            RasterizationSamples = SampleCountFlags.Count1Bit,
            MinSampleShading = 1.0f,
            PSampleMask = null,
            AlphaToCoverageEnable = false,
            AlphaToOneEnable = false
        };

        PipelineColorBlendAttachmentState colorBlend = new()
        {
            ColorWriteMask = ColorComponentFlags.RBit | ColorComponentFlags.GBit | ColorComponentFlags.BBit | ColorComponentFlags.ABit,
            BlendEnable = true,
            ColorBlendOp = BlendOp.Add,
            SrcAlphaBlendFactor = BlendFactor.One,
            DstAlphaBlendFactor = BlendFactor.Zero,
            AlphaBlendOp = BlendOp.Add,
            SrcColorBlendFactor = BlendFactor.SrcAlpha,
            DstColorBlendFactor = BlendFactor.OneMinusSrcAlpha

        };

        PipelineColorBlendStateCreateInfo colorBlendInfo = new()
        {
            SType = StructureType.PipelineColorBlendStateCreateInfo,
            LogicOpEnable = false,
            AttachmentCount = 1,
            PAttachments = &colorBlend
        };

        Rect2D scissor = new(new Offset2D(0, 0), swapchain.Extent);
        return new VkGraphicsPipeline(ctx, device,
            shaderStages,
            pipelineLayout,
            renderPass,
            0,
            [DynamicState.Viewport, DynamicState.Scissor],
            pipelineVertexInput,
            viewport,
            scissor,
            inputAssemblyState,
            &pipelineRasterizationState,
            &multisampling,
            &colorBlendInfo
            );
    }



    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}