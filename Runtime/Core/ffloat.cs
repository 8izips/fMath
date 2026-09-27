using System;
using System.Globalization;

/// <summary>
/// Signed Q16.16 fixed-point scalar (4 bytes). General gameplay scalar: position, distance, velocity,
/// acceleration, radius, height and tuning parameters. 1.0 == 65536 raw, resolution 1/65536
/// (~0.0153 mm when 1 unit = 1 m), range about -32768 .. +32767.99998.
///
/// Arithmetic widens to 64 bits, rounds to nearest with ties to even and saturates the final value
/// deterministically. Equality is raw-exact; use <see cref="Approximately"/> for tolerance checks.
/// There are no float/double operators: convert at the authoring / presentation boundary only
/// (see fMath.Unity.fMathUnityConversions).
/// </summary>
public readonly struct ffloat : IEquatable<ffloat>, IComparable<ffloat>
{
    public const int FractionalBits = 16;
    public const int RawOne = 1 << FractionalBits;
    const int RAW_HALF = RawOne >> 1;
    const int FRACTION_MASK = RawOne - 1;

    // Raw constants, generated once offline (round-to-nearest of the exact value * 65536).
    // They are never derived from Math.PI at runtime.
    const int RAW_PI = 205887;          // 3.14159265358979 * 65536 = 205887.416
    const int RAW_TWO_PI = 411775;      // 6.28318530717959 * 65536 = 411774.833
    const int RAW_HALF_PI = 102944;     // 1.57079632679490 * 65536 = 102943.708
    const int RAW_DEG2RAD = 1144;       // 0.01745329251994 * 65536 = 1143.830
    const int RAW_RAD2DEG = 3754936;    // 57.2957795130823 * 65536 = 3754936.206

    readonly int _rawValue;

    ffloat(int rawValue) { _rawValue = rawValue; }

    public int RawValue => _rawValue;

    #region Constants
    public static readonly ffloat MaxValue = new ffloat(int.MaxValue);
    public static readonly ffloat MinValue = new ffloat(int.MinValue);
    public static readonly ffloat Zero = new ffloat(0);
    public static readonly ffloat One = new ffloat(RawOne);
    public static readonly ffloat MinusOne = new ffloat(-RawOne);
    public static readonly ffloat Two = new ffloat(2 * RawOne);
    public static readonly ffloat Half = new ffloat(RAW_HALF);
    /// <summary>Smallest positive value (1 raw).</summary>
    public static readonly ffloat Epsilon = new ffloat(1);

    // Radian constants are kept for authoring/presentation maths only. Simulation angles use fAngle.
    public static readonly ffloat Pi = new ffloat(RAW_PI);
    public static readonly ffloat TwoPi = new ffloat(RAW_TWO_PI);
    public static readonly ffloat HalfPi = new ffloat(RAW_HALF_PI);
    public static readonly ffloat Deg2Rad = new ffloat(RAW_DEG2RAD);
    public static readonly ffloat Rad2Deg = new ffloat(RAW_RAD2DEG);
    #endregion

    #region Factories
    public static ffloat FromRaw(int rawValue) => new ffloat(rawValue);

    /// <summary>Integer value; saturates outside [-32768, 32767].</summary>
    public static ffloat FromInt(int value) => new ffloat(fWideMath.SaturateToInt((long)value << FractionalBits));

    /// <summary>numerator / denominator rounded to nearest (ties to even), e.g. FromFraction(1, 3).</summary>
    public static ffloat FromFraction(int numerator, int denominator)
    {
        return new ffloat(fWideMath.SaturateToInt(fWideMath.DivideRoundToEven((long)numerator << FractionalBits, denominator)));
    }

    /// <summary>numerator / denominator for wide inputs, e.g. milli-units: FromFraction(1250L, 1000L).</summary>
    public static ffloat FromFraction(long numerator, long denominator)
    {
        // numerator << 16 must not overflow: fall back to a pre-divided form for huge numerators.
        if (numerator > (long.MaxValue >> FractionalBits) || numerator < (long.MinValue >> FractionalBits))
        {
            long q = fWideMath.DivideRoundToEven(numerator, denominator);
            return new ffloat(fWideMath.SaturateToInt(q > (long.MaxValue >> FractionalBits) ? long.MaxValue : q < (long.MinValue >> FractionalBits) ? long.MinValue : q << FractionalBits));
        }
        return new ffloat(fWideMath.SaturateToInt(fWideMath.DivideRoundToEven(numerator << FractionalBits, denominator)));
    }

    public static explicit operator ffloat(int value) => FromInt(value);
    #endregion

    #region Arithmetic
    public static ffloat operator +(ffloat a, ffloat b) => new ffloat(fWideMath.SaturateToInt((long)a._rawValue + b._rawValue));
    public static ffloat operator -(ffloat a, ffloat b) => new ffloat(fWideMath.SaturateToInt((long)a._rawValue - b._rawValue));
    public static ffloat operator -(ffloat a) => new ffloat(fWideMath.SaturateToInt(-(long)a._rawValue));
    public static ffloat operator +(ffloat a) => a;

    public static ffloat operator +(ffloat a, int b) => new ffloat(fWideMath.SaturateToInt((long)a._rawValue + ((long)b << FractionalBits)));
    public static ffloat operator +(int a, ffloat b) => b + a;
    public static ffloat operator -(ffloat a, int b) => new ffloat(fWideMath.SaturateToInt((long)a._rawValue - ((long)b << FractionalBits)));
    public static ffloat operator -(int a, ffloat b) => new ffloat(fWideMath.SaturateToInt(((long)a << FractionalBits) - b._rawValue));

    public static ffloat operator *(ffloat a, ffloat b) => new ffloat(fWideMath.MulQ16(a._rawValue, b._rawValue));
    public static ffloat operator *(ffloat a, int b) => new ffloat(fWideMath.SaturateToInt((long)a._rawValue * b));
    public static ffloat operator *(int a, ffloat b) => b * a;

    /// <summary>
    /// Q16.16 division with a 64-bit numerator, ties to even. Division by zero returns MaxValue for a
    /// positive dividend, MinValue for a negative one and Zero for 0/0 (reported as DivideByZero).
    /// Use <see cref="TryDivide"/> when the caller must branch on failure.
    /// </summary>
    public static ffloat operator /(ffloat a, ffloat b)
    {
        if (!fWideMath.TryDivQ16(a._rawValue, b._rawValue, out int result))
            fMathValidation.Report(b._rawValue == 0 ? fMathValidationKind.DivideByZero : fMathValidationKind.Saturation, "ffloat division");
        return new ffloat(result);
    }

    public static ffloat operator /(ffloat a, int b)
    {
        return new ffloat(fWideMath.SaturateToInt(fWideMath.DivideRoundToEven(a._rawValue, b)));
    }

    /// <summary>
    /// Remainder with the sign of the dividend (C# semantics). x % 0 returns Zero (reported as
    /// DivideByZero); MinValue % -epsilon returns Zero instead of throwing.
    /// </summary>
    public static ffloat operator %(ffloat a, ffloat b)
    {
        if (b._rawValue == 0)
        {
            fMathValidation.Report(fMathValidationKind.DivideByZero, "ffloat % 0");
            return Zero;
        }
        if (b._rawValue == -1)
            return Zero;
        return new ffloat(a._rawValue % b._rawValue);
    }

    public static bool TryDivide(ffloat a, ffloat b, out ffloat result)
    {
        bool ok = fWideMath.TryDivQ16(a._rawValue, b._rawValue, out int raw);
        result = new ffloat(raw);
        return ok;
    }

    /// <summary>a * b / c with a single rounding and a 64-bit intermediate product.</summary>
    public static ffloat MulDiv(ffloat a, ffloat b, ffloat c)
    {
        if (c._rawValue == 0)
            fMathValidation.Report(fMathValidationKind.DivideByZero, "ffloat MulDiv / 0");
        return new ffloat(fWideMath.SaturateToInt(fWideMath.DivideRoundToEven((long)a._rawValue * b._rawValue, c._rawValue)));
    }
    #endregion

    #region Comparison
    public static bool operator ==(ffloat a, ffloat b) => a._rawValue == b._rawValue;
    public static bool operator !=(ffloat a, ffloat b) => a._rawValue != b._rawValue;
    public static bool operator <(ffloat a, ffloat b) => a._rawValue < b._rawValue;
    public static bool operator >(ffloat a, ffloat b) => a._rawValue > b._rawValue;
    public static bool operator <=(ffloat a, ffloat b) => a._rawValue <= b._rawValue;
    public static bool operator >=(ffloat a, ffloat b) => a._rawValue >= b._rawValue;

    public static bool operator <(ffloat a, int b) => a._rawValue < ((long)b << FractionalBits);
    public static bool operator >(ffloat a, int b) => a._rawValue > ((long)b << FractionalBits);
    public static bool operator <=(ffloat a, int b) => a._rawValue <= ((long)b << FractionalBits);
    public static bool operator >=(ffloat a, int b) => a._rawValue >= ((long)b << FractionalBits);
    public static bool operator <(int a, ffloat b) => ((long)a << FractionalBits) < b._rawValue;
    public static bool operator >(int a, ffloat b) => ((long)a << FractionalBits) > b._rawValue;
    public static bool operator <=(int a, ffloat b) => ((long)a << FractionalBits) <= b._rawValue;
    public static bool operator >=(int a, ffloat b) => ((long)a << FractionalBits) >= b._rawValue;

    public bool Equals(ffloat other) => _rawValue == other._rawValue;
    public override bool Equals(object obj) => obj is ffloat other && other._rawValue == _rawValue;
    public override int GetHashCode() => _rawValue;
    public int CompareTo(ffloat other) => _rawValue.CompareTo(other._rawValue);

    /// <summary>|a - b| &lt;= tolerance, evaluated without overflow.</summary>
    public static bool Approximately(ffloat a, ffloat b, ffloat tolerance)
    {
        long diff = (long)a._rawValue - b._rawValue;
        if (diff < 0) diff = -diff;
        return diff <= tolerance._rawValue;
    }
    #endregion

    #region Rounding
    public static int Sign(ffloat x) => x._rawValue < 0 ? -1 : x._rawValue > 0 ? 1 : 0;

    /// <summary>|x|; Abs(MinValue) saturates to MaxValue.</summary>
    public static ffloat Abs(ffloat x) => x._rawValue >= 0 ? x : new ffloat(fWideMath.SaturateToInt(-(long)x._rawValue));

    /// <summary>Largest integer &lt;= x (towards negative infinity).</summary>
    public static ffloat Floor(ffloat x) => new ffloat(x._rawValue & ~FRACTION_MASK);

    /// <summary>Smallest integer &gt;= x; saturates above 32767.</summary>
    public static ffloat Ceiling(ffloat x)
    {
        if ((x._rawValue & FRACTION_MASK) == 0)
            return x;
        return new ffloat(fWideMath.SaturateToInt((long)(x._rawValue & ~FRACTION_MASK) + RawOne));
    }

    /// <summary>Nearest integer, ties to even (2.5 -> 2, 3.5 -> 4, -2.5 -> -2).</summary>
    public static ffloat Round(ffloat x)
    {
        return new ffloat(fWideMath.SaturateToInt(fWideMath.RoundShiftRightToEven(x._rawValue, FractionalBits) << FractionalBits));
    }

    public static ffloat Truncate(ffloat x) => x._rawValue >= 0 ? Floor(x) : -Floor(-x);

    public int FloorToInt() => _rawValue >> FractionalBits;
    public int CeilToInt() => (int)(-((-(long)_rawValue) >> FractionalBits));
    public int RoundToInt() => (int)fWideMath.RoundShiftRightToEven(_rawValue, FractionalBits);
    #endregion

    #region Min / Max / Clamp / Lerp
    public static ffloat Min(ffloat a, ffloat b) => a._rawValue < b._rawValue ? a : b;
    public static ffloat Max(ffloat a, ffloat b) => a._rawValue > b._rawValue ? a : b;

    public static ffloat Clamp(ffloat value, ffloat min, ffloat max)
    {
        if (value._rawValue < min._rawValue)
            return min;
        if (value._rawValue > max._rawValue)
            return max;
        return value;
    }

    public static ffloat Clamp01(ffloat value) => Clamp(value, Zero, One);

    /// <summary>a + (b - a) * t with t clamped to [0, 1]; the difference is kept in 64 bits.</summary>
    public static ffloat Lerp(ffloat a, ffloat b, ffloat t) => LerpUnclamped(a, b, Clamp01(t));

    public static ffloat LerpUnclamped(ffloat a, ffloat b, ffloat t)
    {
        long diff = (long)b._rawValue - a._rawValue;
        long step = fWideMath.RoundShiftRightToEven(diff * t._rawValue, FractionalBits);
        return new ffloat(fWideMath.SaturateToInt(a._rawValue + step));
    }

    /// <summary>Moves current towards target by at most maxDelta (maxDelta &gt;= 0).</summary>
    public static ffloat MoveTowards(ffloat current, ffloat target, ffloat maxDelta)
    {
        long diff = (long)target._rawValue - current._rawValue;
        if (diff <= maxDelta._rawValue && diff >= -(long)maxDelta._rawValue)
            return target;
        return new ffloat(fWideMath.SaturateToInt(current._rawValue + (diff > 0 ? (long)maxDelta._rawValue : -(long)maxDelta._rawValue)));
    }
    #endregion

    #region Sqrt
    /// <summary>
    /// sqrt(x) rounded to nearest in Q16.16: integer_sqrt(raw &lt;&lt; 16). Monotonic over the whole
    /// non-negative range. Returns false for negative input (result Zero).
    /// </summary>
    public static bool TrySqrt(ffloat x, out ffloat result)
    {
        if (x._rawValue < 0)
        {
            result = Zero;
            return false;
        }
        result = new ffloat((int)fWideMath.IntegerSqrtRounded((ulong)x._rawValue << FractionalBits));
        return true;
    }

    /// <summary>
    /// sqrt(x). A negative input is a caller bug: it is reported as NegativeSqrt in validation builds
    /// and returns Zero deterministically in release builds. Use <see cref="TrySqrt"/> to branch.
    /// </summary>
    public static ffloat Sqrt(ffloat x)
    {
        if (!TrySqrt(x, out ffloat result))
            fMathValidation.Report(fMathValidationKind.NegativeSqrt, "ffloat.Sqrt(negative)");
        return result;
    }

    /// <summary>sqrt of a wide Q32.32 value (e.g. a sum of Q16 squares) returned as Q16.16, saturated.</summary>
    public static ffloat SqrtWide(ulong q32)
    {
        return new ffloat(fWideMath.SaturateToInt(fWideMath.IntegerSqrtRounded(q32)));
    }
    #endregion

    #region Presentation / debug
    /// <summary>Presentation and debugging only. Never feed the result back into the simulation.</summary>
    public double ToDouble() => _rawValue / (double)RawOne;

    /// <summary>Presentation and debugging only. Never feed the result back into the simulation.</summary>
    public float ToFloat() => (float)(_rawValue / (double)RawOne);

    public override string ToString() => ToDouble().ToString("0.#####", CultureInfo.InvariantCulture);
    #endregion
}
