using System.Runtime.InteropServices;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Windowing;

namespace BoidsVulkan
{
    public class VkContext : IDisposable
    {

        public Vk Api => _vk;
        public KhrSurface SurfaceApi => _surfaceApi;
        public Instance Instance => _instance;
        public SurfaceKHR Surface => _surface;
        private readonly Vk _vk = Vk.GetApi();
        private readonly Instance _instance;
        private readonly KhrSurface _surfaceApi;
        private readonly SurfaceKHR _surface;
#if DEBUG
        private readonly ExtDebugUtils _extDebugUtils;
        private readonly DebugUtilsMessengerEXT _debugUtilsMessenger;
#endif
        private bool disposedValue = false;

        public unsafe VkContext(IWindow window, string[] RequiredExtensions)
        {
            var enabledInstanceExtensions = new List<string> { ExtDebugUtils.ExtensionName };
            enabledInstanceExtensions.AddRange(RequiredExtensions);

#if DEBUG
            var enabledLayers = new List<string> { "VK_LAYER_KHRONOS_validation" };
#endif
            var flags = InstanceCreateFlags.None;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                //enabledInstanceExtensions.Add("VK_KHR_portability_subset");
                enabledInstanceExtensions.Add("VK_KHR_portability_enumeration");
                flags |= InstanceCreateFlags.EnumeratePortabilityBitKhr;
            }
#if DEBUG
            var pPEnabledLayers = (byte**)SilkMarshal.StringArrayToPtr([.. enabledLayers]);
#endif

            var pPEnabledInstanceExtensions = (byte**)SilkMarshal.StringArrayToPtr([.. enabledInstanceExtensions]);

            var appInfo = new ApplicationInfo
            {
                SType = StructureType.ApplicationInfo,
                ApiVersion = Vk.Version13
            };
#if DEBUG
            var debugInfo = new DebugUtilsMessengerCreateInfoEXT
            {
                SType = StructureType.DebugUtilsMessengerCreateInfoExt,
                MessageSeverity = DebugUtilsMessageSeverityFlagsEXT.ErrorBitExt
                | DebugUtilsMessageSeverityFlagsEXT.WarningBitExt | DebugUtilsMessageSeverityFlagsEXT.InfoBitExt,
                MessageType = DebugUtilsMessageTypeFlagsEXT.ValidationBitExt
                | DebugUtilsMessageTypeFlagsEXT.PerformanceBitExt | DebugUtilsMessageTypeFlagsEXT.DeviceAddressBindingBitExt,
                PfnUserCallback = (DebugUtilsMessengerCallbackFunctionEXT)DebugCallback
            };

            var validationFeatureEnables = new ValidationFeatureEnableEXT[]
            {
                ValidationFeatureEnableEXT.DebugPrintfExt
            };

            fixed (ValidationFeatureEnableEXT* pEnabledFeatures = validationFeatureEnables)
            {
                var validationFeatures = new ValidationFeaturesEXT
                {
                    SType = StructureType.ValidationFeaturesExt,
                    EnabledValidationFeatureCount = (uint)validationFeatureEnables.Length,
                    PEnabledValidationFeatures = pEnabledFeatures

                };

                // Теперь validationFeatures можно использовать в дальнейшем коде
            

#endif

            var instanceInfo = new InstanceCreateInfo
            {
                SType = StructureType.InstanceCreateInfo,
                Flags = flags,
#if DEBUG
                EnabledLayerCount = (uint)enabledLayers.Count,
                PpEnabledLayerNames = pPEnabledLayers,
#endif
                EnabledExtensionCount = (uint)enabledInstanceExtensions.Count,
                PpEnabledExtensionNames = pPEnabledInstanceExtensions,
                PApplicationInfo = &appInfo,
#if DEBUG
                PNext = &validationFeatures
#endif
            };
            if (_vk.CreateInstance(ref instanceInfo, null, out _instance) != Result.Success)
                throw new Exception("Instance could not be created");
#if DEBUG            
            }

            if (!_vk.TryGetInstanceExtension(_instance, out _extDebugUtils))
                throw new Exception($"Could not get instance extension {ExtDebugUtils.ExtensionName}");
            _extDebugUtils.CreateDebugUtilsMessenger(_instance, ref debugInfo, null, out _debugUtilsMessenger);
#endif
            if (!_vk.TryGetInstanceExtension(_instance, out _surfaceApi))
                throw new Exception($"Could not get instance extension {KhrSurface.ExtensionName}");

#if DEBUG
            SilkMarshal.Free((nint)pPEnabledLayers);
#endif
            SilkMarshal.Free((nint)pPEnabledInstanceExtensions);


            _surface = window.VkSurface!.Create<AllocationCallbacks>(_instance.ToHandle(), null).ToSurface();
        }


#if DEBUG
        private unsafe static uint DebugCallback(DebugUtilsMessageSeverityFlagsEXT severityFlags,
                                      DebugUtilsMessageTypeFlagsEXT messageTypeFlags,
                                      DebugUtilsMessengerCallbackDataEXT* pCallbackData,
                                      void* pUserData)
        {
            var message = Marshal.PtrToStringAnsi((nint)pCallbackData->PMessage);
            var copy = $"{message}";
            if(copy.Trim().Length > 0)
                Console.WriteLine($"[Vulkan]: {severityFlags}: {message}");
            return Vk.False;
        }
#endif
        protected unsafe virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                _surfaceApi.DestroySurface(_instance, _surface, null);
#if DEBUG
                _extDebugUtils.DestroyDebugUtilsMessenger(_instance, _debugUtilsMessenger, null);
#endif
                _vk.DestroyInstance(_instance, null);
                if (disposing)
                {
                    _vk.Dispose();
#if DEBUG
                    _extDebugUtils.Dispose();
#endif
                    _surfaceApi.Dispose();
                }

            }
            disposedValue = true;
        }

        ~VkContext()
        {

            Dispose(disposing: false);
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}