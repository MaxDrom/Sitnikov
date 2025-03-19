using System.Reflection;
using Silk.NET.Vulkan;

namespace Sitnikov.BoidsVulkan;

public interface IUniformBufferStruct
{
}

public class VkSetLayout : IDisposable
{
    private readonly VkContext _ctx;
    private readonly VkDevice _device;
    private readonly DescriptorSetLayout _setLayout;
    private bool _disposedValue;

    private VkSetLayout(VkContext ctx,
        VkDevice device,
        DescriptorSetLayoutCreateInfo info)
    {
        _ctx = ctx;
        _device = device;
        unsafe
        {
            if (_ctx.Api.CreateDescriptorSetLayout(_device.Device,
                    ref info, null, out _setLayout) != Result.Success)
                throw new Exception(
                    "Failed to create descriptor set layout");
        }
    }

    public unsafe VkSetLayout(VkContext ctx,
        VkDevice device,
        DescriptorSetLayoutBinding[] bindings)
    {
        _ctx = ctx;
        _device = device;
        fixed (DescriptorSetLayoutBinding* pBindings = bindings)
        {
            DescriptorSetLayoutCreateInfo info = new()
            {
                SType =
                    StructureType.DescriptorSetLayoutCreateInfo,
                Flags = DescriptorSetLayoutCreateFlags.None,
                PBindings = pBindings,
                BindingCount = (uint)bindings.Length,
            };
            if (_ctx.Api.CreateDescriptorSetLayout(_device.Device,
                    ref info, null, out _setLayout) != Result.Success)
                throw new Exception(
                    "Failed to create descriptor set layout");
        }
    }

    public DescriptorSetLayout SetLayout => _setLayout;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public static VkSetLayout CreateForStructure<T>(VkContext ctx,
        VkDevice device)
        where T : struct
    {
        unsafe
        {
            var bindings = typeof(T).GetProperties()
                .Select(z => z.GetCustomAttribute<UniformDescriptionAttribute>())
                .Where(z => z != null)
                .Select(descriptor => new DescriptorSetLayoutBinding
                {
                    Binding = (uint)descriptor.Binding,
                    DescriptorType = descriptor.DescriptorType,
                    DescriptorCount = (uint)descriptor.DescriptorCount,
                    StageFlags = descriptor.ShaderStageFlags,
                    PImmutableSamplers = null,
                })
                .ToList();

            fixed (DescriptorSetLayoutBinding* pBindings =
                       bindings.ToArray())
            {
                var createInfo = new DescriptorSetLayoutCreateInfo
                {
                    SType =
                        StructureType
                            .DescriptorSetLayoutCreateInfo,
                    BindingCount = (uint)bindings.Count,
                    PBindings = pBindings,
                };

                return new VkSetLayout(ctx, device, createInfo);
            }
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposedValue) return;
        unsafe
        {
            _ctx.Api.DestroyDescriptorSetLayout(_device.Device,
                _setLayout, null);
        }

        _disposedValue = true;
    }

    ~VkSetLayout()
    {
        Dispose(false);
    }
}