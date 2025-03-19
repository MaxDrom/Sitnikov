using System.Runtime.InteropServices;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;

namespace Sitnikov.BoidsVulkan;

public unsafe class VkDevice : IDisposable
{
    private readonly uint? _computeFamilyIndex;
    private readonly Queue _computeQueue;
    private readonly VkContext _ctx;

    private readonly Device _device;

    private readonly uint? _graphicsFamilyIndex;
    private readonly Queue _graphicsQueue;
    private readonly uint? _presentFamilyIndex;
    private readonly Queue _presentQueue;
    private readonly uint? _transferFamilyIndex;

    private readonly Queue _transferQueue;
    private bool _disposedValue;

    public VkDevice(VkContext ctx,
        PhysicalDevice physicalDevice,
        List<string> enabledLayersNames,
        List<string> enabledExtensionsNames)
    {
        _ctx = ctx;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            enabledExtensionsNames.Add("VK_KHR_portability_subset");

        PhysicalDevice = physicalDevice;
        var pEnabledLayersNames =
            (byte**)SilkMarshal.StringArrayToPtr(enabledLayersNames);
        var pEnabledExtensionNames =
            (byte**)SilkMarshal.StringArrayToPtr(
                enabledExtensionsNames);

        uint queueFamilyPropertiesCount = 0;
        _ctx.Api.GetPhysicalDeviceQueueFamilyProperties(
            physicalDevice, ref queueFamilyPropertiesCount, null);
        var queueFamiliesProperties =
            new QueueFamilyProperties [queueFamilyPropertiesCount];
        fixed (QueueFamilyProperties* pQueueFamilies =
                   queueFamiliesProperties)
        {
            _ctx.Api.GetPhysicalDeviceQueueFamilyProperties(
                physicalDevice, ref queueFamilyPropertiesCount,
                pQueueFamilies);
        }

        HashSet<uint> familyIndiciesSet = new();
        for (var i = 0u; i < queueFamiliesProperties.Length; i++)
        {
            if (_graphicsFamilyIndex == null &&
                queueFamiliesProperties[i].QueueFlags
                    .HasFlag(QueueFlags.GraphicsBit))
            {
                _graphicsFamilyIndex = i;
                familyIndiciesSet.Add(i);
            }

            if (_computeFamilyIndex == null &&
                queueFamiliesProperties[i].QueueFlags
                    .HasFlag(QueueFlags.ComputeBit))
            {
                _computeFamilyIndex = i;
                familyIndiciesSet.Add(i);
            }

            if (_transferFamilyIndex == null &&
                queueFamiliesProperties[i].QueueFlags
                    .HasFlag(QueueFlags.TransferBit))
            {
                _transferFamilyIndex = i;
                familyIndiciesSet.Add(i);
            }

            if (_presentFamilyIndex == null)
            {
                _ctx.SurfaceApi.GetPhysicalDeviceSurfaceSupport(
                    physicalDevice, i, _ctx.Surface,
                    out var presentSupport);
                if (presentSupport)
                {
                    _presentFamilyIndex = i;
                    familyIndiciesSet.Add(i);
                }
            }
        }

        var familyIndicies = familyIndiciesSet.ToArray();
        var queueCreateInfos =
            new DeviceQueueCreateInfo[familyIndicies.Length];
        for (var i = 0; i < familyIndicies.Length; i++)
        {
            var defaultPriority = 1.0f;
            queueCreateInfos[i] = new DeviceQueueCreateInfo
            {
                SType = StructureType.DeviceQueueCreateInfo,
                QueueCount = 1,
                QueueFamilyIndex = familyIndicies[i],
                PQueuePriorities = &defaultPriority,
            };
        }

        DeviceCreateInfo deviceCreateInfo;
        fixed (DeviceQueueCreateInfo* pqueueCreateInfos =
                   queueCreateInfos)
        {
            deviceCreateInfo = new DeviceCreateInfo
            {
                SType = StructureType.DeviceCreateInfo,
                EnabledLayerCount =
                    (uint)enabledLayersNames.Count,
                PpEnabledLayerNames = pEnabledLayersNames,
                EnabledExtensionCount =
                    (uint)enabledExtensionsNames.Count,
                PpEnabledExtensionNames = pEnabledExtensionNames,
                QueueCreateInfoCount =
                    (uint)queueCreateInfos.Count(),
                PQueueCreateInfos = pqueueCreateInfos,
            };
        }

        if (_ctx.Api.CreateDevice(physicalDevice,
                ref deviceCreateInfo, null, out _device) !=
            Result.Success)
            throw new Exception("Could not create device");

        SilkMarshal.Free((nint)pEnabledLayersNames);
        SilkMarshal.Free((nint)pEnabledExtensionNames);

        if (_graphicsFamilyIndex != null)
            _ctx.Api.GetDeviceQueue(_device,
                _graphicsFamilyIndex!.Value, 0, out _graphicsQueue);

        if (_computeFamilyIndex != null)
            _ctx.Api.GetDeviceQueue(_device,
                _computeFamilyIndex!.Value, 0, out _computeQueue);

        if (_presentFamilyIndex != null)
            _ctx.Api.GetDeviceQueue(_device,
                _presentFamilyIndex!.Value, 0, out _presentQueue);

        if (_transferFamilyIndex != null)
            _ctx.Api.GetDeviceQueue(_device,
                _transferFamilyIndex!.Value, 0, out _transferQueue);
    }

    internal Device Device => _device;

    internal PhysicalDevice PhysicalDevice { get; private set; }

    public uint GraphicsFamilyIndex => _graphicsFamilyIndex!.Value;

    public uint PresentFamilyIndex => _presentFamilyIndex!.Value;
    public uint ComputeFamilyIndex => _computeFamilyIndex!.Value;
    public uint TransferFamilyIndex => _transferFamilyIndex!.Value;

    public Queue GraphicsQueue => _graphicsQueue;

    public Queue PresentQueue => _presentQueue;

    public Queue ComputeQueue => _computeQueue;

    public Queue TransferQueue => _transferQueue;
    
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            _ctx.Api.DestroyDevice(_device, null);
            _disposedValue = true;
        }
    }

    ~VkDevice()
    {
        Dispose(false);
    }
}