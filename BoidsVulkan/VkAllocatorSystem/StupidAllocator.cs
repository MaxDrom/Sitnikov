using BoidsVulkan;
using Silk.NET.Vulkan;

namespace VkAllocatorSystem;

public class StupidAllocatorFactory : IVkAllocatorFactory
{
    public VkAllocator Create(VkContext ctx,
        VkDevice device,
        MemoryPropertyFlags requiredProperties,
        MemoryHeapFlags preferredFlags)
    {
        return new StupidAllocator(ctx, device, requiredProperties,
            preferredFlags);
    }
}

public class StupidAllocator : VkAllocator
{
    private readonly HashSet<AllocationNode> _allocatedNodes = [];

    private readonly PhysicalDeviceMemoryProperties _memoryProperties;

    private readonly List<int> _memoryTypesIndices;
    private readonly RwLock _rwlock = new();

    public StupidAllocator(VkContext ctx,
        VkDevice device,
        MemoryPropertyFlags requiredProperties,
        MemoryHeapFlags preferredFlags) : base(ctx, device,
        requiredProperties, preferredFlags)
    {
        Ctx.Api.GetPhysicalDeviceMemoryProperties(
            Device.PhysicalDevice, out _memoryProperties);
        Dictionary<int, int> memoryTypesScores = [];
        for (var i = 0; i < _memoryProperties.MemoryTypeCount; i++)
        {
            if ((_memoryProperties.MemoryTypes[i].PropertyFlags &
                 requiredProperties) != requiredProperties)
                continue;

            var heapInd = (int)_memoryProperties
                .MemoryTypes[i].HeapIndex;
            var score = NumberOfSetBits(
                (int)(_memoryProperties.MemoryHeaps[heapInd].Flags &
                      preferredFlags));
            memoryTypesScores[i] = score;
        }

        _memoryTypesIndices =
        [
            .. memoryTypesScores.OrderByDescending(z => z.Value)
                .Select(z => z.Key),
        ];
    }

    private static int NumberOfSetBits(int i)
    {
        i -= (i >> 1) & 0x55555555;
        i = (i & 0x33333333) + ((i >> 2) & 0x33333333);
        return (((i + (i >> 4)) & 0x0F0F0F0F) * 0x01010101) >> 24;
    }

    public override unsafe AllocationNode Allocate(
        MemoryRequirements requirements)
    {
        var success = false;

        DeviceMemory deviceMemory = default;
        foreach (var i in _memoryTypesIndices)
        {
            if ((requirements.MemoryTypeBits & (1 << i)) == 0)
                continue;

            var allocateInfo = new MemoryAllocateInfo
            {
                SType = StructureType.MemoryAllocateInfo,
                AllocationSize = requirements.Size,
                MemoryTypeIndex = (uint)i,
            };

            if (Ctx.Api.AllocateMemory(Device.Device,
                    ref allocateInfo, null, out deviceMemory) ==
                Result.Success)
            {
                success = true;
                break;
            }
        }

        if (!success)
            throw new Exception("Failed to allocate memory");

        var result = new AllocationNode(deviceMemory, 0);
        using (var writeLock = _rwlock.WriteLock())
        {
            _allocatedNodes.Add(result);
        }

        return result;
    }

    public override unsafe void Deallocate(AllocationNode node)
    {
        using var writeLock = _rwlock.WriteLock();
        if (!_allocatedNodes.Contains(node))
            throw new Exception(
                "Trying deallocate not allocated memory!");

        _allocatedNodes.Remove(node);
        Ctx.Api.FreeMemory(Device.Device, node.Memory, null);
    }

    public override void Free()
    {
        using var upgradeLock = _rwlock.UpgradeLock();
        var toDeallocate = _allocatedNodes.ToList();
        foreach (var node in toDeallocate) Deallocate(node);
    }
}