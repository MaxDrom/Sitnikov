using System.Runtime.InteropServices;
using Silk.NET.Vulkan;
using VkAllocatorSystem;

namespace BoidsVulkan;
using Buffer = Silk.NET.Vulkan.Buffer;
public class VkBuffer : IDisposable
{
    internal Buffer Buffer =>_buffer;
    private VkContext _ctx;
    private VkDevice _device;
    public ulong Size { get; private set; }

    public DeviceMemory Memory => _node.Memory;
    private Buffer _buffer;
    private bool disposedValue;
    private AllocationNode _node;
    private IVkAllocator _allocator;

    public VkBuffer(ulong size, BufferUsageFlags usage, SharingMode sharingMode, IVkAllocator allocator)
    {
        _ctx = allocator.Ctx;
        _device = allocator.Device;
        _allocator = allocator;
        Size = size;
        unsafe
        {
            BufferCreateInfo createInfo = new BufferCreateInfo()
            {
                SType = StructureType.BufferCreateInfo,
                Size = size,
                Usage = usage,
                SharingMode = sharingMode
            };

            if (_ctx.Api.CreateBuffer(_device.Device, ref createInfo, null, out _buffer) != Result.Success)
                throw new Exception("Failed to create buffer");

            var memoryRequirements = _ctx.Api.GetBufferMemoryRequirements(_device.Device, _buffer);
            _node = allocator.Allocate(memoryRequirements);
            _ctx.Api.BindBufferMemory(_device.Device, _buffer, _node.Memory, _node.Offset);
        }
    }


    public VkMappedMemory<T> Map<T>(int offset, int size)
        where T : unmanaged
    {
        ulong structSize = (ulong)Marshal.SizeOf<T>();
        if(structSize*(ulong)size + (ulong)offset*structSize > Size)
            throw new ArgumentException();
        return new VkMappedMemory<T>(_ctx, _device, _node.Memory, 
                                    offset, size, MemoryMapFlags.None);
    }

    public VkMappedMemory<T> Map<T>(ulong offset, ulong size)
        where T : unmanaged
    {
        ulong structSize = (ulong)Marshal.SizeOf<T>();
        if(structSize*size + offset*structSize > Size)
            throw new ArgumentException();
        return new VkMappedMemory<T>(_ctx, _device, _node.Memory, 
                                    offset, size, MemoryMapFlags.None);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            unsafe
            {
                _ctx.Api.DestroyBuffer(_device.Device, _buffer, null);
            }

            if (disposing)
            {
                _allocator.Deallocate(_node);
            }

            disposedValue = true;
        }
    }

    ~VkBuffer()
    {
        Dispose(disposing: false);
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}