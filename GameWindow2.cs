using System.Runtime.InteropServices;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Silk.NET.Windowing;
using BoidsVulkan;
using System.Numerics;
namespace SymplecticIntegrators;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
struct Instance : IVertexData
{
    [VertexAttributeDescription(2, Format.R32G32Sfloat)]
    public Vector2D<float> position;

    [VertexAttributeDescription(3, Format.R32G32B32A32Sfloat)]
    public Vector4D<float> color;
}

public partial class GameWindow
{
    SymplecticIntegrator<double, Vector<double>> integrator;
    Instance[] instances;
    // Instance[] instances1 = [
    //     new()
    //     {
    //         position = new Vector2D<float>(0, 0),
    //         color = new Vector4D<float>(0, 0, 0, 1.0f)
    //     }
    // ];

    Vertex[] vertices =
    [
        new()
        {
            position = new Vector2D<float>(-0.001f, -0.001f),
            color = new Vector4D<float>(1.0f, 1.0f, 1.0f, 1.0f)
        },

        new()
        {
            position = new Vector2D<float>(0.001f, -0.001f),
            color = new Vector4D<float>(1.0f, 1.0f, 1.0f, 1.0f)
        },

        new()
        {
            position = new Vector2D<float>(0.001f, 0.001f),
            color = new Vector4D<float>(1.0f, 1.0f, 1.0f, 1.0f)
        },

        new()
        {
            position = new Vector2D<float>(-0.001f, 0.001f),
            color = new Vector4D<float>(1.0f, 1.0f, 1.0f, 1.0f)
        },
    ];

    Vertex[] quadVertices =
    [
        new()
        {
            position = new Vector2D<float>(-1f, -1f),
            color = new Vector4D<float>(0.0f, 0.0f, 0.0f, 0.0001f)
        },

        new()
        {
            position = new Vector2D<float>(1f, -1f),
            color = new Vector4D<float>(0.0f, 0.0f, 0.0f, 0.0001f)
        },

        new()
        {
            position = new Vector2D<float>(1f, 1f),
            color = new Vector4D<float>(0.0f, 0.0f, 0.0f, 0.0001f)
        },

        new()
        {
            position = new Vector2D<float>(-1f, 1f),
            color = new Vector4D<float>(0.0f, 0.0f, 0.0f, 0.0001f)
        },
    ];

    uint[] indices = [0, 1, 2, 2, 3, 0];

    public void Run()
    {
        window.Run();
    }
    float[] blendFactors = GC.AllocateArray<float>(4, true);// [0.0f, 0.0f, 0.0f, 0.0f];
    float[] blendFactor2 = GC.AllocateArray<float>(4, true);
    void RecordBuffer(VkCommandBuffer buffer, int imageIndex)
    {
        Viewport viewport = new()
        {
            X = 0.0f,
            Y = 0.0f,
            Width = swapchain.Extent.Width,
            Height = swapchain.Extent.Height
        };
        Rect2D scissor = new(new Offset2D(0, 0), swapchain.Extent);
        buffer.Reset(CommandBufferResetFlags.None);
        using (var recording = buffer.Begin(CommandBufferUsageFlags.None))
        {

            using (var renderRecording = recording.BeginRenderPass(renderPass, framebuffers[imageIndex], scissor))
            {

                recording.BindPipline(graphicsPipeline);

                recording.BindVertexBuffers(0, [quadVertexBuffer, instanceBuffer], [0, (ulong)Marshal.SizeOf<Instance>()*(ulong)(instances.Length - 1)]);
                recording.BindIndexBuffer(indexBuffer, 0, IndexType.Uint32);
                renderRecording.SetViewport(ref viewport);
                renderRecording.SetScissor(ref scissor);
                //renderRecording.SetBlendConstant(blendFactors);
                renderRecording.DrawIndexed((uint)indices.Length, 1u, 0, 0);

                recording.BindVertexBuffers(0, [vertexBuffer, instanceBuffer], [0, 0]);
                recording.BindIndexBuffer(indexBuffer, 0, IndexType.Uint32);
                renderRecording.SetViewport(ref viewport);
                renderRecording.SetScissor(ref scissor);
                //renderRecording.SetBlendConstant(blendFactor2);
                renderRecording.DrawIndexed((uint)indices.Length, (uint)instances.Length-1, 0, 0);
            }
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
        var (q, p) = integrator.Step(new Vector<double>([pos[0] * 2.5, totalTime]), new Vector<double>([pos[1] * 2.5, 0]), frametime);
        return new Vector2D<float>((float)q[0] / 2.5f, (float)p[0] / 2.5f);
    }

    async Task OnUpdate(double frametime)
    {

        if (staggingVertex == null)
        {
            staggingVertex = new VkBuffer((ulong)instances.Length * (ulong)Marshal.SizeOf<Instance>(), BufferUsageFlags.TransferSrcBit, SharingMode.Exclusive, staggingAllocator);
            copyBuffer.Reset(CommandBufferResetFlags.None);
            using (var recording = copyBuffer.Begin(CommandBufferUsageFlags.SimultaneousUseBit))
            {
                recording.CopyBuffer(staggingVertex, instanceBuffer, 0, 0, staggingVertex.Size);
            }
        }

        totalFrametime += frametime;
        FPS++;
        if (totalFrametime >= 1)
        {
            window.Title = $"FPS: {FPS / totalFrametime}";
            totalFrametime = 0;
            FPS = 0;
        }


        var taskList = new List<Task<Instance>>();
        for (var i = 0; i < instances.Length-1; i++)
        {
            var pos = instances[i].position;
            var col = instances[i].color;
            taskList.Add(Task.Run(() => new Instance() { position = Step(pos, frametime, totalTime), color = col }));
        }
    

        var result = await Task.WhenAll(taskList);
        totalTime += frametime;
        using (var mapped = staggingVertex.Map<Instance>(0, instances.Length))
        {
            for (var i = 0; i < instances.Length-1; i++)
            {
                mapped[i] = instances[i] = result[i];
            }
            mapped[instances.Length-1] = new Instance()
            {
                position = Vector2D<float>.Zero,
                color = new Vector4D<float>(0, 0, 0, (float)Math.Exp(-300 * frametime))
            };
        }

        await copyFence.WaitFor();
        copyFence.Reset();
       
        copyBuffer.Submit(device.TransferQueue, copyFence,
                                            waitSemaphores: [],
                                            signalSemaphores: []);

    }
    void OnRender(double frametime)
    {

        fences[frameIndex].WaitFor().GetAwaiter().GetResult();

        if (swapchain.AcquireNextImage(device, imageAvailableSemaphores[frameIndex], out var imageIndex) == Result.ErrorOutOfDateKhr)
            return;

        fences[frameIndex].Reset();
        //buffers[imageIndex].Reset(CommandBufferResetFlags.None);
        //RecordBuffer(buffers[imageIndex], (int)imageIndex);
        buffers[imageIndex].Submit(device.GraphicsQueue, fences[frameIndex],
                waitSemaphores: [imageAvailableSemaphores[frameIndex]],
                signalSemaphores: [renderFinishedSemaphores[frameIndex]]);
        swapchainCtx.QueuePresent(device.PresentQueue, [imageIndex], [swapchain], [renderFinishedSemaphores[frameIndex]]);
        frameIndex = (++frameIndex) % framesInFlight;
    }
}