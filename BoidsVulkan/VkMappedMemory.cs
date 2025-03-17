using System.Runtime.InteropServices;
using Silk.NET.Vulkan;

namespace BoidsVulkan;

public unsafe class VkMappedMemory<T> : IDisposable
    where T : unmanaged
{
    private readonly VkContext _ctx;
    private readonly VkDevice _device;
    private readonly DeviceMemory _memory;
    private readonly T* _memoryPointer;

    private bool _disposedValue;

    public VkMappedMemory(VkContext ctx,
        VkDevice device,
        DeviceMemory memoryToMap,
        int offset,
        int size,
        MemoryMapFlags flags)
    {
        _ctx = ctx;
        _device = device;
        _memory = memoryToMap;
        Length = (ulong)size;
        var structSize = (ulong)Marshal.SizeOf<T>();
        var offsetInBytes = (ulong)offset * structSize;
        var sizeInBytes = (ulong)size * structSize;
        void* tmp;
        _ctx.Api.MapMemory(_device.Device, _memory, offsetInBytes,
            sizeInBytes, flags, &tmp);
        _memoryPointer = (T*)tmp;
    }

    public VkMappedMemory(VkContext ctx,
        VkDevice device,
        DeviceMemory memoryToMap,
        ulong offset,
        ulong size,
        MemoryMapFlags flags)
    {
        _ctx = ctx;
        _device = device;
        _memory = memoryToMap;
        Length = size;
        // var structSize = ;
        var offsetInBytes = offset;
        var sizeInBytes = size;
        void* tmp;
        _ctx.Api.MapMemory(_device.Device, _memory, offsetInBytes,
            sizeInBytes, flags, &tmp);
        _memoryPointer = (T*)tmp;
    }

    public ulong Length { get; }

    public T this[int index]
    {
        get
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(
                (ulong)index, Length);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(0, index);
            return _memoryPointer[index];
        }
        set
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(
                (ulong)index, Length);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(0, index);
            _memoryPointer[index] = value;
        }
    }

    public T this[ulong index]
    {
        get
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(
                index, Length);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(0u, index);
            return _memoryPointer[index];
        }
        set
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(
                index, Length);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(0u, index);
            _memoryPointer[index] = value;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            _ctx.Api.UnmapMemory(_device.Device, _memory);
            _disposedValue = true;
        }
    }

    ~VkMappedMemory()
    {
        Dispose(false);
    }
}