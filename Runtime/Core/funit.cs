using System;
using System.Globalization;

/// <summary>
/// Signed Q1.30 fixed-point scalar (4 bytes) for values whose natural domain is [-1, 1]:
/// direction and normal components, quaternion components and sin/cos results.
/// 1.0 == 2^30 raw (resolution ~9.3e-10). The raw range technically spans [-2, 2) so that
/// intermediate sums can be represented; validation builds report values outside [-1, 1] where a
/// unit value is required.
/// </summary>
public readonly struct funit : IEquatable<funit>, IComparable<funit>
{
    public const int FractionalBits = 30;
    public const int RawOne = 1 << FractionalBits;
    const int Q16_TO_Q30_SHIFT = FractionalBits - ffloat.FractionalBits;

    readonly int _rawValue;

    funit(int rawValue) { _rawValue = rawValue; }

    public int RawValue => _rawValue;

    public static readonly funit Zero = new funit(0);
    public static readonly funit One = new funit(RawOne);
    public static readonly funit MinusOne = new funit(-RawOne);
    public static readonly funit Half = new funit(RawOne >> 1);
    public static readonly funit Epsilon = new funit(1);

    public static funit FromRaw(int rawValue) => new funit(rawValue);

    public static funit FromFraction(int numerator, int denominator)
    {
        return new funit(fWideMath.SaturateToInt(fWideMath.DivideRoundToEven((long)numerator << FractionalBits, denominator)));
    }

    /// <summary>Converts a Q16.16 scalar; values outside [-1, 1] are reported and saturate at [-2, 2).</summary>
    public static funit FromFfloat(ffloat value)
    {
        funit result = new funit(fWideMath.SaturateToInt((long)value.RawValue << Q16_TO_Q30_SHIFT));
        if (!result.IsInUnitRange)
            fMathValidation.Report(fMathValidationKind.UnitOutOfRange, "funit.FromFfloat outside [-1, 1]");
        return result;
    }

    /// <summary>Q1.30 -> Q16.16 with ties-to-even rounding.</summary>
    public ffloat ToFfloat() => ffloat.FromRaw((int)fWideMath.RoundShiftRightToEven(_rawValue, Q16_TO_Q30_SHIFT));

    public bool IsInUnitRange => _rawValue >= -RawOne && _rawValue <= RawOne;

    /// <summary>Clamps into [-1, 1].</summary>
    public static funit ClampUnit(funit value)
    {
        if (value._rawValue > RawOne) return One;
        if (value._rawValue < -RawOne) return MinusOne;
        return value;
    }

    #region Arithmetic
    public static funit operator -(funit a) => new funit(fWideMath.SaturateToInt(-(long)a._rawValue));
    public static funit operator +(funit a, funit b) => new funit(fWideMath.SaturateToInt((long)a._rawValue + b._rawValue));
    public static funit operator -(funit a, funit b) => new funit(fWideMath.SaturateToInt((long)a._rawValue - b._rawValue));
    public static funit operator *(funit a, funit b) => new funit(fWideMath.MulQ30(a._rawValue, b._rawValue));

    /// <summary>Scales a Q16.16 value by a unit scalar; the product is kept in 64 bits.</summary>
    public static ffloat operator *(funit a, ffloat b) => ffloat.FromRaw(fWideMath.MulQ16ByQ30(b.RawValue, a._rawValue));
    public static ffloat operator *(ffloat a, funit b) => ffloat.FromRaw(fWideMath.MulQ16ByQ30(a.RawValue, b._rawValue));

    public static funit Abs(funit a) => a._rawValue >= 0 ? a : -a;
    public static funit Min(funit a, funit b) => a._rawValue < b._rawValue ? a : b;
    public static funit Max(funit a, funit b) => a._rawValue > b._rawValue ? a : b;
    #endregion

    #region Comparison
    public static bool operator ==(funit a, funit b) => a._rawValue == b._rawValue;
    public static bool operator !=(funit a, funit b) => a._rawValue != b._rawValue;
    public static bool operator <(funit a, funit b) => a._rawValue < b._rawValue;
    public static bool operator >(funit a, funit b) => a._rawValue > b._rawValue;
    public static bool operator <=(funit a, funit b) => a._rawValue <= b._rawValue;
    public static bool operator >=(funit a, funit b) => a._rawValue >= b._rawValue;

    public bool Equals(funit other) => _rawValue == other._rawValue;
    public override bool Equals(object obj) => obj is funit other && other._rawValue == _rawValue;
    public override int GetHashCode() => _rawValue;
    public int CompareTo(funit other) => _rawValue.CompareTo(other._rawValue);

    public static bool Approximately(funit a, funit b, int toleranceRaw)
    {
        long diff = (long)a._rawValue - b._rawValue;
        return diff <= toleranceRaw && diff >= -(long)toleranceRaw;
    }
    #endregion

    /// <summary>Presentation and debugging only.</summary>
    public double ToDouble() => _rawValue / (double)RawOne;

    public override string ToString() => ToDouble().ToString("0.#########", CultureInfo.InvariantCulture);
}
