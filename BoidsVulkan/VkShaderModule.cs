using Silk.NET.Vulkan;

namespace BoidsVulkan;

public class VkShaderModule : IDisposable
{
    public ShaderModule ShaderModule => _shaderModule;

    private readonly ShaderModule _shaderModule;
    private readonly VkContext _ctx;
    private readonly VkDevice _device;
    private bool disposedValue;

    public VkShaderModule(VkContext ctx, VkDevice device, string SPIRVPath)
    {
        var bytes = File.ReadAllBytes(SPIRVPath);
        _ctx = ctx;
        _device = device;

        unsafe
        {
            fixed (byte* pcode = bytes)
            {
                ShaderModuleCreateInfo createInfo = new()
                {
                    SType = StructureType.ShaderModuleCreateInfo,
                    CodeSize = (nuint)bytes.Length,
                    PCode = (uint*)pcode
                };

                if (ctx.Api.CreateShaderModule(device.Device, in createInfo, null, out _shaderModule) != Result.Success)
                    throw new Exception($"Failed to create shader module with path {SPIRVPath}");
            }
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            unsafe
            {
                _ctx.Api.DestroyShaderModule(_device.Device, _shaderModule, null);
            }
            disposedValue = true;
        }
    }

    ~VkShaderModule()
    {
        Dispose(disposing: false);
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}