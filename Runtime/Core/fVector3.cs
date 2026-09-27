using System;
using System.Runtime.InteropServices;

/// <summary>
/// 3D vector of Q16.16 components (12 bytes). Positions, offsets, velocities and accelerations.
///
/// Every product-based query (length², distance², dot, cross) is evaluated in 64 bits from the raw
/// components and only rounded when an ffloat result is requested; the *Wide variants return the
/// exact 64-bit value (Q32.32) so that range and angle checks never need a Sqrt.
/// Equality is raw-exact per component; use <see cref="Approximately"/> or
/// <see cref="IsWithinDistance"/> for tolerance checks.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct fVector3 : IEquatable<fVector3>
{
    public ffloat x;
    public ffloat y;
    public ffloat z;

    public fVector3(ffloat x, ffloat y, ffloat z)
    {
        this.x = x;
        this.y = y;
        this.z = z;
    }

    public static fVector3 FromRaw(int rawX, int rawY, int rawZ) => new fVector3(ffloat.FromRaw(rawX), ffloat.FromRaw(rawY), ffloat.FromRaw(rawZ));

    public static fVector3 FromInt(int x, int y, int z) => new fVector3(ffloat.FromInt(x), ffloat.FromInt(y), ffloat.FromInt(z));

    #region Constants
    public static fVector3 zero => default;
    public static fVector3 one => FromRaw(ffloat.RawOne, ffloat.RawOne, ffloat.RawOne);
    public static fVector3 up => FromRaw(0, ffloat.RawOne, 0);
    public static fVector3 down => FromRaw(0, -ffloat.RawOne, 0);
    public static fVector3 left => FromRaw(-ffloat.RawOne, 0, 0);
    public static fVector3 right => FromRaw(ffloat.RawOne, 0, 0);
    public static fVector3 forward => FromRaw(0, 0, ffloat.RawOne);
    public static fVector3 back => FromRaw(0, 0, -ffloat.RawOne);
    #endregion

    public void Set(ffloat newX, ffloat newY, ffloat newZ)
    {
        x = newX;
        y = newY;
        z = newZ;
    }

    #region Length / distance (wide)
    /// <summary>x² + y² + z² in Q32.32, exact for every raw input.</summary>
    public ulong LengthSquaredWide
    {
        get
        {
            long rx = x.RawValue, ry = y.RawValue, rz = z.RawValue;
            return (ulong)(rx * rx) + (ulong)(ry * ry) + (ulong)(rz * rz);
        }
    }

    /// <summary>Squared length rounded to Q16.16 (saturates beyond ~181 m; prefer LengthSquaredWide).</summary>
    public ffloat sqrMagnitude => ffloat.FromRaw(fWideMath.SaturateToInt(fWideMath.RoundShiftRightToEven(LengthSquaredWide, ffloat.FractionalBits)));

    /// <summary>Length rounded to nearest, computed from the exact wide squared length.</summary>
    public ffloat magnitude => ffloat.SqrtWide(LengthSquaredWide);

    /// <summary>|a - b|² in Q32.32. Differences are taken in 64 bits, so no intermediate overflow.</summary>
    public static ulong DistanceSquaredWide(fVector3 a, fVector3 b)
    {
        long dx = (long)a.x.RawValue - b.x.RawValue;
        long dy = (long)a.y.RawValue - b.y.RawValue;
        long dz = (long)a.z.RawValue - b.z.RawValue;
        ulong sum = (ulong)(dx * dx); // each square < 2^64
        sum = fWideMath.AddSaturating(sum, (ulong)(dy * dy));
        return fWideMath.AddSaturating(sum, (ulong)(dz * dz));
    }

    public static ffloat Distance(fVector3 a, fVector3 b) => ffloat.SqrtWide(DistanceSquaredWide(a, b));

    /// <summary>Sign of |a - b|² - distance²: -1 closer, 0 exactly at, +1 farther. No Sqrt.</summary>
    public static int CompareDistanceSquared(fVector3 a, fVector3 b, ffloat distance)
    {
        ulong d2 = DistanceSquaredWide(a, b);
        long r = distance.RawValue;
        ulong r2 = (ulong)(r * r);
        return d2 < r2 ? -1 : d2 > r2 ? 1 : 0;
    }

    /// <summary>|a - b| &lt;= radius, evaluated on exact squared values (no Sqrt).</summary>
    public static bool IsWithinDistance(fVector3 a, fVector3 b, ffloat radius) => CompareDistanceSquared(a, b, radius) <= 0;
    #endregion

    #region Dot / Cross (wide)
    /// <summary>a · b in Q32.32 (64-bit sum of 64-bit products; saturates only far outside the gameplay domain).</summary>
    public static long DotWide(fVector3 a, fVector3 b)
    {
        long sum = (long)a.x.RawValue * b.x.RawValue;
        sum = fWideMath.AddSaturating(sum, (long)a.y.RawValue * b.y.RawValue);
        return fWideMath.AddSaturating(sum, (long)a.z.RawValue * b.z.RawValue);
    }

    public static ffloat Dot(fVector3 a, fVector3 b)
    {
        return ffloat.FromRaw(fWideMath.SaturateToInt(fWideMath.RoundShiftRightToEven(DotWide(a, b), ffloat.FractionalBits)));
    }

    /// <summary>a × b with every component kept in Q32.32.</summary>
    public static fWideVector3 CrossWide(fVector3 a, fVector3 b)
    {
        long ax = a.x.RawValue, ay = a.y.RawValue, az = a.z.RawValue;
        long bx = b.x.RawValue, by = b.y.RawValue, bz = b.z.RawValue;
        return new fWideVector3(
            fWideMath.AddSaturating(ay * bz, -(az * by)),
            fWideMath.AddSaturating(az * bx, -(ax * bz)),
            fWideMath.AddSaturating(ax * by, -(ay * bx)));
    }

    public static fVector3 Cross(fVector3 a, fVector3 b) => CrossWide(a, b).ToVectorFromQ32();
    #endregion

    #region Normalize
    /// <summary>
    /// Unit direction of v in Q1.30. Works for every non-zero vector, including a 1-raw vector and
    /// the full ±32768 m range. Returns false (and a zero vector) only for the zero vector.
    /// </summary>
    public static bool TryNormalize(fVector3 v, out fUnitVector3 result)
    {
        bool ok = fWideMath.TryNormalizeQ30(v.x.RawValue, v.y.RawValue, v.z.RawValue, 0, out int ux, out int uy, out int uz, out _);
        result = fUnitVector3.FromRawUnchecked(ux, uy, uz);
        return ok;
    }

    public bool TryNormalize(out fUnitVector3 result) => TryNormalize(this, out result);

    /// <summary>Unit direction, or <paramref name="fallback"/> for the zero vector.</summary>
    public fUnitVector3 NormalizedOr(fUnitVector3 fallback) => TryNormalize(this, out fUnitVector3 u) ? u : fallback;

    /// <summary>v scaled so that |v| &lt;= maxLength.</summary>
    public static fVector3 ClampMagnitude(fVector3 v, ffloat maxLength)
    {
        if (maxLength.RawValue <= 0)
            return zero;
        long m = maxLength.RawValue;
        if (v.LengthSquaredWide <= (ulong)(m * m))
            return v;
        TryNormalize(v, out fUnitVector3 u);
        return u * maxLength;
    }
    #endregion

    #region Angle
    /// <summary>
    /// Unsigned angle between a and b in [0°, 180°] computed as atan2(|a × b|, a · b), which stays
    /// accurate for tiny angles (no acos). Zero vectors give Zero.
    /// </summary>
    public static fAngle AngleUsingAtan2(fVector3 a, fVector3 b)
    {
        fWideVector3 c = CrossWide(a, b);
        ulong crossLength = fWideMath.LengthWide(c.x, c.y, c.z, 0, out int shift);
        long dot = fWideMath.RoundShiftRightToEven(DotWide(a, b), shift);
        return fTrig.Atan2Wide((long)crossLength, dot);
    }

    /// <summary>Same as <see cref="AngleUsingAtan2"/>.</summary>
    public static fAngle Angle(fVector3 a, fVector3 b) => AngleUsingAtan2(a, b);

    /// <summary>Signed angle from a to b around axis in [-180°, 180°).</summary>
    public static fAngleDelta SignedAngle(fVector3 from, fVector3 to, fUnitVector3 axis)
    {
        fAngle angle = AngleUsingAtan2(from, to);
        fWideVector3 c = CrossWide(from, to);
        // reduce the cross product so that its dot with a Q1.30 axis fits in 64 bits
        ulong m = fWideMath.AbsToULong(c.x);
        ulong t = fWideMath.AbsToULong(c.y); if (t > m) m = t;
        t = fWideMath.AbsToULong(c.z); if (t > m) m = t;
        int shift = Math.Max(0, fWideMath.BitLength(m) - 31);
        long sx = fWideMath.RoundShiftRightToEven(c.x, shift);
        long sy = fWideMath.RoundShiftRightToEven(c.y, shift);
        long sz = fWideMath.RoundShiftRightToEven(c.z, shift);
        long side = sx * axis.x.RawValue + sy * axis.y.RawValue + sz * axis.z.RawValue;
        fAngleDelta delta = angle.ToDelta();
        return side < 0 ? -delta : delta;
    }

    /// <summary>True when the angle between a and b is at most limit (dot threshold, no acos).</summary>
    public static bool WithinAngle(fVector3 a, fVector3 b, fAngle limit)
    {
        if (!TryNormalize(a, out fUnitVector3 ua) || !TryNormalize(b, out fUnitVector3 ub))
            return false;
        return fUnitVector3.WithinAngle(ua, ub, limit);
    }
    #endregion

    #region Interpolation / projection
    public static fVector3 Lerp(fVector3 a, fVector3 b, ffloat t) => LerpUnclamped(a, b, ffloat.Clamp01(t));

    public static fVector3 LerpUnclamped(fVector3 a, fVector3 b, ffloat t)
    {
        return new fVector3(ffloat.LerpUnclamped(a.x, b.x, t), ffloat.LerpUnclamped(a.y, b.y, t), ffloat.LerpUnclamped(a.z, b.z, t));
    }

    /// <summary>Moves current towards target by at most maxDistance along the straight line.</summary>
    public static fVector3 MoveTowards(fVector3 current, fVector3 target, ffloat maxDistance)
    {
        long dx = (long)target.x.RawValue - current.x.RawValue;
        long dy = (long)target.y.RawValue - current.y.RawValue;
        long dz = (long)target.z.RawValue - current.z.RawValue;
        long m = Math.Max(0, maxDistance.RawValue);
        if (DistanceSquaredWide(current, target) <= (ulong)(m * m))
            return target;
        fWideMath.TryNormalizeQ30(dx, dy, dz, 0, out int ux, out int uy, out int uz, out _);
        return current + fUnitVector3.FromRawUnchecked(ux, uy, uz) * maxDistance;
    }

    /// <summary>(v · n) n for a unit normal n.</summary>
    public static fVector3 Project(fVector3 v, fUnitVector3 onNormal)
    {
        long d = DotWithUnitQ16(v, onNormal);
        return FromRaw(
            fWideMath.SaturateToInt(fWideMath.RoundShiftRightToEven(d * onNormal.x.RawValue, funit.FractionalBits)),
            fWideMath.SaturateToInt(fWideMath.RoundShiftRightToEven(d * onNormal.y.RawValue, funit.FractionalBits)),
            fWideMath.SaturateToInt(fWideMath.RoundShiftRightToEven(d * onNormal.z.RawValue, funit.FractionalBits)));
    }

    /// <summary>v - (v · n) n for a unit plane normal n.</summary>
    public static fVector3 ProjectOnPlane(fVector3 v, fUnitVector3 planeNormal)
    {
        long d = DotWithUnitQ16(v, planeNormal);
        return FromRaw(
            fWideMath.SaturateToInt(v.x.RawValue - fWideMath.RoundShiftRightToEven(d * planeNormal.x.RawValue, funit.FractionalBits)),
            fWideMath.SaturateToInt(v.y.RawValue - fWideMath.RoundShiftRightToEven(d * planeNormal.y.RawValue, funit.FractionalBits)),
            fWideMath.SaturateToInt(v.z.RawValue - fWideMath.RoundShiftRightToEven(d * planeNormal.z.RawValue, funit.FractionalBits)));
    }

    /// <summary>direction - 2 (direction · n) n for a unit normal n.</summary>
    public static fVector3 Reflect(fVector3 direction, fUnitVector3 normal)
    {
        long d = DotWithUnitQ16(direction, normal);
        return FromRaw(
            fWideMath.SaturateToInt(direction.x.RawValue - 2 * fWideMath.RoundShiftRightToEven(d * normal.x.RawValue, funit.FractionalBits)),
            fWideMath.SaturateToInt(direction.y.RawValue - 2 * fWideMath.RoundShiftRightToEven(d * normal.y.RawValue, funit.FractionalBits)),
            fWideMath.SaturateToInt(direction.z.RawValue - 2 * fWideMath.RoundShiftRightToEven(d * normal.z.RawValue, funit.FractionalBits)));
    }

    /// <summary>v · n for a unit direction n, rounded to Q16.16 (e.g. signed distance along a plane normal).</summary>
    public static ffloat Dot(fVector3 v, fUnitVector3 n) => ffloat.FromRaw(fWideMath.SaturateToInt(DotWithUnitQ16(v, n)));

    /// <summary>v · n (Q16.16 vector, Q1.30 unit) as a wide Q16 value.</summary>
    internal static long DotWithUnitQ16(fVector3 v, fUnitVector3 n)
    {
        long sum = (long)v.x.RawValue * n.x.RawValue + (long)v.y.RawValue * n.y.RawValue + (long)v.z.RawValue * n.z.RawValue;
        return fWideMath.RoundShiftRightToEven(sum, funit.FractionalBits);
    }

    public static fVector3 Scale(fVector3 a, fVector3 b) => new fVector3(a.x * b.x, a.y * b.y, a.z * b.z);
    public static fVector3 Min(fVector3 a, fVector3 b) => new fVector3(ffloat.Min(a.x, b.x), ffloat.Min(a.y, b.y), ffloat.Min(a.z, b.z));
    public static fVector3 Max(fVector3 a, fVector3 b) => new fVector3(ffloat.Max(a.x, b.x), ffloat.Max(a.y, b.y), ffloat.Max(a.z, b.z));
    #endregion

    #region Operators
    public static fVector3 operator +(fVector3 a, fVector3 b) => new fVector3(a.x + b.x, a.y + b.y, a.z + b.z);
    public static fVector3 operator -(fVector3 a, fVector3 b) => new fVector3(a.x - b.x, a.y - b.y, a.z - b.z);
    public static fVector3 operator -(fVector3 v) => new fVector3(-v.x, -v.y, -v.z);
    public static fVector3 operator *(fVector3 v, ffloat s) => new fVector3(v.x * s, v.y * s, v.z * s);
    public static fVector3 operator *(ffloat s, fVector3 v) => new fVector3(v.x * s, v.y * s, v.z * s);
    public static fVector3 operator *(fVector3 v, funit s) => new fVector3(v.x * s, v.y * s, v.z * s);
    public static fVector3 operator *(fVector3 v, int s) => new fVector3(v.x * s, v.y * s, v.z * s);
    public static fVector3 operator *(int s, fVector3 v) => new fVector3(v.x * s, v.y * s, v.z * s);
    public static fVector3 operator /(fVector3 v, ffloat s) => new fVector3(v.x / s, v.y / s, v.z / s);
    public static fVector3 operator /(fVector3 v, int s) => new fVector3(v.x / s, v.y / s, v.z / s);

    /// <summary>Raw-exact component equality.</summary>
    public static bool operator ==(fVector3 a, fVector3 b) => a.x == b.x && a.y == b.y && a.z == b.z;
    public static bool operator !=(fVector3 a, fVector3 b) => !(a == b);

    public bool Equals(fVector3 other) => this == other;
    public override bool Equals(object obj) => obj is fVector3 other && this == other;
    public override int GetHashCode()
    {
        unchecked
        {
            int h = x.RawValue;
            h = h * 397 ^ y.RawValue;
            h = h * 397 ^ z.RawValue;
            return h;
        }
    }

    /// <summary>Every component differs by at most tolerance.</summary>
    public static bool Approximately(fVector3 a, fVector3 b, ffloat tolerance)
    {
        return ffloat.Approximately(a.x, b.x, tolerance) && ffloat.Approximately(a.y, b.y, tolerance) && ffloat.Approximately(a.z, b.z, tolerance);
    }

    public static explicit operator fVector2(fVector3 v) => new fVector2(v.x, v.y);
    #endregion

    public override string ToString() => "(" + x + ", " + y + ", " + z + ")";
}
