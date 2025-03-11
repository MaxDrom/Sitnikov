using System.Runtime.InteropServices;
using Silk.NET.Vulkan;

namespace BoidsVulkan;

public unsafe class VkMappedMemory<T> : IDisposable
    where T : unmanaged
{

    public ulong Length => _size;
    private VkContext _ctx;
    private VkDevice _device;
    private DeviceMemory _memory;

    private bool disposedValue;
    private T* _memoryPointer;
    private ulong _size;

    public VkMappedMemory(VkContext ctx, VkDevice device, DeviceMemory memoryToMap, int offset, int size, MemoryMapFlags flags)
    {
        _ctx = ctx;
        _device = device;
        _memory = memoryToMap;
        _size = (ulong)size;
        var structSize = (ulong)Marshal.SizeOf<T>();
        var offsetInBytes = (ulong)offset *structSize;
        var sizeInBytes = (ulong)size * structSize;
        void* tmp;
        _ctx.Api.MapMemory(_device.Device, _memory, offsetInBytes, sizeInBytes, flags, &tmp);
        _memoryPointer = (T*)tmp;
    }

    public VkMappedMemory(VkContext ctx, VkDevice device, DeviceMemory memoryToMap, ulong offset, ulong size, MemoryMapFlags flags)
    {
        _ctx = ctx;
        _device = device;
        _memory = memoryToMap;
        _size = size;
        //var structSize = ;
        var offsetInBytes = offset ;
        var sizeInBytes = size;
        void* tmp;
        _ctx.Api.MapMemory(_device.Device, _memory, offsetInBytes, sizeInBytes, flags, &tmp);
        _memoryPointer = (T*)tmp;
    }

    public T this[int index]
    {
        get
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((ulong)index, _size);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(0, index);
            return _memoryPointer[index];
        }
        set
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((ulong)index, _size);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(0, index);
            _memoryPointer[index] = value;
        }
    }

    public T this[ulong index]
    {
        get
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((ulong)index, _size);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(0u, index);
            return _memoryPointer[index];
        }
        set
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((ulong)index, _size);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(0u, index);
            _memoryPointer[index] = value;
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            _ctx.Api.UnmapMemory(_device.Device, _memory);
            disposedValue = true;
        }
    }

    ~VkMappedMemory()
    {
        Dispose(disposing: false);
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
