using System.Numerics;

namespace SymplecticIntegrators;
public static class MyMath
{

    public static int Factorial(int k)
    {
        var result = 1;
        for (int i = 1; i <= k; i++)
        {
            result *= i;
        }
        return result;
    }
    public static TField Root<TField>(TField x, int k)
        where TField : IFloatingPoint<TField>
    {
        if (x == TField.Zero)
            return x;
        var result = x;
        var kf = TField.Zero;
        for (int i = 1; i <= k; i++)
            kf++;
        TField rl;
        do
        {
            rl = result;
            result = ((kf - TField.One) * result + x / Pow(result, k - 1)) / kf;
        }
        while (TField.CreateTruncating(TField.Abs(result - rl)) != TField.Zero);
        return result;
    }

    public static TField Pow<TField>(TField x, int k)
        where TField : INumber<TField>
    {
        Dictionary<int, TField> cache = new Dictionary<int, TField>();
        cache[0] = TField.One;
        cache[1] = x;
        if (k < 0)
            return TField.One / RecursicePow(x, -k, cache);
        return RecursicePow(x, k, cache);
    }

    public static TField Sin<TField>(TField x)
        where TField : IFloatingPoint<TField>
    {
        var two = TField.One+ TField.One;
        if(TField.Abs(x)>= TField.One)
        {
            var s = Sin(x/two);
            return two*s*Root(TField.One-s*s, 2);
        }

        var order = TField.One;
        var stepval = x;
        var res = TField.Zero;
        var xx = x*x;
        while(TField.CreateTruncating(TField.Abs(stepval)) != TField.Zero)
        {
            res+=stepval;
            order++;
            stepval *= xx/order;
            order++;
            stepval *=-TField.One/order;
        }
        return res;
    }

    private static TField RecursicePow<TField>(TField x, int k, Dictionary<int, TField> cache)
        where TField : INumber<TField>
    {
        if (cache.TryGetValue(k, out var r))
            return r;

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