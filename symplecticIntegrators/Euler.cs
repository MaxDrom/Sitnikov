using System.Numerics;

namespace SymplecticIntegrators;

public class SymplecticEuler<TField, TSpace> : SymplecticIntegrator<TField, TSpace>
    where TField : IFloatingPoint<TField>
    where TSpace : ILinearSpace<TSpace, TField>
{
    public SymplecticEuler(Func<TSpace, TSpace> dV, Func<TSpace, TSpace> dT) : base(dV, dT)
    {
    }

    public override (TSpace, TSpace) Step(TSpace q0, TSpace p0, TField tau)
    {
        var (q, p) = StepByV(tau, q0, p0);
        (q, p) = StepByT(tau, q, p);
        return (q, p);
    }
}