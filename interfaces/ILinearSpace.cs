using System.Numerics;

namespace SymplecticIntegrators;

public interface ILinearSpace<TSelf, TField> : IAdditionOperators<TSelf, TSelf, TSelf>,
                                               IAdditiveIdentity<TSelf, TSelf>,
                                               IMultiplyOperators<TSelf, TField, TSelf>,
                                               IUnaryNegationOperators<TSelf, TSelf>,
                                               ISubtractionOperators<TSelf,TSelf,TSelf>
    where TField : INumber<TField>
    where TSelf : ILinearSpace<TSelf, TField>
{
    static abstract TSelf operator *(TField right, TSelf left);
}

