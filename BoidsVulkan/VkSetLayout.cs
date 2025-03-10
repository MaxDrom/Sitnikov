using System.Reflection;
using Silk.NET.Vulkan;

namespace BoidsVulkan;
public interface IUniformBufferStruct
{
}


public class VkSetLayout : IDisposable
{
    public DescriptorSetLayout SetLayout => _setLayout;
    private readonly VkContext _ctx;
    private readonly VkDevice _device;
    private readonly DescriptorSetLayout _setLayout;
    private bool disposedValue;

    private VkSetLayout(VkContext ctx, VkDevice device,
        DescriptorSetLayoutCreateInfo info)
    {
        _ctx = ctx;
        _device = device;
        unsafe
        {
            if (_ctx.Api.CreateDescriptorSetLayout(_device.Device, ref info, null, out _setLayout) != Result.Success)
                throw new Exception("Failed to create descriptor set layout");
        }
    }

    public static VkSetLayout CreateForStructure<T>(VkContext ctx, VkDevice device)
        where T : struct
    {
        unsafe
        {
            var bindings = new List<DescriptorSetLayoutBinding>();
            foreach (var descriptor in typeof(T).GetProperties()
                    .Select(z => z.GetCustomAttribute<UniformDescription>())
                    .Where(z => z != null))
            {
                bindings.Add(new DescriptorSetLayoutBinding()
                {
                    Binding = (uint)descriptor.Binding,
                    DescriptorType = descriptor.DescriptorType,
                    DescriptorCount = (uint)descriptor.DescriptorCount,
                    StageFlags = descriptor.ShaderStageFlags,
                    PImmutableSamplers = null
                });
            }

            fixed (DescriptorSetLayoutBinding* pbindings = bindings.ToArray())
            {
                var createInfo = new DescriptorSetLayoutCreateInfo()
                {
                    SType = StructureType.DescriptorSetLayoutCreateInfo,
                    BindingCount = (uint)bindings.Count,
                    PBindings = pbindings
                };

                return new VkSetLayout(ctx, device, createInfo);
            }
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            unsafe
            {
                _ctx.Api.DestroyDescriptorSetLayout(_device.Device, _setLayout, null);
            }
            disposedValue = true;
        }
    }

    ~VkSetLayout()
    {
        Dispose(disposing: false);
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}