using Silk.NET.Vulkan;

namespace BoidsVulkan;

public class VkShaderModule : IDisposable
{
    private readonly VkContext _ctx;
    private readonly VkDevice _device;

    private readonly ShaderModule _shaderModule;
    private bool _disposedValue;

    public VkShaderModule(VkContext ctx,
        VkDevice device,
        string spirvPath)
    {
        var bytes = File.ReadAllBytes(spirvPath);
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

                if (ctx.Api.CreateShaderModule(device.Device,
                        in createInfo, null, out _shaderModule) !=
                    Result.Success)
                    throw new Exception(
                        $"Failed to create shader module with path {spirvPath}");
            }
        }
    }

    public ShaderModule ShaderModule => _shaderModule;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            unsafe
            {
                _ctx.Api.DestroyShaderModule(_device.Device,
                    _shaderModule, null);
            }

            _disposedValue = true;
        }
    }

    ~VkShaderModule()
    {
        Dispose(false);
    }
}