using System.Collections;
using System.Numerics;
using Sitnikov.interfaces;

namespace Sitnikov;

public class Vector<TField> : ILinearSpace<Vector<TField>, TField>,
    IEnumerable<TField>
    where TField : INumber<TField>

{
    private readonly TField[] _coords;
    private readonly bool _isZero;

    private Vector(bool isZero = false)
    {
        _coords = [];
        _isZero = isZero;
    }

    public Vector(int n)
    {
        _coords = new TField[n];
    }

    public Vector(IEnumerable<TField> coords)
    {
        _coords = coords.ToArray();
    }

    public TField this[int index]
    {
        get => _coords[index];
        set => _coords[index] = value;
    }

    public int Length => _coords.Length;

    public IEnumerator<TField> GetEnumerator()
    {
        return ((IEnumerable<TField>)_coords).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return _coords.GetEnumerator();
    }

    public static Vector<TField> AdditiveIdentity => new(true);

    public static Vector<TField> operator +(Vector<TField> left,
        Vector<TField> right)
    {
        if (left._isZero) return right;

        if (right._isZero) return left;
#if DEBUG
        if (left.Length != right.Length)
            throw new ArgumentException(
                "Вектора должны иметь одинаковый размер");
#endif
        var result = new Vector<TField>(left.Length);
        for (var i = 0; i < left.Length; i++)
            result[i] = left[i] + right[i];

        return result;
    }

    public static Vector<TField> operator *(TField right,
        Vector<TField> left)
    {
        if (left._isZero) return new Vector<TField>(true);

        var result = new Vector<TField>(left.Length);
        for (var i = 0; i < left.Length; i++)
            result[i] = left[i] * right;

        return result;
    }

    public static TField operator *(Vector<TField> right,
        Vector<TField> left)
    {
        var result = TField.Zero;
        if (left._isZero || right._isZero) return TField.Zero;
#if DEBUG
        if (left.Length != right.Length)
            throw new ArgumentException(
                "Вектора должны иметь одинаковый размер");
#endif
        for (var i = 0; i < left.Length; i++)
            result += right[i] * left[i];

        return result;
    }

    public override string ToString()
    {
        return string.Join(" ", _coords);
    }
}