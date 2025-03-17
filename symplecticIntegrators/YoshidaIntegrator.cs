using System.Numerics;

namespace SymplecticIntegrators;

public class
    YoshidaIntegrator<TField, TSpace> : SymplecticIntegrator<TField,
    TSpace>
    where TField : IFloatingPoint<TField>
    where TSpace : ILinearSpace<TSpace, TField>
{
    private readonly SymplecticIntegrator<TField, TSpace> _previous;

    private readonly TField _x0;
    private readonly TField _x1;

    private YoshidaIntegrator(int order,
        SymplecticIntegrator<TField, TSpace> previousIntegrator) :
        base(previousIntegrator.DV, previousIntegrator.DT)
    {
        _previous = previousIntegrator;

        (_x0, _x1) = FindX(order);
    }

    public int Order { get; private set; }

    private (TField, TField) FindX(int order)
    {
        Order = order;
        var x1 = TField.One /
                 (Two - MyMath.Root(Two, 2 * order + 1));
        var x0 = TField.One - Two * x1;

        var fieldorder = (dynamic)order;

        for (var i = 0; i < 100; i++)
        {
            var w11 = TField.One;
            var w12 = Two;
            var w21 = Two * MyMath.Pow(x1, 2 * order) *
                      (Two * fieldorder + TField.One);
            var w22 = MyMath.Pow(x0, 2 * order) *
                      (Two * fieldorder + TField.One);
            var det = w11 * w22 - w21 * w12;
            var y0 = Two * x1 + x0 - TField.One;
            var y1 = Two * MyMath.Pow(x1, 2 * order + 1) +
                     MyMath.Pow(x0, 2 * order + 1);

            x0 = x0 - (w22 * y0 - w12 * y1) / det;
            x1 = x1 - (-w21 * y0 + w11 * y1) / det;
        }

        return (x0, x1);
    }

    public override (TSpace, TSpace) Step(TSpace q0,
        TSpace p0,
        TField tau)
    {
        var (q, p) = _previous.Step(q0, p0, tau * _x1);
        (q, p) = _previous.Step(q, p, tau * _x0);
        return _previous.Step(q, p, tau * _x1);
    }

    public static SymplecticIntegrator<TField, TSpace>
        BuildFromLeapfrog(Func<TSpace, TSpace> dV,
            Func<TSpace, TSpace> dT,
            int order)
    {
        if (order == 1) return new Leapfrog<TField, TSpace>(dV, dT);

        var tracer = new Tracer<TField>();
        var integrator =
            YoshidaIntegrator<TField, Vector<TField>>.BuildFromBasic(
                new Leapfrog<TField, Vector<TField>>(tracer.DV,
                    tracer.DT), order);
        var (q, p) = integrator.Step(
            new Vector<TField>([TField.Zero]),
            new Vector<TField>([TField.Zero]), TField.One);
        return new OptimizedYoshida<TField, TSpace>(
            tracer.GetReducedSteps(q[0], p[0]), dV, dT);
    }

    private static SymplecticIntegrator<TField, TSpace>
        BuildFromBasic(SymplecticIntegrator<TField, TSpace> basic,
            int order)
    {
        if (order == 1) return basic;

        var previosIntegrator = basic;

        for (var i = 1; i < order; i++)
            previosIntegrator =
                new YoshidaIntegrator<TField, TSpace>(i,
                    previosIntegrator);

        return previosIntegrator;
    }
}

internal class
    OptimizedYoshida<TField, TSpace> : SymplecticIntegrator<TField,
    TSpace>
    where TField : IFloatingPoint<TField>
    where TSpace : ILinearSpace<TSpace, TField>
{
    private readonly List<(TField, StepType)> _steps;

    private readonly
        Dictionary<StepType, Func<TField, TSpace, TSpace, (
            TSpace, TSpace)>> _stepTypes;

    public OptimizedYoshida(List<(TField, StepType)> steps,
        Func<TSpace, TSpace> dV,
        Func<TSpace, TSpace> dT) : base(dV,
        dT)
    {
        _steps = steps;
        _stepTypes =
            new Dictionary<StepType, Func<TField, TSpace, TSpace, (
                TSpace, TSpace)>>
            {
                [StepType.DV] = StepByV, [StepType.DT] = StepByT
            };
    }

    public override (TSpace, TSpace) Step(TSpace q0,
        TSpace p0,
        TField tau)
    {
        var (q, p) = (q0, p0);

        foreach (var (t, stepType) in _steps)
            (q, p) = _stepTypes[stepType](tau * t, q, p);

        return (q, p);
    }
}

public enum StepType
{
    DV = 0,
    DT = 1
}

public class Tracer<TField>
    where TField : IFloatingPoint<TField>
{
    private readonly List<(TField, TField, StepType)>
        _results = new();

    private TField _p = TField.Zero;
    private TField _q = TField.Zero;

    public Vector<TField> DV(Vector<TField> q)
    {
        _results.Add((q[0], _p, StepType.DV));
        _q = q[0];
        return new Vector<TField>([TField.One]);
    }

    public Vector<TField> DT(Vector<TField> p)
    {
        _results.Add((_q, p[0], StepType.DT));
        _p = p[0];
        return new Vector<TField>([TField.One]);
    }

    private List<(TField, StepType)> GetTau(TField qEnd, TField pEnd)
    {
        _results.Reverse();
        var res = new List<(TField, StepType)>();
        foreach (var (q, p, type) in _results)
        {
            TField tau;
            if (type == StepType.DV)
            {
                // pEnd = p - tau
                tau = p - pEnd;
                pEnd = p;
            }
            else
            {
                // qEnd = q + tau
                tau = qEnd - q;
                qEnd = q;
            }

            res.Add((tau, type));
        }

        _results.Reverse();
        res.Reverse();
        return res;
    }

    public List<(TField, StepType)> GetReducedSteps(TField qEnd,
        TField pEnd)
    {
        var taus = GetTau(qEnd, pEnd);
        var result = new List<(TField, StepType)>();
        for (var i = 0; i < taus.Count - 1; i++)
        {
            if (taus[i].Item2 != taus[i + 1].Item2)
            {
                result.Add(taus[i]);
                continue;
            }

            taus[i + 1] = (taus[i].Item1 + taus[i + 1].Item1,
                taus[i].Item2);
        }

        result.Add(taus[^1]);
        return result;
    }
}