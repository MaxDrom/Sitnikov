using System.Runtime.InteropServices;
using Silk.NET.Vulkan;

namespace BoidsVulkan;

public class VkVertexInputStateCreateInfo : IDisposable
{
    public PipelineVertexInputStateCreateInfo PipelineVertexInputStateCreateInfo { get; private set; }
    private GCHandle _vertexInputAttributeDescriptionsHandle;
    private GCHandle _bindingDescriptionsHandle;
    private bool disposedValue;

    public VkVertexInputStateCreateInfo(IEnumerable<VertexInputBindingDescription> bindingDescriptions,
        IEnumerable<VertexInputAttributeDescription> vertexInputAttributeDescriptions)
    {
        _vertexInputAttributeDescriptionsHandle = GCHandle.Alloc(vertexInputAttributeDescriptions.ToArray(), GCHandleType.Pinned);
        _bindingDescriptionsHandle = GCHandle.Alloc(bindingDescriptions.ToArray(), GCHandleType.Pinned);
        unsafe
        {
            PipelineVertexInputStateCreateInfo = new()
            {
                SType = StructureType.PipelineVertexInputStateCreateInfo,
                VertexBindingDescriptionCount = (uint)bindingDescriptions.Count(),
                PVertexBindingDescriptions = (VertexInputBindingDescription*)_bindingDescriptionsHandle.AddrOfPinnedObject(),
                VertexAttributeDescriptionCount = (uint)vertexInputAttributeDescriptions.Count(),
                PVertexAttributeDescriptions = (VertexInputAttributeDescription *)_vertexInputAttributeDescriptionsHandle.AddrOfPinnedObject(),
            };
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            _bindingDescriptionsHandle.Free();
            _vertexInputAttributeDescriptionsHandle.Free();
            disposedValue = true;
        }
    }

    ~VkVertexInputStateCreateInfo()
    {
        Dispose(disposing: false);
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}