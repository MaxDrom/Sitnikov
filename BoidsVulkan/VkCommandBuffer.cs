using System.Runtime.CompilerServices;
using Silk.NET.Vulkan;
using Buffer = Silk.NET.Vulkan.Buffer;
using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace Sitnikov.BoidsVulkan;

using Buffer = Buffer;

public class VkCommandBuffer(VkContext ctx,
    VkDevice device,
    VkCommandPool pool,
    CommandBuffer buffer
)
{
    private readonly VkDevice _device = device;
    private readonly VkCommandPool _pool = pool;

    public CommandBuffer Buffer { get; } = buffer;

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
            if (ctx.Api.BeginCommandBuffer(Buffer, &beginInfo) !=
                Result.Success)
                throw new Exception("Failed to begin command buffer");
        }

        return new VkCommandRecordingObject(ctx, this);
    }

    public void Reset(CommandBufferResetFlags flags)
    {
        ctx.Api.ResetCommandBuffer(Buffer, flags);
    }

    public unsafe void Submit(Queue queue,
        VkFence fence,
        VkSemaphore[] waitSemaphores,
        VkSemaphore[] signalSemaphores)
    {
        var waits = waitSemaphores
            .Select(z => z.Semaphore).ToArray();
        var stageMasks =
            waitSemaphores
                .Select(z => z.Flag).ToArray();
        var signals = signalSemaphores
            .Select(z => z.Semaphore).ToArray();

        fixed (Semaphore* pWaits = waits)
        {
            fixed (PipelineStageFlags* pStageMasks = stageMasks)
            {
                fixed (Semaphore* pSignals = signals)
                {
                    var pb = Buffer;
                    SubmitInfo submitInfo = new()
                    {
                        SType = StructureType.SubmitInfo,
                        WaitSemaphoreCount = (uint)waits.Length,
                        PWaitSemaphores = pWaits,
                        PWaitDstStageMask =
                            pStageMasks,
                        SignalSemaphoreCount =
                            (uint)signals.Length,
                        PSignalSemaphores =
                            pSignals,
                        CommandBufferCount = 1,
                        PCommandBuffers = &pb,
                    };

                    if (ctx.Api.QueueSubmit(queue, 1u,
                            in submitInfo, fence.InternalFence) !=
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
                    in renderPassInfo, SubpassContents.Inline);
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
                dst.Buffer, 1, in region);
        }

        public void BindVertexBuffers(int firstBinding,
            IVkBuffer[] buffers,
            ulong[] offsets)
        {
            unsafe
            {
                var pBuffers = stackalloc Buffer[buffers.Length];
                for (var i = 0; i < buffers.Length; i++)
                    pBuffers[i] = buffers[i].Buffer;

                var pOffsets = stackalloc ulong[offsets.Length];
                for (var i = 0; i < offsets.Length; i++)
                    pOffsets[i] = offsets[i];

                _ctx.Api.CmdBindVertexBuffers(_buffer.Buffer,
                    (uint)firstBinding, (uint)buffers.Length, pBuffers,
                    pOffsets);
            }
        }

        public void PipelineBarrier(PipelineStageFlags srcStageFlags,
            PipelineStageFlags dstStageFlags,
            DependencyFlags dependencyFlags,
            ReadOnlySpan<MemoryBarrier> memoryBarriers = default,
            ReadOnlySpan<BufferMemoryBarrier> bufferMemoryBarriers = default,
            ReadOnlySpan<ImageMemoryBarrier> imageMemoryBarriers = default)
        {
            _ctx.Api.CmdPipelineBarrier(
                _buffer.Buffer, srcStageFlags,
                dstStageFlags, dependencyFlags,
                memoryBarriers,
                bufferMemoryBarriers,
                imageMemoryBarriers);
        }

        public void BindIndexBuffer(VkBuffer<uint> buffer,
            ulong offset,
            IndexType indexType)

        {
            _ctx.Api.CmdBindIndexBuffer(_buffer.Buffer, buffer.Buffer,
                offset, indexType);
        }

        public void BindDescriptorSets(PipelineBindPoint bindPoint,
            PipelineLayout pipelineLayout,
            ReadOnlySpan<DescriptorSet> sets,
            ReadOnlySpan<uint> dynamicOffsets = default,
            uint firstSet = 0)
        {
            _ctx.Api.CmdBindDescriptorSets(_buffer.Buffer, bindPoint,
                pipelineLayout, firstSet,
                sets,
                dynamicOffsets);
        }

        public void BindPipeline(IVkPipeline pipeline)
        {
            _ctx.Api.CmdBindPipeline(_buffer.Buffer,
                pipeline.BindPoint, pipeline.InternalPipeline);
        }
    }

    public class VkCommandRecordingRenderObject(VkContext ctx,
        VkCommandBuffer buffer,
        VkRenderPass renderPass,
        VkFrameBuffer framebuffer
    )
        : IDisposable
    {
        private VkFrameBuffer _framebuffer = framebuffer;
        private VkRenderPass _renderPass = renderPass;

        public void Dispose()
        {
            ctx.Api.CmdEndRenderPass(buffer.Buffer);
        }

        public void Draw(uint vertexCount,
            uint instanceCount,
            uint firstVertex,
            uint firstInstance)
        {
            ctx.Api.CmdDraw(buffer.Buffer, vertexCount,
                instanceCount, firstVertex, firstInstance);
        }

        public void DrawIndexed(uint indexCount,
            uint instanceCount,
            uint firstIndex,
            uint firstInstance)
        {
            ctx.Api.CmdDrawIndexed(buffer.Buffer, indexCount,
                instanceCount, firstIndex, 0, firstInstance);
        }

        public void SetScissor(ref Rect2D scissor)
        {
            ctx.Api.CmdSetScissor(buffer.Buffer, 0, 1, in scissor);
        }

        public unsafe void SetScissor(Rect2D[] scissors)
        {
            fixed (Rect2D* pScissors = scissors)
            {
                ctx.Api.CmdSetScissor(buffer.Buffer, 0,
                    (uint)scissors.Length, pScissors);
            }
        }

        public void SetBlendConstant(ReadOnlySpan<float> constants)
        {
            ctx.Api.CmdSetBlendConstants(buffer.Buffer, constants);
        }

        public void SetBlendConstant(float[] constants)
        {
            unsafe
            {
                fixed (float* tmp = constants)
                {
                    ctx.Api.CmdSetBlendConstants(buffer.Buffer,
                        tmp);
                }
            }
        }

        public void SetViewport(ref Viewport viewport)
        {
            ctx.Api.CmdSetViewport(buffer.Buffer, 0, 1,
                in viewport);
        }
    }
}