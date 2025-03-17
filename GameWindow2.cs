using System.Runtime.InteropServices;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Silk.NET.Windowing;
using BoidsVulkan;
using System.Numerics;
namespace SymplecticIntegrators;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct Instance : IVertexData<Instance>
{
    [VertexAttributeDescription(2, Format.R32G32Sfloat)]
    public Vector2D<float> position;
    [VertexAttributeDescription(4, Format.R32G32Sfloat)]
    public Vector2D<float> offset;

    [VertexAttributeDescription(3, Format.R32G32B32A32Sfloat)]
    public Vector4D<float> color;
}

public partial class GameWindow
{
    SymplecticIntegrator<double, Vector<double>> integrator;
    static Instance[] instances;

    Vertex[] vertices = [
        new() { position = new Vector2D<float>(-0.003f, -0.003f), color = new Vector4D<float>(1.0f, 1.0f, 1.0f, 1.0f) },

        new() { position = new Vector2D<float>(0.003f, -0.003f), color = new Vector4D<float>(1.0f, 1.0f, 1.0f, 1.0f) },

        new() { position = new Vector2D<float>(0.003f, 0.003f), color = new Vector4D<float>(1.0f, 1.0f, 1.0f, 1.0f) },

        new() { position = new Vector2D<float>(-0.003f, 0.003f), color = new Vector4D<float>(1.0f, 1.0f, 1.0f, 1.0f) },
    ];

    Vertex[] quadVertices = [
        new() { position = new Vector2D<float>(-1f, -1f), color = new Vector4D<float>(0.0f, 0.0f, 0.0f, 0.0001f) },

        new() { position = new Vector2D<float>(1f, -1f), color = new Vector4D<float>(0.0f, 0.0f, 0.0f, 0.0001f) },

        new() { position = new Vector2D<float>(1f, 1f), color = new Vector4D<float>(0.0f, 0.0f, 0.0f, 0.0001f) },

        new() { position = new Vector2D<float>(-1f, 1f), color = new Vector4D<float>(0.0f, 0.0f, 0.0f, 0.0001f) },
    ];

    uint[] indices = [0, 1, 2, 2, 3, 0];

    public void Run()
    {
        window.Run();
    }

    void RecordBuffer(VkCommandBuffer buffer, int imageIndex)
    {
        Viewport viewport =
            new() { X = 0.0f, Y = 0.0f, Width = swapchain.Extent.Width, Height = swapchain.Extent.Height };
        Rect2D scissor = new(new Offset2D(0, 0), swapchain.Extent);
        buffer.Reset(CommandBufferResetFlags.None);
        using (var recording = buffer.Begin(CommandBufferUsageFlags.None))
        {
            var subresourceRange = new ImageSubresourceRange(ImageAspectFlags.ColorBit, 0, 1, 0, 1);

            ImageMemoryBarrier bb =
                new() { SType = StructureType.ImageMemoryBarrier,  OldLayout = ImageLayout.Undefined,
                        NewLayout = ImageLayout.General,           SrcAccessMask = AccessFlags.None,
                        DstAccessMask = AccessFlags.MemoryReadBit, Image = textureBuffer.Image.Image,
                        SubresourceRange = subresourceRange };

            recording.PipelineBarrier(PipelineStageFlags.TopOfPipeBit, PipelineStageFlags.ColorAttachmentOutputBit,
                                      DependencyFlags.None, imageMemoryBarriers: [bb]);

            using (var renderRecording = recording.BeginRenderPass(renderPass, framebuffers[0], scissor))
            {
                var pushConstant2 =
                    new PushConstant() { xrange = new Vector2D<float>(-1, 1), yrange = new Vector2D<float>(-1, 1) };

                recording.BindPipline(graphicsPipeline);
                ctx.Api.CmdPushConstants(buffer.InternalBuffer, graphicsPipeline.PipelineLayout,
                                         ShaderStageFlags.VertexBit, 0, (uint)Marshal.SizeOf<PushConstant>(),
                                         ref pushConstant2);

                recording.BindVertexBuffers(0, [quadVertexBuffer, quadInstanceBuffer], [0, 0]);
                recording.BindIndexBuffer(indexBuffer, 0, IndexType.Uint32);
                renderRecording.SetViewport(ref viewport);
                renderRecording.SetScissor(ref scissor);
                // renderRecording.SetBlendConstant(blendFactors);
                renderRecording.DrawIndexed((uint)indices.Length, 1u, 0, 0);
                var pushConstant =
                    new PushConstant() { xrange = new Vector2D<float>((float)config.Visualization.RangeX.Item1,
                                                                      (float)config.Visualization.RangeX.Item2),
                                         yrange = new Vector2D<float>((float)config.Visualization.RangeY.Item1,
                                                                      (float)config.Visualization.RangeY.Item2) };
                ctx.Api.CmdPushConstants(buffer.InternalBuffer, graphicsPipeline.PipelineLayout,
                                         ShaderStageFlags.VertexBit, 0, (uint)Marshal.SizeOf<PushConstant>(),
                                         ref pushConstant);
                recording.BindVertexBuffers(0, [vertexBuffer, instanceBuffer], [0, 0]);
                renderRecording.SetViewport(ref viewport);
                renderRecording.SetScissor(ref scissor);
                renderRecording.DrawIndexed((uint)indices.Length, (uint)instances.Length - 1, 0, 0);
            }

            ImageCopy region =
                new ImageCopy() { SrcOffset = new Offset3D(0, 0, 0), DstOffset = new Offset3D(0, 0, 0),
                                  Extent = new Extent3D(swapchain.Extent.Width, swapchain.Extent.Height, 1),
                                  SrcSubresource = new ImageSubresourceLayers(ImageAspectFlags.ColorBit, 0, 0, 1),
                                  DstSubresource = new ImageSubresourceLayers(ImageAspectFlags.ColorBit, 0, 0, 1) };

            ImageMemoryBarrier[] barriers = [
                new() { SType = StructureType.ImageMemoryBarrier, OldLayout = ImageLayout.Undefined,
                        NewLayout = ImageLayout.TransferSrcOptimal, SrcAccessMask = AccessFlags.ColorAttachmentWriteBit,
                        DstAccessMask = AccessFlags.TransferReadBit, Image = textureBuffer.Image.Image,
                        SubresourceRange = subresourceRange },

                new() { SType = StructureType.ImageMemoryBarrier, OldLayout = ImageLayout.Undefined,
                        NewLayout = ImageLayout.TransferDstOptimal, SrcAccessMask = AccessFlags.None,
                        DstAccessMask = AccessFlags.TransferWriteBit, Image = swapchain.Images[imageIndex].Image,
                        SubresourceRange = subresourceRange }

            ];

            recording.PipelineBarrier(PipelineStageFlags.ColorAttachmentOutputBit, PipelineStageFlags.TransferBit,
                                      DependencyFlags.None, imageMemoryBarriers: barriers);

            ctx.Api.CmdCopyImage(buffer.InternalBuffer, textureBuffer.Image.Image, ImageLayout.TransferSrcOptimal,
                                 swapchain.Images[imageIndex].Image, ImageLayout.TransferDstOptimal, 1, ref region);

            ImageMemoryBarrier barrier2 =
                new() { SType = StructureType.ImageMemoryBarrier, OldLayout = ImageLayout.TransferDstOptimal,
                        NewLayout = ImageLayout.PresentSrcKhr,    SrcAccessMask = AccessFlags.TransferWriteBit,
                        DstAccessMask = AccessFlags.None,         Image = swapchain.Images[imageIndex].Image,
                        SubresourceRange = subresourceRange };

            recording.PipelineBarrier(PipelineStageFlags.TransferBit, PipelineStageFlags.BottomOfPipeBit,
                                      DependencyFlags.None, imageMemoryBarriers: [barrier2]);
        }
    }

