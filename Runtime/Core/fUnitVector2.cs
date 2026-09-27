using System;
using System.Runtime.InteropServices;

/// <summary>
/// Unit-length 2D direction with Q1.30 components (8 bytes), e.g. planar facing.
/// <see cref="FromAngle"/> / <see cref="ToAngle"/> convert to and from <see cref="fAngle"/>.
/// The default value (0, 0) is not a valid direction.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct fUnitVector2 : IEquatable<fUnitVector2>
{
    const int ONE = funit.RawOne;
    const long VALID_TOLERANCE_Q60 = 1L << 40;

    public readonly funit x;
    public readonly funit y;

    fUnitVector2(int rawX, int rawY)
    {
        x = funit.FromRaw(rawX);
        y = funit.FromRaw(rawY);
    }

    public static readonly fUnitVector2 right = new fUnitVector2(ONE, 0);
    public static readonly fUnitVector2 left = new fUnitVector2(-ONE, 0);
    public static readonly fUnitVector2 up = new fUnitVector2(0, ONE);
    public static readonly fUnitVector2 down = new fUnitVector2(0, -ONE);

    #region Factories
    public static bool TryFromVector(fVector2 v, out fUnitVector2 result) => fVector2.TryNormalize(v, out result);

    public static bool TryFromRaw(int rawX, int rawY, out fUnitVector2 result)
    {
        bool ok = fWideMath.TryNormalizeQ30(rawX, rawY, 0, 0, out int ux, out int uy, out _, out _);
        result = new fUnitVector2(ux, uy);
        return ok;
    }

    public static fUnitVector2 FromRawUnchecked(int rawX, int rawY)
    {
        var u = new fUnitVector2(rawX, rawY);
#if FMATH_VALIDATE
        if (!u.IsValid && !(rawX == 0 && rawY == 0))
            fMathValidation.Report(fMathValidationKind.UnitOutOfRange, "fUnitVector2 is not unit length");
#endif
        return u;
    }

    /// <summary>(cos a, sin a).</summary>
    public static fUnitVector2 FromAngle(fAngle angle) => new fUnitVector2(fTrig.Cos(angle).RawValue, fTrig.Sin(angle).RawValue);
    #endregion

    public long LengthSquaredWide
    {
        get
        {
            long rx = x.RawValue, ry = y.RawValue;
            return rx * rx + ry * ry;
        }
    }

    public bool IsValid
    {
        get
        {
            long diff = LengthSquaredWide - (1L << 60);
            return diff <= VALID_TOLERANCE_Q60 && diff >= -VALID_TOLERANCE_Q60;
        }
    }

    public fAngle ToAngle() => fTrig.Atan2Wide(y.RawValue, x.RawValue);

    public static long DotWide(fUnitVector2 a, fUnitVector2 b) => (long)a.x.RawValue * b.x.RawValue + (long)a.y.RawValue * b.y.RawValue;

    public static funit Dot(fUnitVector2 a, fUnitVector2 b) => funit.FromRaw(fWideMath.SaturateToInt(fWideMath.RoundShiftRightToEven(DotWide(a, b), funit.FractionalBits)));

    /// <summary>a.x b.y - a.y b.x in Q2.60 (= sin of the signed angle).</summary>
    public static long CrossWide(fUnitVector2 a, fUnitVector2 b) => (long)a.x.RawValue * b.y.RawValue - (long)a.y.RawValue * b.x.RawValue;

    public static bool WithinAngle(fUnitVector2 a, fUnitVector2 b, fAngle limit)
    {
        if (limit.RawValue >= 0x80000000u)
            return true;
        return DotWide(a, b) >= (long)fTrig.Cos(limit).RawValue << funit.FractionalBits;
    }

    public static fAngleDelta SignedAngle(fUnitVector2 from, fUnitVector2 to) => fTrig.Atan2Wide(CrossWide(from, to), DotWide(from, to)).ToDelta();

    /// <summary>Rotates counter-clockwise by angle; the result is renormalized.</summary>
    public static fUnitVector2 Rotate(fUnitVector2 u, fAngle angle)
    {
        long s = fTrig.Sin(angle).RawValue, c = fTrig.Cos(angle).RawValue;
        long ux = u.x.RawValue, uy = u.y.RawValue;
        fWideMath.TryNormalizeQ30(c * ux - s * uy, s * ux + c * uy, 0, 0, out int rx, out int ry, out _, out _);
        return new fUnitVector2(rx, ry);
    }

    public fVector2 ToVector() => this * ffloat.One;

    public static fVector2 operator *(fUnitVector2 u, ffloat length)
    {
        return fVector2.FromRaw(fWideMath.MulQ16ByQ30(length.RawValue, u.x.RawValue), fWideMath.MulQ16ByQ30(length.RawValue, u.y.RawValue));
    }

    public static fVector2 operator *(ffloat length, fUnitVector2 u) => u * length;

    public static fUnitVector2 operator -(fUnitVector2 u) => new fUnitVector2(-u.x.RawValue, -u.y.RawValue);

    public static bool operator ==(fUnitVector2 a, fUnitVector2 b) => a.x == b.x && a.y == b.y;
    public static bool operator !=(fUnitVector2 a, fUnitVector2 b) => !(a == b);
    public bool Equals(fUnitVector2 other) => this == other;
    public override bool Equals(object obj) => obj is fUnitVector2 other && this == other;
    public override int GetHashCode() => unchecked(x.RawValue * 397 ^ y.RawValue);

    public static bool Approximately(fUnitVector2 a, fUnitVector2 b, int toleranceRaw)
    {
        return funit.Approximately(a.x, b.x, toleranceRaw) && funit.Approximately(a.y, b.y, toleranceRaw);
    }

    public override string ToString() => "(" + x + ", " + y + ")";
}
