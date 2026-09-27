// Deterministic 64-bit intermediate arithmetic shared by every fMath v2 type.
//
// Rules applied everywhere in fMath v2:
//  * 32-bit raw state is widened to int64/uint64 before any multiply, sum of products or shift.
//  * Rounding back to a narrower format is round-to-nearest, ties-to-even, symmetric for negatives.
//  * A final value that cannot be represented saturates deterministically (and is reported when
//    FMATH_VALIDATE is defined).
//  * No floating point, no System.Math, no platform intrinsics: the result is bit-identical on
//    Mono, IL2CPP, x64 and ARM64.

internal static class fWideMath
{
    internal const int Q16 = 16;
    internal const int Q30 = 30;

    #region Saturation
    internal static int SaturateToInt(long value)
    {
        if (value > int.MaxValue)
        {
            fMathValidation.Report(fMathValidationKind.Saturation, "int32 saturation (positive)");
            return int.MaxValue;
        }
        if (value < int.MinValue)
        {
            fMathValidation.Report(fMathValidationKind.Saturation, "int32 saturation (negative)");
            return int.MinValue;
        }
        return (int)value;
    }

    internal static int SaturateToInt(ulong value)
    {
        if (value > int.MaxValue)
        {
            fMathValidation.Report(fMathValidationKind.Saturation, "int32 saturation (positive)");
            return int.MaxValue;
        }
        return (int)value;
    }

    internal static long AddSaturating(long a, long b)
    {
        long r = unchecked(a + b);
        if (((a ^ r) & (b ^ r)) < 0)
        {
            fMathValidation.Report(fMathValidationKind.Saturation, "int64 add saturation");
            return a < 0 ? long.MinValue : long.MaxValue;
        }
        return r;
    }

    internal static ulong AddSaturating(ulong a, ulong b)
    {
        ulong r = unchecked(a + b);
        if (r < a)
        {
            fMathValidation.Report(fMathValidationKind.Saturation, "uint64 add saturation");
            return ulong.MaxValue;
        }
        return r;
    }
    #endregion

    #region Abs
    /// <summary>|value| without the int.MinValue trap of Math.Abs.</summary>
    internal static long AbsToLong(int value) => value < 0 ? -(long)value : value;

    /// <summary>|value| as ulong, valid for long.MinValue.</summary>
    internal static ulong AbsToULong(long value) => value < 0 ? (ulong)(-(value + 1)) + 1UL : (ulong)value;

    internal static int BitLength(ulong value)
    {
        int bits = 0;
        if (value >= 1UL << 32) { value >>= 32; bits += 32; }
        if (value >= 1UL << 16) { value >>= 16; bits += 16; }
        if (value >= 1UL << 8) { value >>= 8; bits += 8; }
        if (value >= 1UL << 4) { value >>= 4; bits += 4; }
        if (value >= 1UL << 2) { value >>= 2; bits += 2; }
        if (value >= 1UL << 1) { value >>= 1; bits += 1; }
        return bits + (int)value;
    }
    #endregion

    #region Rounding
    /// <summary>
    /// value / 2^shift rounded to nearest, ties to even. Symmetric: f(-v) == -f(v).
    /// shift must be in [0, 62].
    /// </summary>
    internal static long RoundShiftRightToEven(long value, int shift)
    {
        if (shift <= 0)
            return value;

        long quotient = value >> shift; // floor
        long remainder = value & ((1L << shift) - 1L);
        long half = 1L << (shift - 1);
        if (remainder > half || (remainder == half && (quotient & 1L) != 0))
            quotient++;
        return quotient;
    }

    internal static ulong RoundShiftRightToEven(ulong value, int shift)
    {
        if (shift <= 0)
            return value;

        ulong quotient = value >> shift;
        ulong remainder = value & ((1UL << shift) - 1UL);
        ulong half = 1UL << (shift - 1);
        if (remainder > half || (remainder == half && (quotient & 1UL) != 0))
            quotient++;
        return quotient;
    }

