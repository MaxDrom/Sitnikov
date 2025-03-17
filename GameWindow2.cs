using System.Runtime.InteropServices;
using BoidsVulkan;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Silk.NET.Windowing;

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
    private static Instance[] _instances;

    private readonly uint[] _indices = [0, 1, 2, 2, 3, 0];

    private readonly Vertex[] _quadVertices =
    [
        new()
        {
            position = new Vector2D<float>(-1f, -1f),
            color = new Vector4D<float>(0.0f, 0.0f, 0.0f, 0.0001f),
        },

        new()
        {
            position = new Vector2D<float>(1f, -1f),
            color = new Vector4D<float>(0.0f, 0.0f, 0.0f, 0.0001f),
        },

        new()
        {
            position = new Vector2D<float>(1f, 1f),
            color = new Vector4D<float>(0.0f, 0.0f, 0.0f, 0.0001f),
        },

        new()
        {
            position = new Vector2D<float>(-1f, 1f),
            color = new Vector4D<float>(0.0f, 0.0f, 0.0f, 0.0001f),
        },
    ];

    private readonly Vertex[] _vertices =
    [
        new()
        {
            position = new Vector2D<float>(-0.003f, -0.003f),
            color = new Vector4D<float>(1.0f, 1.0f, 1.0f, 1.0f),
        },

        new()
        {
            position = new Vector2D<float>(0.003f, -0.003f),
            color = new Vector4D<float>(1.0f, 1.0f, 1.0f, 1.0f),
        },

        new()
        {
            position = new Vector2D<float>(0.003f, 0.003f),
            color = new Vector4D<float>(1.0f, 1.0f, 1.0f, 1.0f),
        },

        new()
        {
            position = new Vector2D<float>(-0.003f, 0.003f),
            color = new Vector4D<float>(1.0f, 1.0f, 1.0f, 1.0f),
        },
    ];

    private bool _firstrun = true;
    private uint _imageIndex;

    private SymplecticIntegrator<double, Vector<double>> _integrator;

    private double _totaltime;

    public void Run()
    {
        _window.Run();
    }

    private void RecordBuffer(VkCommandBuffer buffer, int imageIndex)
    {
        Viewport viewport = new()
        {
            X = 0.0f,
            Y = 0.0f,
            Width = _swapchain.Extent.Width,
            Height = _swapchain.Extent.Height,
        };
        Rect2D scissor = new(new Offset2D(0, 0), _swapchain.Extent);
        buffer.Reset(CommandBufferResetFlags.None);
        using (var recording =
               buffer.Begin(CommandBufferUsageFlags.None))
        {
            var subresourceRange =
                new ImageSubresourceRange(ImageAspectFlags.ColorBit,
                    0, 1, 0, 1);

            ImageMemoryBarrier bb = new()
            {
                SType = StructureType.ImageMemoryBarrier,
                OldLayout = ImageLayout.Undefined,
                NewLayout = ImageLayout.General,
                SrcAccessMask = AccessFlags.None,
                DstAccessMask = AccessFlags.MemoryReadBit,
                Image = _textureBuffer.Image.Image,
                SubresourceRange = subresourceRange,
            };

            recording.PipelineBarrier(PipelineStageFlags.TopOfPipeBit,
                PipelineStageFlags.ColorAttachmentOutputBit,
                DependencyFlags.None, imageMemoryBarriers: [bb]);

            using (var renderRecording =
                   recording.BeginRenderPass(_renderPass,
                       _framebuffers[0], scissor))
            {
                var pushConstant2 = new PushConstant
                {
                    xrange = new Vector2D<float>(-1, 1),
                    yrange = new Vector2D<float>(-1, 1),
                };

                recording.BindPipline(_graphicsPipeline);
                _ctx.Api.CmdPushConstants(buffer.Buffer,
                    _graphicsPipeline.PipelineLayout,
                    ShaderStageFlags.VertexBit, 0,
                    (uint)Marshal.SizeOf<PushConstant>(),
                    ref pushConstant2);

                recording.BindVertexBuffers(0,
                    [_quadVertexBuffer, _quadInstanceBuffer], [0, 0]);
                recording.BindIndexBuffer(_indexBuffer, 0,
                    IndexType.Uint32);
                renderRecording.SetViewport(ref viewport);
                renderRecording.SetScissor(ref scissor);
                renderRecording.DrawIndexed((uint)_indices.Length, 1u,
                    0, 0);
                var pushConstant = new PushConstant
                {
                    xrange =
                        new Vector2D<float>(
                            (float)_config.Visualization.RangeX
                                .Item1,
                            (float)_config.Visualization.RangeX
                                .Item2),
                    yrange = new Vector2D<float>(
                        (float)_config.Visualization.RangeY.Item1,
                        (float)_config.Visualization.RangeY.Item2),
                };
                _ctx.Api.CmdPushConstants(buffer.Buffer,
                    _graphicsPipeline.PipelineLayout,
                    ShaderStageFlags.VertexBit, 0,
                    (uint)Marshal.SizeOf<PushConstant>(),
                    ref pushConstant);
                recording.BindVertexBuffers(0,
                    [_vertexBuffer, _instanceBuffer], [0, 0]);
                renderRecording.SetViewport(ref viewport);
                renderRecording.SetScissor(ref scissor);
                renderRecording.DrawIndexed((uint)_indices.Length,
                    (uint)_instances.Length - 1, 0, 0);
            }

            var region = new ImageCopy
            {
                SrcOffset = new Offset3D(0, 0, 0),
                DstOffset = new Offset3D(0, 0, 0),
                Extent =
                    new Extent3D(_swapchain.Extent.Width,
                        _swapchain.Extent.Height, 1),
                SrcSubresource =
                    new ImageSubresourceLayers(
                        ImageAspectFlags.ColorBit, 0, 0, 1),
                DstSubresource =
                    new ImageSubresourceLayers(
                        ImageAspectFlags.ColorBit, 0, 0, 1),
            };

            ImageMemoryBarrier[] barriers =
            [
                new()
                {
                    SType = StructureType.ImageMemoryBarrier,
                    OldLayout = ImageLayout.Undefined,
                    NewLayout = ImageLayout.TransferSrcOptimal,
                    SrcAccessMask =
                        AccessFlags.ColorAttachmentWriteBit,
                    DstAccessMask = AccessFlags.TransferReadBit,
                    Image = _textureBuffer.Image.Image,
                    SubresourceRange = subresourceRange,
                },

                new()
                {
                    SType = StructureType.ImageMemoryBarrier,
                    OldLayout = ImageLayout.Undefined,
                    NewLayout = ImageLayout.TransferDstOptimal,
                    SrcAccessMask = AccessFlags.None,
                    DstAccessMask = AccessFlags.TransferWriteBit,
                    Image = _swapchain.Images[imageIndex].Image,
                    SubresourceRange = subresourceRange,
                },
            ];

            recording.PipelineBarrier(
                PipelineStageFlags.ColorAttachmentOutputBit,
                PipelineStageFlags.TransferBit, DependencyFlags.None,
                imageMemoryBarriers: barriers);

            _ctx.Api.CmdCopyImage(buffer.Buffer,
                _textureBuffer.Image.Image,
                ImageLayout.TransferSrcOptimal,
                _swapchain.Images[imageIndex].Image,
                ImageLayout.TransferDstOptimal, 1, ref region);

            ImageMemoryBarrier barrier2 = new()
            {
                SType = StructureType.ImageMemoryBarrier,
                OldLayout = ImageLayout.TransferDstOptimal,
                NewLayout = ImageLayout.PresentSrcKhr,
                SrcAccessMask = AccessFlags.TransferWriteBit,
                DstAccessMask = AccessFlags.None,
                Image = _swapchain.Images[imageIndex].Image,
                SubresourceRange = subresourceRange,
            };

            recording.PipelineBarrier(PipelineStageFlags.TransferBit,
                PipelineStageFlags.BottomOfPipeBit,
                DependencyFlags.None,
                imageMemoryBarriers: [barrier2]);
        }
    }

    private void OnResize(Vector2D<int> x)
    {
        _windowOptions.Size = x;
        _ctx.Api.DeviceWaitIdle(_device.Device);
        CleanUpSwapchain();

        CreateSwapchain();
        CreateViews();
        CreateFramebuffers();

        for (var i = 0; i < _views.Count; i++)
            RecordBuffer(_buffers[i], i);
    }

    private async Task OnUpdate(double frametime)
    {
        if (_firstrun)
        {
            _firstrun = false;
            _copyBuffer.Reset(CommandBufferResetFlags.None);
            using (var recording =
                   _copyBuffer.Begin(CommandBufferUsageFlags
                       .SimultaneousUseBit))
            {
                recording.CopyBuffer(_particleSystem.Buffer,
                    _instanceBuffer, 0, 0,
                    _particleSystem.Buffer.Size);
            }

            _totaltime = 0;
            return;
        }

        using (var mapped = _quadInstanceBuffer.Map(0, 1))
        {
            mapped[0] = new Instance
            {
                position = Vector2D<float>.Zero,
                color = new Vector4D<float>(0, 0, 0,
                    1.0f - (float)Math.Exp(
                        -_config.Visualization.Fade * frametime)),
                offset = new Vector2D<float>(0, 1),
            };
        }

        _totalFrametime += frametime;
        _fps++;
        if (_totalFrametime >= 1)
        {
            _window.Title = $"FPS: {_fps / _totalFrametime}";
            _totalFrametime = 0;
            _fps = 0;
        }

        await _particleSystem.Update(frametime, _totaltime);
        _totaltime += frametime;
        _copyFence.Reset();
        _copyBuffer.Submit(_device.GraphicsQueue, _copyFence, [], []);
        await _copyFence.WaitFor();
    }

    private void OnRender(double frametime)
    {
        _fences[_frameIndex].WaitFor().GetAwaiter().GetResult();
        if (_swapchain.AcquireNextImage(_device,
                _imageAvailableSemaphores[_frameIndex],
                out _imageIndex) == Result.ErrorOutOfDateKhr)
            return;

        _fences[_frameIndex].Reset();
        _buffers[_imageIndex].Submit(_device.GraphicsQueue,
            _fences[_frameIndex],
            [_imageAvailableSemaphores[_frameIndex]],
            [_renderFinishedSemaphores[_frameIndex]]);
        _swapchainCtx.QueuePresent(_device.PresentQueue,
            [_imageIndex],
            [_swapchain], [_renderFinishedSemaphores[_frameIndex]]);
        _frameIndex = ++_frameIndex % _framesInFlight;
    }
}