using System;
using System.Runtime.InteropServices;

/// <summary>
/// 2D vector of Q16.16 components (8 bytes). Same rules as <see cref="fVector3"/>: 64-bit
/// intermediates, *Wide exact queries and raw-exact equality.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct fVector2 : IEquatable<fVector2>
{
    public ffloat x;
    public ffloat y;

    public fVector2(ffloat x, ffloat y)
    {
        this.x = x;
        this.y = y;
    }

    public static fVector2 FromRaw(int rawX, int rawY) => new fVector2(ffloat.FromRaw(rawX), ffloat.FromRaw(rawY));

    public static fVector2 FromInt(int x, int y) => new fVector2(ffloat.FromInt(x), ffloat.FromInt(y));

    #region Constants
    public static fVector2 zero => default;
    public static fVector2 one => FromRaw(ffloat.RawOne, ffloat.RawOne);
    public static fVector2 up => FromRaw(0, ffloat.RawOne);
    public static fVector2 down => FromRaw(0, -ffloat.RawOne);
    public static fVector2 left => FromRaw(-ffloat.RawOne, 0);
    public static fVector2 right => FromRaw(ffloat.RawOne, 0);
    #endregion

    public void Set(ffloat newX, ffloat newY)
    {
        x = newX;
        y = newY;
    }

    #region Length / distance (wide)
    /// <summary>x² + y² in Q32.32, exact for every raw input.</summary>
    public ulong LengthSquaredWide
    {
        get
        {
            long rx = x.RawValue, ry = y.RawValue;
            return (ulong)(rx * rx) + (ulong)(ry * ry);
        }
    }

    public ffloat sqrMagnitude => ffloat.FromRaw(fWideMath.SaturateToInt(fWideMath.RoundShiftRightToEven(LengthSquaredWide, ffloat.FractionalBits)));

    public ffloat magnitude => ffloat.SqrtWide(LengthSquaredWide);

    public static ulong DistanceSquaredWide(fVector2 a, fVector2 b)
    {
        long dx = (long)a.x.RawValue - b.x.RawValue;
        long dy = (long)a.y.RawValue - b.y.RawValue;
        return fWideMath.AddSaturating((ulong)(dx * dx), (ulong)(dy * dy));
    }

    public static ffloat Distance(fVector2 a, fVector2 b) => ffloat.SqrtWide(DistanceSquaredWide(a, b));

    public static int CompareDistanceSquared(fVector2 a, fVector2 b, ffloat distance)
    {
        ulong d2 = DistanceSquaredWide(a, b);
        long r = distance.RawValue;
        ulong r2 = (ulong)(r * r);
        return d2 < r2 ? -1 : d2 > r2 ? 1 : 0;
    }

    public static bool IsWithinDistance(fVector2 a, fVector2 b, ffloat radius) => CompareDistanceSquared(a, b, radius) <= 0;
    #endregion

    #region Dot / Cross (wide)
    public static long DotWide(fVector2 a, fVector2 b)
    {
        return fWideMath.AddSaturating((long)a.x.RawValue * b.x.RawValue, (long)a.y.RawValue * b.y.RawValue);
    }

    public static ffloat Dot(fVector2 a, fVector2 b)
    {
        return ffloat.FromRaw(fWideMath.SaturateToInt(fWideMath.RoundShiftRightToEven(DotWide(a, b), ffloat.FractionalBits)));
    }

    /// <summary>z component of the 3D cross product (a.x b.y - a.y b.x) in Q32.32.</summary>
    public static long CrossWide(fVector2 a, fVector2 b)
    {
        return fWideMath.AddSaturating((long)a.x.RawValue * b.y.RawValue, -((long)a.y.RawValue * b.x.RawValue));
    }

    public static ffloat Cross(fVector2 a, fVector2 b)
    {
        return ffloat.FromRaw(fWideMath.SaturateToInt(fWideMath.RoundShiftRightToEven(CrossWide(a, b), ffloat.FractionalBits)));
    }
    #endregion

    #region Normalize / angle
    public static bool TryNormalize(fVector2 v, out fUnitVector2 result)
    {
        bool ok = fWideMath.TryNormalizeQ30(v.x.RawValue, v.y.RawValue, 0, 0, out int ux, out int uy, out _, out _);
        result = fUnitVector2.FromRawUnchecked(ux, uy);
        return ok;
    }

    public bool TryNormalize(out fUnitVector2 result) => TryNormalize(this, out result);

    public fUnitVector2 NormalizedOr(fUnitVector2 fallback) => TryNormalize(this, out fUnitVector2 u) ? u : fallback;

    public static fVector2 ClampMagnitude(fVector2 v, ffloat maxLength)
    {
        if (maxLength.RawValue <= 0)
            return zero;
        long m = maxLength.RawValue;
        if (v.LengthSquaredWide <= (ulong)(m * m))
            return v;
        TryNormalize(v, out fUnitVector2 u);
        return u * maxLength;
    }

    /// <summary>Direction angle of v measured counter-clockwise from +x.</summary>
    public fAngle ToAngle() => fTrig.Atan2Wide(y.RawValue, x.RawValue);

    /// <summary>Unsigned angle in [0°, 180°] via atan2(|cross|, dot).</summary>
    public static fAngle AngleUsingAtan2(fVector2 a, fVector2 b)
    {
        long cross = CrossWide(a, b);
        return fTrig.Atan2Wide(cross < 0 ? -cross : cross, DotWide(a, b));
    }

    public static fAngle Angle(fVector2 a, fVector2 b) => AngleUsingAtan2(a, b);

    /// <summary>Signed planar angle from a to b in [-180°, 180°) (counter-clockwise positive).</summary>
    public static fAngleDelta SignedAngle(fVector2 from, fVector2 to)
    {
        return fTrig.Atan2Wide(CrossWide(from, to), DotWide(from, to)).ToDelta();
    }

    public static bool WithinAngle(fVector2 a, fVector2 b, fAngle limit)
    {
        if (!TryNormalize(a, out fUnitVector2 ua) || !TryNormalize(b, out fUnitVector2 ub))
            return false;
        return fUnitVector2.WithinAngle(ua, ub, limit);
    }

    /// <summary>Rotates v counter-clockwise by angle (Q1.30 sin/cos, 64-bit products, one rounding).</summary>
    public static fVector2 Rotate(fVector2 v, fAngle angle)
    {
        long s = fTrig.Sin(angle).RawValue, c = fTrig.Cos(angle).RawValue;
        long vx = v.x.RawValue, vy = v.y.RawValue;
        return FromRaw(
            fWideMath.SaturateToInt(fWideMath.RoundShiftRightToEven(c * vx - s * vy, funit.FractionalBits)),
            fWideMath.SaturateToInt(fWideMath.RoundShiftRightToEven(s * vx + c * vy, funit.FractionalBits)));
    }

    public void Rotate(fAngle angle) => this = Rotate(this, angle);
    #endregion

    #region Interpolation / projection
    public static fVector2 Lerp(fVector2 a, fVector2 b, ffloat t) => LerpUnclamped(a, b, ffloat.Clamp01(t));

    public static fVector2 LerpUnclamped(fVector2 a, fVector2 b, ffloat t)
    {
        return new fVector2(ffloat.LerpUnclamped(a.x, b.x, t), ffloat.LerpUnclamped(a.y, b.y, t));
    }

    public static fVector2 MoveTowards(fVector2 current, fVector2 target, ffloat maxDistance)
    {
        long dx = (long)target.x.RawValue - current.x.RawValue;
        long dy = (long)target.y.RawValue - current.y.RawValue;
        long m = Math.Max(0, maxDistance.RawValue);
        if (DistanceSquaredWide(current, target) <= (ulong)(m * m))
            return target;
        fWideMath.TryNormalizeQ30(dx, dy, 0, 0, out int ux, out int uy, out _, out _);
        return current + fUnitVector2.FromRawUnchecked(ux, uy) * maxDistance;
    }

    public static fVector2 Reflect(fVector2 direction, fUnitVector2 normal)
    {
        long d = fWideMath.RoundShiftRightToEven((long)direction.x.RawValue * normal.x.RawValue + (long)direction.y.RawValue * normal.y.RawValue, funit.FractionalBits);
        return FromRaw(
            fWideMath.SaturateToInt(direction.x.RawValue - 2 * fWideMath.RoundShiftRightToEven(d * normal.x.RawValue, funit.FractionalBits)),
            fWideMath.SaturateToInt(direction.y.RawValue - 2 * fWideMath.RoundShiftRightToEven(d * normal.y.RawValue, funit.FractionalBits)));
    }

    /// <summary>Counter-clockwise perpendicular (-y, x).</summary>
    public static fVector2 Perpendicular(fVector2 v) => new fVector2(-v.y, v.x);

    public static fVector2 Scale(fVector2 a, fVector2 b) => new fVector2(a.x * b.x, a.y * b.y);
    public static fVector2 Min(fVector2 a, fVector2 b) => new fVector2(ffloat.Min(a.x, b.x), ffloat.Min(a.y, b.y));
    public static fVector2 Max(fVector2 a, fVector2 b) => new fVector2(ffloat.Max(a.x, b.x), ffloat.Max(a.y, b.y));
    #endregion

    #region Operators
    public static fVector2 operator +(fVector2 a, fVector2 b) => new fVector2(a.x + b.x, a.y + b.y);
    public static fVector2 operator -(fVector2 a, fVector2 b) => new fVector2(a.x - b.x, a.y - b.y);
    public static fVector2 operator -(fVector2 v) => new fVector2(-v.x, -v.y);
    public static fVector2 operator *(fVector2 v, ffloat s) => new fVector2(v.x * s, v.y * s);
    public static fVector2 operator *(ffloat s, fVector2 v) => new fVector2(v.x * s, v.y * s);
    public static fVector2 operator *(fVector2 v, funit s) => new fVector2(v.x * s, v.y * s);
    public static fVector2 operator *(fVector2 v, int s) => new fVector2(v.x * s, v.y * s);
    public static fVector2 operator *(int s, fVector2 v) => new fVector2(v.x * s, v.y * s);
    public static fVector2 operator /(fVector2 v, ffloat s) => new fVector2(v.x / s, v.y / s);
    public static fVector2 operator /(fVector2 v, int s) => new fVector2(v.x / s, v.y / s);

    /// <summary>Raw-exact component equality.</summary>
    public static bool operator ==(fVector2 a, fVector2 b) => a.x == b.x && a.y == b.y;
    public static bool operator !=(fVector2 a, fVector2 b) => !(a == b);
    public bool Equals(fVector2 other) => this == other;
    public override bool Equals(object obj) => obj is fVector2 other && this == other;
    public override int GetHashCode() => unchecked(x.RawValue * 397 ^ y.RawValue);

    public static bool Approximately(fVector2 a, fVector2 b, ffloat tolerance)
    {
        return ffloat.Approximately(a.x, b.x, tolerance) && ffloat.Approximately(a.y, b.y, tolerance);
    }

    public static implicit operator fVector3(fVector2 v) => new fVector3(v.x, v.y, ffloat.Zero);
    #endregion

    public override string ToString() => "(" + x + ", " + y + ")";
}
