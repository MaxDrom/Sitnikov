using System.Runtime.InteropServices;
using Silk.NET.Vulkan;
using VkAllocatorSystem;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace BoidsVulkan;

using Buffer = Buffer;

public interface IVkBuffer
{
    Buffer Buffer { get; }
}

public class VkBuffer<T> : IVkBuffer, IDisposable
    where T : unmanaged
{
    private readonly VkAllocator _allocator;
    private readonly Buffer _buffer;
    private readonly VkContext _ctx;
    private readonly VkDevice _device;
    private readonly AllocationNode _node;
    private bool _disposedValue;

    public VkBuffer(int length,
        BufferUsageFlags usage,
        SharingMode sharingMode,
        VkAllocator allocator)
    {
        _ctx = allocator.Ctx;
        _device = allocator.Device;
        _allocator = allocator;
        Size = (ulong)length * (ulong)Marshal.SizeOf<T>();
        unsafe
        {
            var createInfo = new BufferCreateInfo
            {
                SType = StructureType.BufferCreateInfo,
                Size = Size,
                Usage = usage,
                SharingMode = sharingMode
            };

            if (_ctx.Api.CreateBuffer(_device.Device, ref createInfo,
                    null, out _buffer) != Result.Success)
                throw new Exception("Failed to create buffer");

            var memoryRequirements =
                _ctx.Api.GetBufferMemoryRequirements(_device.Device,
                    _buffer);
            _node = allocator.Allocate(memoryRequirements);
            _ctx.Api.BindBufferMemory(_device.Device, _buffer,
                _node.Memory, _node.Offset);
        }
    }

    public VkBuffer(ulong length,
        BufferUsageFlags usage,
        SharingMode sharingMode,
        VkAllocator allocator)
    {
        _ctx = allocator.Ctx;
        _device = allocator.Device;
        _allocator = allocator;
        Size = length * (ulong)Marshal.SizeOf<T>();
        unsafe
        {
            var createInfo = new BufferCreateInfo
            {
                SType = StructureType.BufferCreateInfo,
                Size = Size,
                Usage = usage,
                SharingMode = sharingMode
            };

            if (_ctx.Api.CreateBuffer(_device.Device, ref createInfo,
                    null, out _buffer) != Result.Success)
                throw new Exception("Failed to create buffer");

            var memoryRequirements =
                _ctx.Api.GetBufferMemoryRequirements(_device.Device,
                    _buffer);
            _node = allocator.Allocate(memoryRequirements);
            _ctx.Api.BindBufferMemory(_device.Device, _buffer,
                _node.Memory, _node.Offset);
        }
    }

    public ulong Size { get; }

    public DeviceMemory Memory => _node.Memory;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public Buffer Buffer => _buffer;

    public VkMappedMemory<T> Map(int offset, int size)
    {
        var structSize = (ulong)Marshal.SizeOf<T>();
        if (structSize * (ulong)size + (ulong)offset * structSize >
            Size)
            throw new ArgumentException();

        return new VkMappedMemory<T>(_ctx, _device, _node.Memory,
            offset, size, MemoryMapFlags.None);
    }

    public VkMappedMemory<T> Map(ulong offset, ulong size)
    {
        var structSize = (ulong)Marshal.SizeOf<T>();
        if (structSize * size + offset * structSize > Size)
            throw new ArgumentException();

        return new VkMappedMemory<T>(_ctx, _device, _node.Memory,
            offset, size, MemoryMapFlags.None);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            unsafe
            {
                _ctx.Api.DestroyBuffer(_device.Device, _buffer, null);
            }

            if (disposing) _allocator.Deallocate(_node);

            _disposedValue = true;
        }
    }

    ~VkBuffer()
    {
        Dispose(false);
    }
}