using System.Collections.Concurrent;
using System.Globalization;
using Silk.NET.Windowing;
using YamlDotNet.Serialization;

namespace SymplecticIntegrators;


public class SitnikovConfig
{
    public double e { get; set; } = 0.2;
    public int SizeX { get; set; } = 50;
    public int SizeY { get; set; } = 50;
    public (double, double) RangeX { get; set; } = (0, 1);
    public (double, double) RangeY { get; set; } = (0, 1);
    
    public IntegratorConfig Integrator{get;set;} = new IntegratorConfig();
    public VisualizationConfig Visualization {get; set;} = new VisualizationConfig();
    public PoincareConfig Poincare{get; set;} = null;
}

public class IntegratorConfig
{
    public int Order {get; set;} = 2;
    public double Timestep {get; set;} = 0.01;
}

public class PoincareConfig
{
    public int Periods {get; set;} = 10;
}

public class VisualizationConfig
{
    public double Fade {get; set;} = 5;
    public (double, double) RangeX {get; set;} = (-2.5, 2.5);
    public (double, double) RangeY {get; set;} = (-2.5, 2.5);
}
class Program
{

    public static ConcurrentDictionary<double, double> KeplerSolutions = new();

    public static double e = 0.1;
    public static double dt = 0.01;

    public static (double, double) SolveKeplerEq(double M)
    {
        var E = M;
        double sin = 0;

        for (var i = 0; i < 10; i++)
        {
            sin = Math.Sin(E);
            var newE = M + e * sin;
            E = newE;
            E %= 2 * Math.PI;
            if (E >= Math.PI)
                E -= 2 * Math.PI;
        }
        return (E, sin);
    }

    public static double GetDistance(double t)
    {
        var M = t;
        M %= 2 * Math.PI;
        if (M >= Math.PI)
            M -= 2 * Math.PI;
        if (KeplerSolutions.TryGetValue(M, out var result))
            return result;

        var (E, sin) = SolveKeplerEq(M);
        var res = 1 - e * Math.Sqrt(1 - sin * sin);
        KeplerSolutions.TryAdd(M, res);
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
        var result = new Vector<double>(2);

        result[0] = p[0];
        result[1] = 1;

        return result;
    }

    static SymplecticIntegrator<double, Vector<double>> yoshida6;

    static async Task Main(string[] args)
    {
        ThreadPool.SetMaxThreads(Environment.ProcessorCount, Environment.ProcessorCount);
        CultureInfo.CurrentCulture = new CultureInfo("en-US", false);
        CultureInfo.CurrentCulture.NumberFormat.CurrencyDecimalDigits = 28;
        var deserializer = new DeserializerBuilder()
                            .Build();

        using var sr = File.OpenText("config.yaml");
        var config = deserializer.Deserialize<SitnikovConfig>(sr);
        e = config.e;
        dt = config.Integrator.Timestep;
        yoshida6 = YoshidaIntegrator<double, Vector<double>>.BuildFromLeapfrog(dV, dT, config.Integrator.Order/2);
        if (config.Poincare != null)
        {
            var taskList = new List<Task<(double, double)[]>>();
            var gridX = config.SizeX;
            var gridY = config.SizeY;
            var dx = config.RangeX.Item2 - config.RangeX.Item1;
            var dy = config.RangeY.Item2 - config.RangeY.Item1;
            for (var x = 0; x < gridX; x++)
            {
                var q =  config.RangeX.Item1 + dx* x / (double)(gridX -1);
                for (var y = 0; y < gridY; y++)
                {
                    var p =  config.RangeY.Item1 + dx* y / (double)(gridY -1);
                    taskList.Add(
                                Task.Run(() =>
                                            PoincareMap(q,
                                                        p,
                                                        config.Poincare.Periods)
                                        ));

                }
            }
            var totalResults = await Task.WhenAll(taskList);

            using var file = new StreamWriter("result.dat");
            foreach (var res in totalResults)
                foreach (var (q, p) in res)
                    file.WriteLine($"{q} {p}");

            return;
        }
        using var gameWindow = new GameWindow(WindowOptions.DefaultVulkan, yoshida6, config);

        gameWindow.Run();
    }

    static (double, double)[] PoincareMap(double q0, double p0, int niters)
    {
        var result = new (double, double)[niters];
        result[0] = (q0, p0);
        double qq = q0;
        double pp = p0;
        for (var i = 0; i < niters; i++)
        {

            foreach (var (t, q, p) in yoshida6
                                    .Integrate(Math.PI * 2,
                                        dt,
                                        new([qq, 0]),
                                        new([pp, 0])))
            {
                qq = q[0];
                pp = p[0];
            }

            result[i] = (qq, pp);
        }

        return result;
    }
}