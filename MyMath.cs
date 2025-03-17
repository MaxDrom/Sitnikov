using System.Numerics;

namespace Sitnikov;

public static class MyMath
{
    public static TField Root<TField>(TField x, int k)
        where TField : IFloatingPoint<TField>
    {
        if (x == TField.Zero) return x;

        var result = x;
        var kf = TField.Zero;
        for (var i = 1; i <= k; i++) kf++;

        TField rl;
        var steps = 0;
        do
        {
            rl = result;
            result = ((kf - TField.One) * result +
                      x / Pow(result, k - 1)) / kf;
            steps++;
        } while (TField.CreateTruncating(TField.Abs(result - rl)) !=
                 TField.Zero && steps<100);

        return result;
    }

    public static TField Pow<TField>(TField x, int k)
        where TField : INumber<TField>
    {
        var cache = new Dictionary<int, TField>();
        cache[0] = TField.One;
        cache[1] = x;
        if (k < 0) return TField.One / RecursicePow(x, -k, cache);

        return RecursicePow(x, k, cache);
    }

    private static TField RecursicePow<TField>(TField x,
        int k,
        Dictionary<int, TField> cache)
        where TField : INumber<TField>
    {
        if (cache.TryGetValue(k, out var r)) return r;

        TField result;
        var a = RecursicePow(x, k / 2, cache);
        if (k % 2 == 0)
            result = a * a;
        else
            result = x * a * a;

        cache[k] = result;
        return result;
    }
}