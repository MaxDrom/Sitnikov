using System.Numerics;

namespace SymplecticIntegrators;

public class
    Leapfrog<TField, TSpace> : SymplecticIntegrator<TField, TSpace>
    where TField : IFloatingPoint<TField>
    where TSpace : ILinearSpace<TSpace, TField>
{
    public Leapfrog(Func<TSpace, TSpace> dV, Func<TSpace, TSpace> dT)
        : base(dV, dT)
    {
    }

    public override (TSpace, TSpace) Step(TSpace q0,
        TSpace p0,
        TField tau)
    {
        var result = StepByV(tau / Two, q0, p0);
        result = StepByT(tau, result.Item1, result.Item2);
        return StepByV(tau / Two, result.Item1, result.Item2);
    }
}