    void OnResize(Vector2D<int> x)
    {
        windowOptions.Size = x;
        ctx.Api.DeviceWaitIdle(device.Device);
        CleanUpSwapchain();

        CreateSwapchain();
        CreateViews();
        CreateFramebuffers();

        for (var i = 0; i < views.Count; i++)
        {
            RecordBuffer(buffers[i], i);
        }
    }
    double totalTime = 0;
    Vector2D<float> ComplexMul(Vector2D<float> a, Vector2D<float> b)
    {
        return new Vector2D<float>(a.X * b.X - a.Y * b.Y, a.X * b.Y + b.X * a.Y);
    }

    Vector2D<float> Step(Vector2D<float> pos, double frametime, double totalTime)
    {
        var (xmin, xmax) = config.Visualization.RangeX;
        var (ymin, ymax) = config.Visualization.RangeY;
        var (q, p) =
            integrator.Step(new Vector<double>([pos[0], totalTime]), new Vector<double>([pos[1], 0]), frametime);
        return new Vector2D<float>((float)q[0], (float)p[0]);
    }

    bool firstrun = true;
    uint imageIndex;
    Task[] taskList;
    VkMappedMemory<Instance> mapped;
    double totaltime;
    async Task OnUpdate(double frametime)
    {
        int maxFPS = 500;
        double minFrametime = 1.0 / (double)maxFPS;
        if (frametime < minFrametime)
        {
            await Task.Delay((int)(1000 * minFrametime - 1000 * frametime));
            frametime = minFrametime;
        }

        if (firstrun)
        {
            firstrun = true;
            copyBuffer.Reset(CommandBufferResetFlags.None);
            using (var recording = copyBuffer.Begin(CommandBufferUsageFlags.SimultaneousUseBit))
            {
                recording.CopyBuffer(particleSystem.Buffer, instanceBuffer, 0, 0, particleSystem.Buffer.Size);
            }
        }

        using (var mapped = quadInstanceBuffer.Map(0, 1))
        {
            mapped[0] = new() {

                position = Vector2D<float>.Zero,
                color = new Vector4D<float>(0, 0, 0, 1.0f - (float)Math.Exp(-config.Visualization.Fade * frametime)),
                offset = new Vector2D<float>(0, 1)
            };
        };

        totalFrametime += frametime;
        FPS++;
        if (totalFrametime >= 1)
        {
            window.Title = $"FPS: {FPS / totalFrametime}";
            totalFrametime = 0;
            FPS = 0;
        }

        await particleSystem.Update(frametime, totaltime);
        totaltime += frametime;
        copyFence.Reset();
        copyBuffer.Submit(device.GraphicsQueue, copyFence, waitSemaphores: [], signalSemaphores: []);
        await copyFence.WaitFor();
    }
    void OnRender(double frametime)
    {
        fences[frameIndex].WaitFor().GetAwaiter().GetResult();
        if (swapchain.AcquireNextImage(device, imageAvailableSemaphores[frameIndex], out imageIndex) ==
            Result.ErrorOutOfDateKhr)
            return;
        fences[frameIndex].Reset();
        buffers[imageIndex].Submit(device.GraphicsQueue, fences[frameIndex],
                                   waitSemaphores: [imageAvailableSemaphores[frameIndex]],
                                   signalSemaphores: [renderFinishedSemaphores[frameIndex]]);
        swapchainCtx.QueuePresent(device.PresentQueue, [imageIndex], [swapchain],
                                  [renderFinishedSemaphores[frameIndex]]);
        frameIndex = (++frameIndex) % framesInFlight;
    }
}