/// <summary>
/// Deterministic trigonometry on <see cref="fAngle"/>.
///
/// Sin/Cos use a quarter-wave table (4096 intervals + end point, Q1.30) indexed by the upper bits of
/// the Angle32 value and linearly interpolated with the lower 18 bits in 64-bit arithmetic.
/// Quadrant folding is exact, so Sin(-a) == -Sin(a), Sin(a + 180°) == -Sin(a),
/// Cos(a) == Sin(a + 90°), and Sin(90°) == 1, Cos(0) == 1, Tan(45°) == 1 exactly.
/// Maximum interpolation error is ~20 raw Q1.30 (~1.9e-8).
///
/// Atan2 folds into the first octant, computes the ratio in Q1.30 and interpolates an atan table
/// (4096 intervals) in Angle32 units; the error is a few raw angle units (~5e-9 rad) and the result
/// is monotonic within each octant and continuous across octant boundaries.
/// No floating point and no runtime table generation are involved.
/// </summary>
public static class fTrig
{
    const int QUARTER_BITS = 30;
    const uint QUARTER_MASK = (1u << QUARTER_BITS) - 1u;
    const uint RAW_QUARTER = 1u << QUARTER_BITS;
    const int LUT_INDEX_SHIFT = QUARTER_BITS - 12; // 4096 intervals per quarter turn
    const uint LUT_FRACTION_MASK = (1u << LUT_INDEX_SHIFT) - 1u;
    const int ATAN_INDEX_SHIFT = funit.FractionalBits - 12; // ratio in Q1.30, 4096 intervals
    const uint ATAN_FRACTION_MASK = (1u << ATAN_INDEX_SHIFT) - 1u;

    #region Sin / Cos
    /// <summary>sin over [0°, 90°] for p in [0, 2^30] (quarter-turn position).</summary>
    static int SinQuarter(uint p)
    {
        int[] table = fTrigSinQuarterLut.Table;
        int index = (int)(p >> LUT_INDEX_SHIFT);
        uint fraction = p & LUT_FRACTION_MASK;
        int a = table[index];
        if (fraction == 0)
            return a;
        int b = table[index + 1];
        return a + (int)fWideMath.RoundShiftRightToEven((long)(b - a) * fraction, LUT_INDEX_SHIFT);
    }

    static int SinRaw(uint angle)
    {
        uint p = angle & QUARTER_MASK;
        switch (angle >> QUARTER_BITS)
        {
            case 0: return SinQuarter(p);
            case 1: return SinQuarter(RAW_QUARTER - p);
            case 2: return -SinQuarter(p);
            default: return -SinQuarter(RAW_QUARTER - p);
        }
    }

    public static funit Sin(fAngle angle) => funit.FromRaw(SinRaw(angle.RawValue));

    public static funit Cos(fAngle angle) => funit.FromRaw(SinRaw(unchecked(angle.RawValue + RAW_QUARTER)));

    public static funit Sin(fAngleDelta angle) => Sin(angle.ToAngle());

    public static funit Cos(fAngleDelta angle) => Cos(angle.ToAngle());

    public static void SinCos(fAngle angle, out funit sin, out funit cos)
    {
        sin = Sin(angle);
        cos = Cos(angle);
    }
    #endregion

    #region Tan
    /// <summary>tan(angle) in Q16.16 via 64-bit sin/cos division. False at ±90° (result saturated).</summary>
    public static bool TryTan(fAngle angle, out ffloat result)
    {
        int s = SinRaw(angle.RawValue);
        int c = SinRaw(unchecked(angle.RawValue + RAW_QUARTER));
        if (c == 0)
        {
            result = s >= 0 ? ffloat.MaxValue : ffloat.MinValue;
            return false;
        }
        long q = fWideMath.DivideRoundToEven((long)s << ffloat.FractionalBits, c);
        if (q > int.MaxValue) { result = ffloat.MaxValue; return false; }
        if (q < int.MinValue) { result = ffloat.MinValue; return false; }
        result = ffloat.FromRaw((int)q);
        return true;
    }

