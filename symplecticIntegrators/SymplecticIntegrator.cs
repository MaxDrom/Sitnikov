using System.Numerics;

namespace SymplecticIntegrators;
public abstract class SymplecticIntegrator<TField, TSpace>
    where TField : IFloatingPoint<TField>
    where TSpace : ILinearSpace<TSpace, TField>
{
    protected TField _two;

    public Func<TSpace, TSpace> dV => _dV;
    public Func<TSpace, TSpace> dT => _dT;
    protected Func<TSpace, TSpace> _dV;
    protected Func<TSpace, TSpace> _dT;
    public SymplecticIntegrator(Func<TSpace, TSpace> dV, Func<TSpace, TSpace> dT)
    {
        _two = TField.One + TField.One;
        _dV = dV;
        _dT = dT;
    }

    protected (TSpace, TSpace) StepByV(TField tau, TSpace q, TSpace p)
    {
        return (q, p-_dV(q)*tau);
    }

    protected (TSpace, TSpace) StepByT(TField tau, TSpace q, TSpace p)
    {
        return (q+_dT(p)*tau, p);
    }

    public abstract (TSpace, TSpace) Step(TSpace q0, TSpace p0, TField tau);
}

public static class SymplecticIntegratorExtensions
{
    public static IEnumerable<(TField, TSpace, TSpace)> Integrate<TField, TSpace>(this SymplecticIntegrator<TField, TSpace>integrator, 
            TField T, TField tau, TSpace q0, TSpace p0)
        where TField : IFloatingPoint<TField>
        where TSpace : ILinearSpace<TSpace, TField>
    {
        TField t = TField.Zero;
        var (q, p) = (q0, p0);
        do
        {
            yield return (t, q, p);
            (q, p) = integrator.Step(q, p, tau);
            t+=tau;
        }
        while (t <= T);
    }
}