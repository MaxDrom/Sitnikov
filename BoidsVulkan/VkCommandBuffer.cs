using Silk.NET.Vulkan;

namespace BoidsVulkan;
using Buffer = Silk.NET.Vulkan.Buffer;
public class VkCommandBuffer
{
    public CommandBuffer InternalBuffer => _buffer;
    private readonly VkContext _ctx;
    private readonly VkDevice _device;
    private readonly VkCommandPool _pool;
    private readonly CommandBuffer _buffer;

    public VkCommandBuffer(VkContext ctx,
                            VkDevice device,
                            VkCommandPool pool,
                            CommandBuffer buffer)
    {
        _ctx = ctx;
        _device = device;
        _pool = pool;
        _buffer = buffer;
    }


    public VkCommandRecordingObject Begin(CommandBufferUsageFlags flags)
    {
        CommandBufferBeginInfo beginInfo = new()
        {
            SType = StructureType.CommandBufferBeginInfo,
            Flags = flags
        };

        unsafe
        {
            if (_ctx.Api.BeginCommandBuffer(_buffer, &beginInfo) != Result.Success)
                throw new Exception("Failed to begin command buffer");
        }

        return new VkCommandRecordingObject(_ctx, this);
    }

    public void Reset(CommandBufferResetFlags flags)
    {
        _ctx.Api.ResetCommandBuffer(_buffer, flags);
    }

    public unsafe void Submit(Queue queue, VkFence fence, IEnumerable<VkSemaphore> waitSemaphores,
            IEnumerable<VkSemaphore> signalSemaphores)
    {
        var tmp = waitSemaphores.Select(z => z.Semaphore).ToArray();
        var flags = waitSemaphores.Select(z => z.Flag).ToArray();
        var signalTmp = signalSemaphores.Select(z => z.Semaphore).ToArray();
        fixed (Silk.NET.Vulkan.Semaphore* pSemaphores = tmp)
        {
            fixed (PipelineStageFlags* pwaitStageMask = flags)
            {
                fixed (Silk.NET.Vulkan.Semaphore* pSignalSemaphores = signalTmp)
                {
                    var pb = _buffer;
                    SubmitInfo submitInfo = new()
                    {
                        SType = StructureType.SubmitInfo,
                        WaitSemaphoreCount = (uint)tmp.Length,
                        PWaitSemaphores = pSemaphores,
                        PWaitDstStageMask = pwaitStageMask,
                        SignalSemaphoreCount = (uint)signalTmp.Length,
                        PSignalSemaphores = pSignalSemaphores,
                        CommandBufferCount = 1,
                        PCommandBuffers = &pb
                    };

                    if (_ctx.Api.QueueSubmit(queue, 1u, ref submitInfo, fence.InternalFence) != Result.Success)
                        throw new Exception("Failed to submit buffer");

                }
            }
        }
    }

    public class VkCommandRecordingObject : IDisposable
    {

        private VkCommandBuffer _buffer;
        private VkContext _ctx;

        public VkCommandRecordingObject(VkContext ctx, VkCommandBuffer buffer)
        {
            _buffer = buffer;
            _ctx = ctx;
        }
        public VkCommandRecordingRenderObject BeginRenderPass(VkRenderPass renderPass, VkFrameBuffer framebuffer, Rect2D renderArea)
        {
            unsafe
            {
                ClearValue clearValue = new(new ClearColorValue(0, 0, 0, 1));
                var renderPassInfo = new RenderPassBeginInfo()
                {
                    SType = StructureType.RenderPassBeginInfo,
                    ClearValueCount = 1,
                    PClearValues = &clearValue,
                    RenderArea = renderArea,
                    Framebuffer = framebuffer.Framebuffer,
                    RenderPass = renderPass.RenderPass
                };
                _ctx.Api.CmdBeginRenderPass(_buffer.InternalBuffer, ref renderPassInfo, SubpassContents.Inline);
            }
            return new VkCommandRecordingRenderObject(_ctx, _buffer, renderPass, framebuffer);
        }

        public void CopyBuffer(VkBuffer src, VkBuffer dst, ulong srcOffset, ulong dstOffset, ulong size)
        {
            var region = new BufferCopy()
            {
                SrcOffset = srcOffset,
                DstOffset = dstOffset,
                Size = size
            };
            _ctx.Api.CmdCopyBuffer(_buffer.InternalBuffer, src.Buffer, dst.Buffer, 1, ref region);
        }