    /// <summary>
    /// numerator / denominator rounded to nearest, ties to even. Symmetric in sign.
    /// denominator == 0 saturates by the sign of the numerator (0 for 0/0) and reports DivideByZero.
    /// The only unrepresentable quotient (long.MinValue / -1) saturates to long.MaxValue.
    /// </summary>
    internal static long DivideRoundToEven(long numerator, long denominator)
    {
        if (denominator == 0)
        {
            fMathValidation.Report(fMathValidationKind.DivideByZero, "wide divide by zero");
            return numerator > 0 ? long.MaxValue : numerator < 0 ? long.MinValue : 0L;
        }

        bool negative = (numerator < 0) != (denominator < 0);
        ulong n = AbsToULong(numerator);
        ulong d = AbsToULong(denominator);
        ulong q = n / d;
        ulong r = n % d;
        ulong rest = d - r;
        if (r > rest || (r == rest && (q & 1UL) != 0))
            q++;

        if (negative)
            return q >= 1UL << 63 ? long.MinValue : -(long)q;

        if (q > long.MaxValue)
        {
            fMathValidation.Report(fMathValidationKind.Saturation, "wide divide saturation");
            return long.MaxValue;
        }
        return (long)q;
    }

    internal static ulong DivideRoundToEven(ulong numerator, ulong denominator)
    {
        if (denominator == 0)
        {
            fMathValidation.Report(fMathValidationKind.DivideByZero, "wide divide by zero");
            return numerator > 0 ? ulong.MaxValue : 0UL;
        }

        ulong q = numerator / denominator;
        ulong r = numerator % denominator;
        ulong rest = denominator - r;
        if (r > rest || (r == rest && (q & 1UL) != 0))
            q++;
        return q;
    }
    #endregion

    #region Square root
    /// <summary>floor(sqrt(value)) for the full uint64 range (digit-by-digit, exact).</summary>
    internal static ulong IntegerSqrt(ulong value)
    {
        if (value == 0)
            return 0;
        ulong result = 0;
        ulong bit = 1UL << ((BitLength(value) - 1) & ~1); // highest power of four <= value

        while (bit != 0)
        {
            ulong trial = result + bit;
            if (value >= trial)
            {
                value -= trial;
                result = (result >> 1) + bit;
            }
            else
            {
                result >>= 1;
            }
            bit >>= 2;
        }
        return result;
    }

    /// <summary>
    /// sqrt(value) rounded to the nearest integer. An exact tie cannot occur for integer input.
    /// Monotonic non-decreasing in value.
    /// </summary>
    internal static ulong IntegerSqrtRounded(ulong value)
    {
        ulong s = IntegerSqrt(value);
        // (s + 0.5)^2 = s^2 + s + 0.25, so round up when value - s^2 > s.
        return value - s * s > s ? s + 1UL : s;
    }
    #endregion

    #region Fixed point products
    /// <summary>Q16.16 * Q16.16 -> Q16.16, ties-to-even, saturated.</summary>
    internal static int MulQ16(int aRaw, int bRaw)
    {
        return SaturateToInt(RoundShiftRightToEven((long)aRaw * bRaw, Q16));
    }

    /// <summary>Q1.30 * Q1.30 -> Q1.30, ties-to-even, saturated.</summary>
    internal static int MulQ30(int aRaw, int bRaw)
    {
        return SaturateToInt(RoundShiftRightToEven((long)aRaw * bRaw, Q30));
    }

    /// <summary>Q16.16 * Q1.30 -> Q16.16, ties-to-even, saturated.</summary>
    internal static int MulQ16ByQ30(int q16Raw, int q30Raw)
    {
        return SaturateToInt(RoundShiftRightToEven((long)q16Raw * q30Raw, Q30));
    }

    /// <summary>
    /// Q16.16 / Q16.16 -> Q16.16 using a 64-bit numerator, ties-to-even.
    /// Returns false on divide by zero (result saturated by the sign of a, 0 for 0/0)
    /// or when the quotient is not representable (result saturated).
    /// </summary>
    internal static bool TryDivQ16(int aRaw, int bRaw, out int resultRaw)
    {
        if (bRaw == 0)
        {
            resultRaw = aRaw > 0 ? int.MaxValue : aRaw < 0 ? int.MinValue : 0;
            return false;
        }

        long q = DivideRoundToEven((long)aRaw << Q16, bRaw);
        if (q > int.MaxValue)
        {
            resultRaw = int.MaxValue;
            return false;
        }
        if (q < int.MinValue)
        {
            resultRaw = int.MinValue;
            return false;
        }
        resultRaw = (int)q;
        return true;
    }
    #endregion

