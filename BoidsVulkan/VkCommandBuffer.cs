using Silk.NET.Vulkan;
using Buffer = Silk.NET.Vulkan.Buffer;
using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace Sitnikov.BoidsVulkan;

using Buffer = Buffer;

public class VkCommandBuffer
{
    private readonly VkContext _ctx;
    private readonly VkDevice _device;
    private readonly VkCommandPool _pool;

    public VkCommandBuffer(VkContext ctx,
        VkDevice device,
        VkCommandPool pool,
        CommandBuffer buffer)
    {
        _ctx = ctx;
        _device = device;
        _pool = pool;
        Buffer = buffer;
    }

    public CommandBuffer Buffer { get; }

    public VkCommandRecordingObject Begin(
        CommandBufferUsageFlags flags)
    {
        CommandBufferBeginInfo beginInfo = new()
        {
            SType = StructureType.CommandBufferBeginInfo,
            Flags = flags,
        };

        unsafe
        {
            if (_ctx.Api.BeginCommandBuffer(Buffer, &beginInfo) !=
                Result.Success)
                throw new Exception("Failed to begin command buffer");
        }

        return new VkCommandRecordingObject(_ctx, this);
    }

    public void Reset(CommandBufferResetFlags flags)
    {
        _ctx.Api.ResetCommandBuffer(Buffer, flags);
    }

    public unsafe void Submit(Queue queue,
        VkFence fence,
        IEnumerable<VkSemaphore> waitSemaphores,
        IEnumerable<VkSemaphore> signalSemaphores)
    {
        var tmp = waitSemaphores.Select(z => z.Semaphore).ToArray();
        var flags = waitSemaphores.Select(z => z.Flag).ToArray();
        var signalTmp = signalSemaphores.Select(z => z.Semaphore)
            .ToArray();
        fixed (Semaphore* pSemaphores = tmp)
        {
            fixed (PipelineStageFlags* pwaitStageMask = flags)
            {
                fixed (Semaphore* pSignalSemaphores = signalTmp)
                {
                    var pb = Buffer;
                    SubmitInfo submitInfo = new()
                    {
                        SType = StructureType.SubmitInfo,
                        WaitSemaphoreCount = (uint)tmp.Length,
                        PWaitSemaphores = pSemaphores,
                        PWaitDstStageMask = pwaitStageMask,
                        SignalSemaphoreCount =
                            (uint)signalTmp.Length,
                        PSignalSemaphores = pSignalSemaphores,
                        CommandBufferCount = 1,
                        PCommandBuffers = &pb,
                    };

                    if (_ctx.Api.QueueSubmit(queue, 1u,
                            ref submitInfo, fence.InternalFence) !=
                        Result.Success)
                        throw new Exception(
                            "Failed to submit buffer");
                }
            }
        }
    }

    public class VkCommandRecordingObject : IDisposable
    {
        private readonly VkCommandBuffer _buffer;
        private readonly VkContext _ctx;

        public VkCommandRecordingObject(VkContext ctx,
            VkCommandBuffer buffer)
        {
            _buffer = buffer;
            _ctx = ctx;
        }

        public void Dispose()
        {
            _ctx.Api.EndCommandBuffer(_buffer.Buffer);
        }

        public VkCommandRecordingRenderObject BeginRenderPass(
            VkRenderPass renderPass,
            VkFrameBuffer framebuffer,
            Rect2D renderArea)
        {
            unsafe
            {
                ClearValue clearValue =
                    new(new ClearColorValue(0, 0, 0, 1));
                var renderPassInfo = new RenderPassBeginInfo
                {
                    SType = StructureType.RenderPassBeginInfo,
                    ClearValueCount = 1,
                    PClearValues = &clearValue,
                    RenderArea = renderArea,
                    Framebuffer = framebuffer.Framebuffer,
                    RenderPass = renderPass.RenderPass,
                };
                _ctx.Api.CmdBeginRenderPass(_buffer.Buffer,
                    ref renderPassInfo, SubpassContents.Inline);
            }

            return new VkCommandRecordingRenderObject(_ctx, _buffer,
                renderPass, framebuffer);
        }

        public void CopyBuffer<T>(VkBuffer<T> src,
            VkBuffer<T> dst,
            ulong srcOffset,
            ulong dstOffset,
            ulong size)
            where T : unmanaged
        {
            var region = new BufferCopy
            {
                SrcOffset = srcOffset,
                DstOffset = dstOffset,
                Size = size,
            };
            _ctx.Api.CmdCopyBuffer(_buffer.Buffer, src.Buffer,
                dst.Buffer, 1, ref region);
        }

