using System;
using System.Runtime.InteropServices;

/// <summary>
/// Unit-length 3D direction with Q1.30 components (12 bytes): facing, surface normals, axes.
/// Obtain one through <see cref="TryFromVector"/> / fVector3.TryNormalize (which never lose a tiny
/// but non-zero vector) or from the axis constants. The default value (0, 0, 0) is not a valid
/// direction; <see cref="IsValid"/> reports that case.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct fUnitVector3 : IEquatable<fUnitVector3>
{
    const int ONE = funit.RawOne;
    // |u|² may deviate from 1 by a few raw units after rounding; allow 2^-20 relative.
    const long VALID_TOLERANCE_Q60 = 1L << 40;

    public readonly funit x;
    public readonly funit y;
    public readonly funit z;

    fUnitVector3(int rawX, int rawY, int rawZ)
    {
        x = funit.FromRaw(rawX);
        y = funit.FromRaw(rawY);
        z = funit.FromRaw(rawZ);
    }

    #region Constants
    public static readonly fUnitVector3 right = new fUnitVector3(ONE, 0, 0);
    public static readonly fUnitVector3 left = new fUnitVector3(-ONE, 0, 0);
    public static readonly fUnitVector3 up = new fUnitVector3(0, ONE, 0);
    public static readonly fUnitVector3 down = new fUnitVector3(0, -ONE, 0);
    public static readonly fUnitVector3 forward = new fUnitVector3(0, 0, ONE);
    public static readonly fUnitVector3 back = new fUnitVector3(0, 0, -ONE);
    #endregion

    #region Factories
    public static bool TryFromVector(fVector3 v, out fUnitVector3 result) => fVector3.TryNormalize(v, out result);

    /// <summary>Normalizes arbitrary Q1.30 raw components (e.g. from a snapshot or authoring data).</summary>
    public static bool TryFromRaw(int rawX, int rawY, int rawZ, out fUnitVector3 result)
    {
        bool ok = fWideMath.TryNormalizeQ30(rawX, rawY, rawZ, 0, out int ux, out int uy, out int uz, out _);
        result = new fUnitVector3(ux, uy, uz);
        return ok;
    }

    /// <summary>
    /// Wraps raw Q1.30 components that are already unit length (deserialization, internal results).
    /// Validation builds report components that are not unit length.
    /// </summary>
    public static fUnitVector3 FromRawUnchecked(int rawX, int rawY, int rawZ)
    {
        var u = new fUnitVector3(rawX, rawY, rawZ);
#if FMATH_VALIDATE
        if (!u.IsValid && !(rawX == 0 && rawY == 0 && rawZ == 0))
            fMathValidation.Report(fMathValidationKind.UnitOutOfRange, "fUnitVector3 is not unit length");
#endif
        return u;
    }
    #endregion

    /// <summary>|u|² in Q2.60.</summary>
    public long LengthSquaredWide
    {
        get
        {
            long rx = x.RawValue, ry = y.RawValue, rz = z.RawValue;
            return rx * rx + ry * ry + rz * rz;
        }
    }

    /// <summary>True when |u| is 1 within rounding tolerance (false for the default zero value).</summary>
    public bool IsValid
    {
        get
        {
            long diff = LengthSquaredWide - (1L << 60);
            return diff <= VALID_TOLERANCE_Q60 && diff >= -VALID_TOLERANCE_Q60;
        }
    }

    #region Dot / Cross
    /// <summary>a · b in Q2.60 (exact).</summary>
    public static long DotWide(fUnitVector3 a, fUnitVector3 b)
    {
        return (long)a.x.RawValue * b.x.RawValue + (long)a.y.RawValue * b.y.RawValue + (long)a.z.RawValue * b.z.RawValue;
    }

    /// <summary>a · b rounded to Q1.30.</summary>
    public static funit Dot(fUnitVector3 a, fUnitVector3 b)
    {
        return funit.FromRaw(fWideMath.SaturateToInt(fWideMath.RoundShiftRightToEven(DotWide(a, b), funit.FractionalBits)));
    }

    /// <summary>a × b in Q2.60 (exact). |a × b| = sin(angle).</summary>
    public static fWideVector3 CrossWide(fUnitVector3 a, fUnitVector3 b)
    {
        long ax = a.x.RawValue, ay = a.y.RawValue, az = a.z.RawValue;
        long bx = b.x.RawValue, by = b.y.RawValue, bz = b.z.RawValue;
        return new fWideVector3(ay * bz - az * by, az * bx - ax * bz, ax * by - ay * bx);
    }

    /// <summary>Unit direction of a × b; false when a and b are parallel.</summary>
    public static bool TryNormalizedCross(fUnitVector3 a, fUnitVector3 b, out fUnitVector3 result) => CrossWide(a, b).TryNormalize(out result);
    #endregion

    #region Angles
    /// <summary>angle(a, b) &lt;= limit using dot &gt;= cos(limit); no acos. limit is taken in [0°, 180°].</summary>
    public static bool WithinAngle(fUnitVector3 a, fUnitVector3 b, fAngle limit)
    {
        if (limit.RawValue >= 0x80000000u)
            return true;
        long cosLimit = (long)fTrig.Cos(limit).RawValue << funit.FractionalBits; // Q2.60
        return DotWide(a, b) >= cosLimit;
    }

    /// <summary>Unsigned angle in [0°, 180°] as atan2(|a × b|, a · b); accurate for tiny angles.</summary>
    public static fAngle AngleUsingAtan2(fUnitVector3 a, fUnitVector3 b)
    {
        fWideVector3 c = CrossWide(a, b);
        ulong crossLength = fWideMath.LengthWide(c.x, c.y, c.z, 0, out int shift);
        long dot = fWideMath.RoundShiftRightToEven(DotWide(a, b), shift);
        return fTrig.Atan2Wide((long)crossLength, dot);
    }
    #endregion

    #region Conversions / operators
    /// <summary>Unit vector as a Q16.16 vector of length 1.</summary>
    public fVector3 ToVector() => this * ffloat.One;

    /// <summary>Direction scaled by a length: exact 64-bit products rounded once to Q16.16.</summary>
    public static fVector3 operator *(fUnitVector3 u, ffloat length)
    {
        return fVector3.FromRaw(
            fWideMath.MulQ16ByQ30(length.RawValue, u.x.RawValue),
            fWideMath.MulQ16ByQ30(length.RawValue, u.y.RawValue),
            fWideMath.MulQ16ByQ30(length.RawValue, u.z.RawValue));
    }

    public static fVector3 operator *(ffloat length, fUnitVector3 u) => u * length;

    public static fUnitVector3 operator -(fUnitVector3 u) => new fUnitVector3(-u.x.RawValue, -u.y.RawValue, -u.z.RawValue);

    /// <summary>Raw-exact component equality.</summary>
    public static bool operator ==(fUnitVector3 a, fUnitVector3 b) => a.x == b.x && a.y == b.y && a.z == b.z;
    public static bool operator !=(fUnitVector3 a, fUnitVector3 b) => !(a == b);
    public bool Equals(fUnitVector3 other) => this == other;
    public override bool Equals(object obj) => obj is fUnitVector3 other && this == other;
    public override int GetHashCode() => unchecked((x.RawValue * 397 ^ y.RawValue) * 397 ^ z.RawValue);

    public static bool Approximately(fUnitVector3 a, fUnitVector3 b, int toleranceRaw)
    {
        return funit.Approximately(a.x, b.x, toleranceRaw) && funit.Approximately(a.y, b.y, toleranceRaw) && funit.Approximately(a.z, b.z, toleranceRaw);
    }
    #endregion

    public override string ToString() => "(" + x + ", " + y + ", " + z + ")";
}