    #region Normalization
    /// <summary>
    /// Scale-invariant normalization of up to four wide components into Q1.30.
    /// Any common fixed-point scale of the inputs (Q16, Q30, Q32, Q60 ...) is irrelevant, so the
    /// same routine serves vectors, wide cross products and quaternions. Pass 0 for unused components.
    /// The inputs are first shifted so that the largest magnitude has 31 (three components) or 30
    /// (four components) significant bits, so a 1-raw vector normalizes as accurately as a huge one.
    /// Symmetric: normalize(-v) == -normalize(v). Returns false only for the zero vector.
    /// </summary>
    internal static bool TryNormalizeQ30(long x, long y, long z, long w, out int ox, out int oy, out int oz, out int ow)
    {
        ulong m = AbsToULong(x);
        ulong t = AbsToULong(y); if (t > m) m = t;
        t = AbsToULong(z); if (t > m) m = t;
        t = AbsToULong(w); if (t > m) m = t;

        if (m == 0)
        {
            ox = oy = oz = ow = 0;
            return false;
        }

        // Three components (w == 0): scale the largest magnitude into [2^30, 2^31); the squared sum
        // stays below 3 * 2^62 and every int32 input is used without rounding.
        // Four components: scale into [2^29, 2^30) so that 4 * squares < 2^62 and the length can be
        // taken with one extra bit (sqrt(4 * sum)).
        bool three = w == 0;
        int targetBits = three ? 31 : 30;
        long limit = 1L << targetBits;
        int shift = targetBits - BitLength(m);
        long sx, sy, sz, sw;
        if (shift >= 0)
        {
            sx = x << shift; sy = y << shift; sz = z << shift; sw = w << shift;
        }
        else
        {
            int s = -shift;
            while (true)
            {
                sx = RoundShiftRightToEven(x, s);
                sy = RoundShiftRightToEven(y, s);
                sz = RoundShiftRightToEven(z, s);
                sw = RoundShiftRightToEven(w, s);
                // rounding may carry the largest component up to exactly the limit; shift once more then
                if (sx < limit && sx > -limit && sy < limit && sy > -limit &&
                    sz < limit && sz > -limit && sw < limit && sw > -limit)
                    break;
                s++;
            }
        }

        ulong sum = (ulong)(sx * sx) + (ulong)(sy * sy) + (ulong)(sz * sz) + (ulong)(sw * sw);
        int numeratorShift;
        long den;
        if (three)
        {
            den = (long)IntegerSqrtRounded(sum);           // |v| >= 2^30, sum < 3 * 2^62
            numeratorShift = 30;
        }
        else
        {
            den = (long)IntegerSqrtRounded(sum << 2);      // 2|v| >= 2^30, sum < 2^62
            numeratorShift = 31;
        }
        ox = (int)DivideRoundToEven(sx << numeratorShift, den);
        oy = (int)DivideRoundToEven(sy << numeratorShift, den);
        oz = (int)DivideRoundToEven(sz << numeratorShift, den);
        ow = (int)DivideRoundToEven(sw << numeratorShift, den);
        return true;
    }

    /// <summary>
    /// Euclidean length of up to four wide components, returned together with the right shift
    /// that was applied to keep the squared sum inside uint64 (length == result << shift).
    /// </summary>
    internal static ulong LengthWide(long x, long y, long z, long w, out int shift)
    {
        ulong m = AbsToULong(x);
        ulong t = AbsToULong(y); if (t > m) m = t;
        t = AbsToULong(z); if (t > m) m = t;
        t = AbsToULong(w); if (t > m) m = t;

        shift = 0;
        int bits = BitLength(m);
        if (bits > 31)
        {
            shift = bits - 31;
            x = RoundShiftRightToEven(x, shift);
            y = RoundShiftRightToEven(y, shift);
            z = RoundShiftRightToEven(z, shift);
            w = RoundShiftRightToEven(w, shift);
        }
        // every component now satisfies |c| <= 2^31, so each square <= 2^62 and the sum < 2^64
        ulong sum = (ulong)(x * x);
        sum += (ulong)(y * y);
        sum += (ulong)(z * z);
        sum = AddSaturating(sum, (ulong)(w * w));
        return IntegerSqrtRounded(sum);
    }
    #endregion
}
