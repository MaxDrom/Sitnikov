using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Autofac;
using Silk.NET.Core.Native;
using Silk.NET.Windowing;
using Sitnikov.symplecticIntegrators;
using YamlDotNet.Serialization;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using Sitnikov.BoidsVulkan;
using Sitnikov.BoidsVulkan.VkAllocatorSystem;

namespace Sitnikov;

public delegate SymplecticIntegrator<double, Vector<double>>
    SymplecticFactory(
        Func<Vector<double>, Vector<double>> dV,
        Func<Vector<double>, Vector<double>> dT);

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct Instance : IVertexData<Instance>
{
    [VertexInputDescription(2, Format.R32G32Sfloat)]
    public Vector2D<float> position;

    [VertexInputDescription(4, Format.R32G32Sfloat)]
    public Vector2D<float> offset;

    [VertexInputDescription(3, Format.R32G32B32A32Sfloat)]
    public Vector4D<float> color;
}

public class DisplayFormat
{
    public Format Format { get; set; }
    public ColorSpaceKHR ColorSpace { get; set; }

    public WindowOptions WindowOptions { get; set; }
}

public class SitnikovConfig
{
    public double e { get; set; } = 0.2;
    public int SizeX { get; set; } = 50;
    public int SizeY { get; set; } = 50;
    public (double, double) RangeX { get; set; } = (0, 1);
    public (double, double) RangeY { get; set; } = (0, 1);

    public IntegratorConfig Integrator { get; set; } = new();

    public VisualizationConfig Visualization { get; set; } = new();

    public PoincareConfig Poincare { get; set; } = null;
}

public class IntegratorConfig
{
    public int Order { get; set; } = 2;
    public double Timestep { get; set; } = 0.01;
}

public class PoincareConfig
{
    public int Periods { get; set; } = 10;
}

public class VisualizationConfig
{
    public bool OnGPU { get; set; } = false;
    public double Fade { get; set; } = 5;

    public (double, double) RangeX { get; set; } = (-2.5, 2.5);

    public (double, double) RangeY { get; set; } = (-2.5, 2.5);
}

internal class Program
{
    public static ConcurrentDictionary<double, double>
        KeplerSolutions = new();

    public static double E = 0.1;
    public static double Dt = 0.01;

    private static SymplecticIntegrator<double, Vector<double>>
        _yoshida6;

    public static (double, double) SolveKeplerEq(double m)
    {
        var e = m;
        double sin = 0;

        for (var i = 0; i < 10; i++)
        {
            sin = Math.Sin(e);
            var newE = m + E * sin;
            e = newE;
            e %= 2 * Math.PI;
            if (e >= Math.PI) e -= 2 * Math.PI;
        }

        return (e, sin);
    }

    public static double GetDistance(double t)
    {
        var m = t;
        m %= 2 * Math.PI;
        if (m >= Math.PI) m -= 2 * Math.PI;

        if (KeplerSolutions.TryGetValue(m, out var result))
            return result;

        var (e, sin) = SolveKeplerEq(m);
        var res = 1 - E * Math.Sqrt(1 - sin * sin);
        KeplerSolutions.TryAdd(m, res);
        return res;
    }

    public static Vector<double> dV(Vector<double> q)
    {
        var result = new Vector<double>(2);
        var t = q[1];
        var r = GetDistance(t);
        var rr = Math.Sqrt(r * r + q[0] * q[0]);
        result[0] = q[0] / (rr * rr * rr);
        result[1] = 0;

        return result;
    }

    public static Vector<double> dT(Vector<double> p)
    {
        var result = new Vector<double>(2) { [0] = p[0], [1] = 1 };

        return result;
    }

