using System.Numerics;

namespace SymplecticIntegrators;

public class YoshidaIntegrator<TField, TSpace> : SymplecticIntegrator<TField, TSpace>
    where TField : IFloatingPoint<TField>
    where TSpace : ILinearSpace<TSpace, TField>
{
    private SymplecticIntegrator<TField, TSpace> _previos;
    private TField _x0;
    private TField _x1;

    public int Order {get; private set;}

    private (TField, TField) FindX(int order)
    {
        Order = order;
        var x1 =  TField.One/(_two - MyMath.Root(_two, 2*order +1));
        var x0 = TField.One - _two*x1;
 
        var fieldorder = (dynamic) order;

        for (var i = 0; i<100; i++)
        {
            var w11 = TField.One;
            var w12 = _two;
            var w21 = _two*MyMath.Pow(x1, 2*order )*(_two*fieldorder + TField.One);
            var w22 = MyMath.Pow(x0, 2*order )*(_two*fieldorder + TField.One);
            var det = w11*w22 - w21*w12;
            var y0 = _two*x1 +x0-TField.One;
            var y1 = _two*MyMath.Pow(x1, 2*order +1) + MyMath.Pow(x0, 2*order +1);

            x0 = x0-(w22*y0-w12*y1)/det;
            x1 = x1-(-w21*y0+w11*y1)/det;
        }
        return (x0, x1);
    }

    private YoshidaIntegrator(int order,
        SymplecticIntegrator<TField, TSpace> previosIntegrator) : base(previosIntegrator.dV, previosIntegrator.dT)
    {
        _previos = previosIntegrator;

        (_x0, _x1) = FindX(order);
    }

    public override (TSpace, TSpace) Step(TSpace q0, TSpace p0, TField tau)
    {
        var (q, p) = _previos.Step(q0, p0, tau * _x1);
        (q, p)= _previos.Step(q, p, tau * _x0);
        return _previos.Step(q, p, tau * _x1);
    }

    public static SymplecticIntegrator<TField, TSpace> BuildFromLeapfrog(Func<TSpace, TSpace> dV, Func<TSpace, TSpace> dT, int order)
    {
        if (order == 1)
            return new Leapfrog<TField, TSpace>(dV, dT);

        var tracer = new Tracer<TField>();
        var integrator = YoshidaIntegrator<TField, Vector<TField>>.BuildFromBasic(new Leapfrog<TField, Vector<TField>>(tracer.dV, tracer.dT), order);
        var (q, p) = integrator.Step(new Vector<TField>([TField.Zero]),new Vector<TField>([TField.Zero]), TField.One);
        return new OptimizedYoshida<TField, TSpace>(tracer.GetReducedSteps(q[0],p[0]), dV, dT);
    }

    private static SymplecticIntegrator<TField, TSpace> BuildFromBasic(SymplecticIntegrator<TField, TSpace> basic, int order)
    {
        if (order == 1)
            return basic;

        var previosIntegrator = basic;
        
        for (int i = 1; i < order; i++)
        {
            previosIntegrator = new YoshidaIntegrator<TField, TSpace>(i, previosIntegrator);
        }
        return previosIntegrator;
    }
}

class OptimizedYoshida<TField, TSpace>: SymplecticIntegrator<TField, TSpace>
    where TField : IFloatingPoint<TField>
    where TSpace : ILinearSpace<TSpace, TField>
{
    private List<(TField, StepType)> _steps;
    private Dictionary<StepType, Func<TField, TSpace, TSpace, (TSpace, TSpace)>> stepTypes;
    public OptimizedYoshida(List<(TField, StepType)> steps, Func<TSpace, TSpace> dV, Func<TSpace, TSpace> dT) : base(dV, dT)
    {
        _steps = steps;
        stepTypes = new ()
        {
            [StepType.dV] = StepByV,
            [StepType.dT] = StepByT
        };
    }
    public override (TSpace, TSpace) Step(TSpace q0, TSpace p0, TField tau)
    {
        var (q, p) = (q0, p0);
        
        foreach(var (_tau, stepType) in _steps)
            (q, p) = stepTypes[stepType](tau*_tau, q, p);
        return (q, p);
    }
}

public enum StepType
{
    dV = 0,
    dT = 1
}

public class Tracer<TField>
    where TField: IFloatingPoint<TField>
{
    private List<(TField, TField, StepType)> _results = new List<(TField, TField, StepType)>();
    public Vector<TField> dV(Vector<TField> q)
    {
        _results.Add((q[0], _p, StepType.dV));
        _q = q[0];
        return new Vector<TField>([TField.One]);
    }
    private TField _q = TField.Zero;
    private TField _p = TField.Zero;
    public Vector<TField> dT(Vector<TField> p)
    {
        _results.Add((_q, p[0], StepType.dT));
        _p = p[0];
        return new Vector<TField>([TField.One]);
    }

    private List<(TField, StepType)> GetTau(TField qEnd, TField pEnd)
    {
        _results.Reverse();
        var res = new List<(TField, StepType)>();
        foreach(var (q, p, type) in _results)
        {
            TField tau;
            if(type == StepType.dV)
            {
                // pEnd = p - tau 
                tau = p - pEnd;
                pEnd = p;
            }
            else
            {
                //qEnd = q + tau
                tau = qEnd-q;
                qEnd = q;
            }
            res.Add((tau, type));
        }

        _results.Reverse();
        res.Reverse();
        return res;
    }

    public List<(TField, StepType)> GetReducedSteps(TField qEnd, TField pEnd)
    {
        var taus = GetTau(qEnd, pEnd);
        var result = new List<(TField, StepType)>();
        for(var i = 0; i<taus.Count-1; i++)
        {
            if(taus[i].Item2 != taus[i+1].Item2)
            {
                result.Add(taus[i]);
                continue;
            }

            taus[i+1] = (taus[i].Item1 + taus[i+1].Item1, taus[i].Item2);
        }

        result.Add(taus[^1]);
        return result;
    }
}