using System.Collections.Concurrent;
using System.Globalization;
using Silk.NET.Windowing;

namespace SymplecticIntegrators;

class Program
{

    public static ConcurrentDictionary<double, double> KeplerSolutions = new();

    public static double e = 0.1;
    
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
        if(KeplerSolutions.TryGetValue(M, out var result))
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
        yoshida6 = YoshidaIntegrator<double, Vector<double>>.BuildFromLeapfrog(dV, dT, 2);
        // var taskList = new List<Task<(double, double)[]>>();
        // var grid = 100;
        // for (var x = 0; x < grid; x++)
        // {
        //     var q = -2.5 + 5 * x / (double)grid;
        //     var maxvel = Math.Sqrt(2/Math.Sqrt(q*q+1))*1.5; 
        //     for (var y = 0; y < grid; y++)
        //     {
        //         var p = -maxvel + 2 * maxvel * y / (double)grid;
        //         taskList.Add(
        //                     Task.Run(() =>
        //                                 PoincareMap(q,
        //                                             p,
        //                                             1000)
        //                             ));

        //     }
        // }
        // var totalResults = await Task.WhenAll(taskList);

        // using var file = new StreamWriter("result.dat");
        // foreach (var res in totalResults)
        //     foreach (var (q, p) in res)
        //         file.WriteLine($"{q} {p}");

        using var gameWindow = new GameWindow(WindowOptions.DefaultVulkan, yoshida6);

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
                                        0.01,
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