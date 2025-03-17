using System.Runtime.InteropServices;
using Silk.NET.Vulkan;

namespace Sitnikov.BoidsVulkan;

public class VkVertexInputStateCreateInfo : IDisposable
{
    private GCHandle _bindingDescriptionsHandle;

    private bool _disposedValue;

    private GCHandle _vertexInputAttributeDescriptionsHandle;

    public VkVertexInputStateCreateInfo(
        IEnumerable<VertexInputBindingDescription>
            bindingDescriptions,
        IEnumerable<VertexInputAttributeDescription>
            vertexInputAttributeDescriptions)
    {
        _vertexInputAttributeDescriptionsHandle = GCHandle.Alloc(
            vertexInputAttributeDescriptions.ToArray(),
            GCHandleType.Pinned);
        _bindingDescriptionsHandle = GCHandle.Alloc(
            bindingDescriptions.ToArray(), GCHandleType.Pinned);
        unsafe
        {
            PipelineVertexInputStateCreateInfo =
                new PipelineVertexInputStateCreateInfo
                {
                    SType =
                        StructureType
                            .PipelineVertexInputStateCreateInfo,
                    VertexBindingDescriptionCount =
                        (uint)bindingDescriptions.Count(),
                    PVertexBindingDescriptions =
                        (VertexInputBindingDescription*)
                        _bindingDescriptionsHandle
                            .AddrOfPinnedObject(),
                    VertexAttributeDescriptionCount =
                        (uint)vertexInputAttributeDescriptions
                            .Count(),
                    PVertexAttributeDescriptions =
                        (VertexInputAttributeDescription*)
                        _vertexInputAttributeDescriptionsHandle
                            .AddrOfPinnedObject(),
                };
        }
    }

    public PipelineVertexInputStateCreateInfo
        PipelineVertexInputStateCreateInfo { get; private set; }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            _bindingDescriptionsHandle.Free();
            _vertexInputAttributeDescriptionsHandle.Free();
            _disposedValue = true;
        }
    }

    ~VkVertexInputStateCreateInfo()
    {
        Dispose(false);
    }
}