        public void BindVertexBuffers(int firstBinding, VkBuffer[] buffers, ulong[] offsets)
        {
            unsafe
            {
                var tmp = stackalloc Buffer[buffers.Length];
                for (var i = 0; i < buffers.Length; i++)
                    tmp[i] = buffers[i].Buffer;

                var tmp2 = stackalloc ulong[offsets.Length];
                for (var i = 0; i < offsets.Length; i++)
                    tmp2[i] = offsets[i];
                _ctx.Api.CmdBindVertexBuffers(_buffer.InternalBuffer, (uint)firstBinding, (uint)buffers.Length, tmp, tmp2);
            }
        }

        public void PipelineBarrier(PipelineStageFlags srcStageFlags,
                                    PipelineStageFlags dstStageFlags,
                                    DependencyFlags dependencyFlags,
                                    MemoryBarrier[] memoryBarriers = null,
                                    BufferMemoryBarrier[] bufferMemoryBarriers = null,
                                    ImageMemoryBarrier[] imageMemoryBarriers = null)
        {

            unsafe
            {
                fixed (MemoryBarrier* pmem = memoryBarriers)
                {
                    fixed (ImageMemoryBarrier* pmemImage = imageMemoryBarriers)
                    {
                        fixed (BufferMemoryBarrier* pmemBuffer = bufferMemoryBarriers)
                        {
                            _ctx.Api.CmdPipelineBarrier(_buffer.InternalBuffer,
                                            srcStageFlags,
                                            dstStageFlags,
                                            dependencyFlags,
                                            (uint)(memoryBarriers?.Length ?? 0),
                                            pmem,
                                            (uint)(bufferMemoryBarriers?.Length ?? 0),
                                            pmemBuffer,
                                            (uint)(imageMemoryBarriers?.Length ?? 0),
                                            pmemImage
                                            );
                        }
                    }
                }
            }
        }

        public void BindIndexBuffer(VkBuffer buffer, ulong offset, IndexType indexType)
        {
            _ctx.Api.CmdBindIndexBuffer(_buffer.InternalBuffer, buffer.Buffer, offset, indexType);
        }
        public void Dispose()
        {
            _ctx.Api.EndCommandBuffer(_buffer.InternalBuffer);
        }

        public void BindPipline(IVkPipeline pipeline)
        {
            unsafe
            {
                _ctx.Api.CmdBindPipeline(_buffer.InternalBuffer,
                                            pipeline.BindPoint,
                                            pipeline.InternalPipeline);
            }
        }
    }

    public class VkCommandRecordingRenderObject : IDisposable
    {
        private VkCommandBuffer _buffer;
        private VkContext _ctx;
        private VkRenderPass _renderPass;
        private VkFrameBuffer _framebuffer;

        public VkCommandRecordingRenderObject(VkContext ctx,
                                        VkCommandBuffer buffer,
                                        VkRenderPass renderPass,
                                        VkFrameBuffer framebuffer)
        {
            _buffer = buffer;
            _ctx = ctx;
            _renderPass = renderPass;
            _framebuffer = framebuffer;
        }
        public void Dispose()
        {
            _ctx.Api.CmdEndRenderPass(_buffer.InternalBuffer);
        }

        public void Draw(uint vertexCount, uint instanceCount, uint firstVertex, uint firstInstance)
        {
            _ctx.Api.CmdDraw(_buffer.InternalBuffer, vertexCount, instanceCount, firstVertex, firstInstance);
        }

        public void DrawIndexed(uint indexCount, uint instanceCount, uint firstIndex, uint firstInstance)
        {
            _ctx.Api.CmdDrawIndexed(_buffer.InternalBuffer, indexCount, instanceCount, firstIndex, 0, firstInstance);
        }

        public unsafe void SetScissor(ref Rect2D scissor)
        {
            _ctx.Api.CmdSetScissor(_buffer.InternalBuffer, 0, 1, ref scissor);
        }

        public unsafe void SetScissor(Rect2D[] scissors)
        {
            fixed (Rect2D* pScissors = scissors)
            {
                _ctx.Api.CmdSetScissor(_buffer.InternalBuffer, 0, (uint)scissors.Length, pScissors);
            }
        }


        public void SetBlendConstant(ReadOnlySpan<float> constants)
        {

            _ctx.Api.CmdSetBlendConstants(_buffer.InternalBuffer, constants);
        }

        public void SetBlendConstant(float[] constants)
        {
            unsafe
            {
                fixed (float* tmp = constants)
                {
                    _ctx.Api.CmdSetBlendConstants(_buffer.InternalBuffer, tmp);
                }
            }
        }

        public void SetViewport(ref Viewport viewport)
        {
            _ctx.Api.CmdSetViewport(_buffer.InternalBuffer, 0, 1, ref viewport);
        }
    }
}