        public void BindVertexBuffers(int firstBinding,
            IVkBuffer[] buffers,
            ulong[] offsets)
        {
            unsafe
            {
                var tmp = stackalloc Buffer[buffers.Length];
                for (var i = 0; i < buffers.Length; i++)
                    tmp[i] = buffers[i].Buffer;

                var tmp2 = stackalloc ulong[offsets.Length];
                for (var i = 0; i < offsets.Length; i++)
                    tmp2[i] = offsets[i];

                _ctx.Api.CmdBindVertexBuffers(_buffer.Buffer,
                    (uint)firstBinding, (uint)buffers.Length, tmp,
                    tmp2);
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
                    fixed (ImageMemoryBarrier* pmemImage =
                               imageMemoryBarriers)
                    {
                        fixed (BufferMemoryBarrier* pmemBuffer =
                                   bufferMemoryBarriers)
                        {
                            _ctx.Api.CmdPipelineBarrier(
                                _buffer.Buffer, srcStageFlags,
                                dstStageFlags, dependencyFlags,
                                (uint)(memoryBarriers?.Length ?? 0),
                                pmem,
                                (uint)(bufferMemoryBarriers?.Length ??
                                       0), pmemBuffer,
                                (uint)(imageMemoryBarriers?.Length ??
                                       0), pmemImage);
                        }
                    }
                }
            }
        }

        public void BindIndexBuffer(VkBuffer<uint> buffer,
            ulong offset,
            IndexType indexType)

        {
            _ctx.Api.CmdBindIndexBuffer(_buffer.Buffer, buffer.Buffer,
                offset, indexType);
        }

        public void BindDescriptorSets(PipelineBindPoint bindPoint,
            PipelineLayout piplineLayout,
            DescriptorSet[] sets,
            uint[] dynamicOffsets = null,
            uint firstSet = 0)
        {
            _ctx.Api.CmdBindDescriptorSets(_buffer.Buffer, bindPoint,
                piplineLayout, 0,
                new ReadOnlySpan<DescriptorSet>(sets),
                new ReadOnlySpan<uint>(dynamicOffsets));
        }

        public void BindPipline(IVkPipeline pipeline)
        {
            _ctx.Api.CmdBindPipeline(_buffer.Buffer,
                pipeline.BindPoint, pipeline.InternalPipeline);
        }
    }

    public class VkCommandRecordingRenderObject : IDisposable
    {
        private readonly VkCommandBuffer _buffer;
        private readonly VkContext _ctx;
        private VkFrameBuffer _framebuffer;
        private VkRenderPass _renderPass;

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
            _ctx.Api.CmdEndRenderPass(_buffer.Buffer);
        }

        public void Draw(uint vertexCount,
            uint instanceCount,
            uint firstVertex,
            uint firstInstance)
        {
            _ctx.Api.CmdDraw(_buffer.Buffer, vertexCount,
                instanceCount, firstVertex, firstInstance);
        }

        public void DrawIndexed(uint indexCount,
            uint instanceCount,
            uint firstIndex,
            uint firstInstance)
        {
            _ctx.Api.CmdDrawIndexed(_buffer.Buffer, indexCount,
                instanceCount, firstIndex, 0, firstInstance);
        }

        public void SetScissor(ref Rect2D scissor)
        {
            _ctx.Api.CmdSetScissor(_buffer.Buffer, 0, 1, ref scissor);
        }

        public unsafe void SetScissor(Rect2D[] scissors)
        {
            fixed (Rect2D* pScissors = scissors)
            {
                _ctx.Api.CmdSetScissor(_buffer.Buffer, 0,
                    (uint)scissors.Length, pScissors);
            }
        }

        public void SetBlendConstant(ReadOnlySpan<float> constants)
        {
            _ctx.Api.CmdSetBlendConstants(_buffer.Buffer, constants);
        }

        public void SetBlendConstant(float[] constants)
        {
            unsafe
            {
                fixed (float* tmp = constants)
                {
                    _ctx.Api.CmdSetBlendConstants(_buffer.Buffer,
                        tmp);
                }
            }
        }

        public void SetViewport(ref Viewport viewport)
        {
            _ctx.Api.CmdSetViewport(_buffer.Buffer, 0, 1,
                ref viewport);
        }
    }
}