    private static async Task Main(string[] args)
    {
        ThreadPool.SetMaxThreads(Environment.ProcessorCount,
            Environment.ProcessorCount);
        CultureInfo.CurrentCulture = new CultureInfo("en-US", false);
        CultureInfo.CurrentCulture.NumberFormat
            .CurrencyDecimalDigits = 28;
        var deserializer = new DeserializerBuilder().Build();

        using var sr = File.OpenText("config.yaml");
        var config = deserializer.Deserialize<SitnikovConfig>(sr);
        E = config.e;
        Dt = config.Integrator.Timestep;

        var windowOptions = WindowOptions.DefaultVulkan;
        using var window = Window.Create(windowOptions);

        var gridX = config.SizeX;
        var gridY = config.SizeY;
        var instances = new Instance[gridX * gridY];
        var dx = (float)(config.RangeX.Item2 - config.RangeX.Item1);
        var dy = (float)(config.RangeY.Item2 - config.RangeY.Item1);
        for (var xx = 0; xx < gridX; xx++)
        for (var yy = 0; yy < gridY; yy++)
        {
            instances[xx + yy * gridX] = new Instance
            {
                position = new Vector2D<float>(
                    (float)config.RangeX.Item1 +
                    xx / (gridX - 1f) * dx,
                    (float)config.RangeY.Item1 +
                    yy / (gridY - 1f) * dy),
                color =
                    new Vector4D<float>(xx / (gridX - 1f),
                        yy / (gridY - 1f), 1f, 1f),
                offset = new Vector2D<float>(0, 0),
            };
        }

        var builder = new ContainerBuilder();
        builder.RegisterInstance(config).SingleInstance();

        SymplecticFactory factory = (x, y) =>
            YoshidaIntegrator<double, Vector<double>>
                .BuildFromLeapfrog(x, y,
                    config.Integrator.Order / 2);
        builder.RegisterInstance(factory).SingleInstance();

        _yoshida6 = YoshidaIntegrator<double, Vector<double>>
            .BuildFromLeapfrog(dV, dT,
                config.Integrator.Order / 2);
        builder.RegisterInstance(_yoshida6).SingleInstance();


        if (config.Visualization.OnGPU)
            builder.RegisterType<ParticleSystemGpu>()
                .As<IParticleSystem>()
                .WithParameter("initialData",
                    instances).SingleInstance();
        else
            builder.RegisterType<ParticleSystemCpu>()
                .As<IParticleSystem>()
                .WithParameter("initialData",
                    instances).SingleInstance();

        InitVulkan(builder, window, windowOptions);
        var container = builder.Build();
        var particleSystem = container.Resolve<IParticleSystem>();
        if (config.Poincare != null)
        {
            using (var stream =
                   new StreamWriter("result.dat", false))
            {
                for (int i = 1; i <= config.Poincare.Periods; i++)
                {
                    double T = 0;
                    while (T < 2 * Math.PI)
                    {
                        await particleSystem.Update(delta: Dt, T);
                        T += Dt;
                    }

                    foreach (var instance in particleSystem
                                 .DataOnCpu)
                        stream.Write(
                            $"{instance.position.X} {instance.position.Y}\n");

                    var blocksCount = 50;
                    var progressBlockCount = (int)Math.Floor(i *
                        blocksCount /
                        (double)config.Poincare.Periods);
                    var animation = @"|/-\";
                    string text = string.Format("[{0}{1}] {2,3}% {3}",
                        new string('#',
                            progressBlockCount),
                        new string('-', blocksCount - progressBlockCount),
                        Math.Floor(100*i/(double)config.Poincare.Periods),
                        animation[
                            i % animation.Length]);
                    var stringBuilder = new StringBuilder();
                    stringBuilder.Append('\b', text.Length);
                    stringBuilder.Append(text);
                    Console.Write(stringBuilder);
                }
            }
        }
        else
        {
            var gameWindow = container.Resolve<GameWindow>();
            gameWindow.Run();
        }

        container.Dispose();
    }

    public static void InitVulkan(ContainerBuilder builder,
        IWindow window,
        WindowOptions windowOptions)
    {
        window.Initialize();
        if (window.VkSurface is null)
            throw new Exception(
                "Windowing platform doesn't support Vulkan.");

        string[] extensions;
        unsafe
        {
            var pp =
                window.VkSurface
                    .GetRequiredExtensions(out var count);
            extensions = new string[count];
            SilkMarshal.CopyPtrToStringArray((nint)pp, extensions);
        }

        var ctx
            = new VkContext(window, extensions);
        var physicalDevice = ctx.Api
            .GetPhysicalDevices(ctx.Instance).ToArray()[0];
        string deviceName;
        unsafe
        {
            var property =
                ctx.Api.GetPhysicalDeviceProperty(physicalDevice);
            deviceName =
                SilkMarshal.PtrToString((nint)property.DeviceName)!;

            uint nn;
            ctx.SurfaceApi.GetPhysicalDeviceSurfaceFormats(
                physicalDevice, ctx.Surface, &nn, null);
            var formats = new SurfaceFormatKHR[nn];
            fixed (SurfaceFormatKHR* pFormat = formats)
            {
                ctx.SurfaceApi.GetPhysicalDeviceSurfaceFormats(
                    physicalDevice, ctx.Surface, &nn, pFormat);
            }

            var format = formats[0].Format;
            var colorSpace = formats[0].ColorSpace;
            foreach (var formatCap in formats)
                if (formatCap.Format == Format.R16G16B16A16Sfloat)
                {
                    colorSpace = formatCap.ColorSpace;
                    break;
                }

            builder.RegisterInstance(
                    new DisplayFormat()
                    {
                        Format = format, ColorSpace = colorSpace,
                        WindowOptions = windowOptions
                    })
                .SingleInstance();
        }


        Console.WriteLine(deviceName);

        builder.RegisterInstance(window).SingleInstance();
        builder.RegisterInstance(ctx).SingleInstance();
        builder.RegisterInstance(new VkDevice(ctx,
            physicalDevice, [],
            [KhrSwapchain.ExtensionName])).SingleInstance();


        builder.RegisterType<StupidAllocator>().As<VkAllocator>()
            .WithParameter("requiredProperties",
                MemoryPropertyFlags.None)
            .WithParameter("preferredFlags",
                MemoryHeapFlags.DeviceLocalBit)
            .WithMetadata("Type", "DeviceLocal")
            .SingleInstance();

        builder.RegisterType<StupidAllocator>().As<VkAllocator>()
            .WithParameter("requiredProperties",
                MemoryPropertyFlags.HostVisibleBit |
                MemoryPropertyFlags.HostCoherentBit)
            .WithParameter("preferredFlags",
                MemoryHeapFlags.None)
            .WithMetadata("Type", "HostVisible")
            .SingleInstance();

        builder.RegisterType<GameWindow>().AsSelf().SingleInstance();
    }
}