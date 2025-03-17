using System.Numerics;
using Sitnikov.interfaces;

namespace Sitnikov.symplecticIntegrators;

public abstract class SymplecticIntegrator<TField, TSpace>
    where TField : IFloatingPoint<TField>
    where TSpace : ILinearSpace<TSpace, TField>
{
    protected Func<TSpace, TSpace> _dT;
    protected Func<TSpace, TSpace> _dV;
    protected TField Two;

    public SymplecticIntegrator(Func<TSpace, TSpace> dV,
        Func<TSpace, TSpace> dT)
    {
        Two = TField.One + TField.One;
        _dV = dV;
        _dT = dT;
    }

    public Func<TSpace, TSpace> DV => _dV;

    public Func<TSpace, TSpace> DT => _dT;

    protected (TSpace, TSpace) StepByV(TField tau, TSpace q, TSpace p)
    {
        return (q, p - DV(q) * tau);
    }

    protected (TSpace, TSpace) StepByT(TField tau, TSpace q, TSpace p)
    {
        return (q + DT(p) * tau, p);
    }

    public abstract (TSpace, TSpace) Step(TSpace q0,
        TSpace p0,
        TField tau);
}

public static class SymplecticIntegratorExtensions
{
    public static IEnumerable<(TField, TSpace, TSpace)>
        Integrate<TField, TSpace>(
            this SymplecticIntegrator<TField, TSpace> integrator,
            TField T,
            TField tau,
            TSpace q0,
            TSpace p0)
        where TField : IFloatingPoint<TField>
        where TSpace : ILinearSpace<TSpace, TField>
    {
        var t = TField.Zero;
        var (q, p) = (q0, p0);
        do
        {
            yield return (t, q, p);
            (q, p) = integrator.Step(q, p, tau);
            t += tau;
        } while (t <= T);
    }
}