    /// <summary>tan(angle); saturates (and is reported in validation builds) near ±90°.</summary>
    public static ffloat Tan(fAngle angle)
    {
        if (!TryTan(angle, out ffloat result))
            fMathValidation.Report(fMathValidationKind.Saturation, "fTrig.Tan near 90 degrees");
        return result;
    }
    #endregion

    #region Atan2
    /// <summary>atan(t) for t in [0, 1] given as Q1.30, in Angle32 raw units ([0, 2^29]).</summary>
    static uint AtanOctant(ulong ratioQ30)
    {
        uint[] table = fTrigAtanLut.Table;
        int index = (int)(ratioQ30 >> ATAN_INDEX_SHIFT);
        uint fraction = (uint)ratioQ30 & ATAN_FRACTION_MASK;
        uint a = table[index];
        if (fraction == 0)
            return a;
        uint b = table[index + 1];
        return a + (uint)fWideMath.RoundShiftRightToEven((long)(b - a) * fraction, ATAN_INDEX_SHIFT);
    }

    /// <summary>
    /// Angle of the vector (x, y) measured counter-clockwise from +x, for any common fixed-point scale
    /// of x and y (Q16 components, Q32 cross/dot products, Q60 ...). Atan2(0, 0) is Zero.
    /// </summary>
    public static fAngle Atan2Wide(long y, long x)
    {
        if (x == 0 && y == 0)
            return fAngle.Zero;

        ulong ax = fWideMath.AbsToULong(x);
        ulong ay = fWideMath.AbsToULong(y);

        // keep both below 2^32 so that (numerator << 30) fits in 64 bits
        int bits = fWideMath.BitLength(ax > ay ? ax : ay);
        if (bits > 32)
        {
            int shift = bits - 32;
            ax = fWideMath.RoundShiftRightToEven(ax, shift);
            ay = fWideMath.RoundShiftRightToEven(ay, shift);
        }

        bool swap = ay > ax;
        ulong numerator = swap ? ax : ay;
        ulong denominator = swap ? ay : ax;
        ulong ratio = fWideMath.DivideRoundToEven(numerator << funit.FractionalBits, denominator); // [0, 2^30]
        uint octant = AtanOctant(ratio);

        uint angle = swap ? RAW_QUARTER - octant : octant;  // [0°, 90°]
        if (x < 0)
            angle = 0x80000000u - angle;                    // [90°, 180°]
        if (y < 0)
            angle = unchecked(0u - angle);                  // mirror below the x axis
        return fAngle.FromRaw(angle);
    }

    public static fAngle Atan2(ffloat y, ffloat x) => Atan2Wide(y.RawValue, x.RawValue);

    public static fAngle Atan2(funit y, funit x) => Atan2Wide(y.RawValue, x.RawValue);

    /// <summary>atan(x) in [-90°, 90°].</summary>
    public static fAngleDelta Atan(ffloat x) => Atan2Wide(x.RawValue, ffloat.RawOne).ToDelta();
    #endregion

    #region Asin / Acos
    /// <summary>acos(x) in [0°, 180°] computed as atan2(sqrt(1 - x^2), x); x is clamped to [-1, 1].</summary>
    public static fAngle Acos(funit x)
    {
        x = funit.ClampUnit(x);
        return Atan2Wide(SqrtOneMinusSquare(x.RawValue), x.RawValue);
    }

    /// <summary>asin(x) in [-90°, 90°] computed as atan2(x, sqrt(1 - x^2)); x is clamped to [-1, 1].</summary>
    public static fAngleDelta Asin(funit x)
    {
        x = funit.ClampUnit(x);
        return Atan2Wide(x.RawValue, SqrtOneMinusSquare(x.RawValue)).ToDelta();
    }

    /// <summary>sqrt(1 - x^2) in Q1.30 for |x| &lt;= 1 (Q1.30).</summary>
    static long SqrtOneMinusSquare(int xRaw)
    {
        ulong one = 1UL << 60;
        ulong square = (ulong)((long)xRaw * xRaw);
        return square >= one ? 0L : (long)fWideMath.IntegerSqrtRounded(one - square);
    }
    #endregion
}
