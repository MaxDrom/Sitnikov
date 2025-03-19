using System.Numerics;

namespace Sitnikov.interfaces;

public interface ILinearSpace<TSelf, in TField> :
    IAdditionOperators<TSelf, TSelf, TSelf>,
    IAdditiveIdentity<TSelf, TSelf>
    where TField : INumber<TField>
    where TSelf : ILinearSpace<TSelf, TField>
{
    static abstract TSelf operator *(TField right, TSelf left);

    static virtual TSelf operator *(TSelf right, TField left)
    {
        return left * right;
    }

    static virtual TSelf operator -(TSelf value)
    {
        return -TField.One * value;
    }

    static virtual TSelf operator -(TSelf left, TSelf right)
    {
        return left + -right;
    }
}