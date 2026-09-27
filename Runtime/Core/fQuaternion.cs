using System;
using System.Runtime.InteropServices;

/// <summary>
/// Rotation quaternion with Q1.30 components (16 bytes).
///
/// Products and sums of products are evaluated in 64 bits (Q2.60) and rounded once. Every API that
/// builds a rotation (AngleAxis, FromToRotation, LookRotation, Lerp, Slerp, Euler) returns a
/// normalized quaternion. Vectors are rotated through a Q1.30 rotation matrix, so rotation does not
/// change vector length beyond rounding (a fraction of a raw unit at 2048 m).
///
/// operator == is raw-exact. q and -q describe the same rotation: use <see cref="IsSameRotation"/>
/// or <see cref="Approximately"/> for rotation comparisons. The default value (0, 0, 0, 0) is not a
/// rotation; use <see cref="identity"/>.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct fQuaternion : IEquatable<fQuaternion>
{
    const int ONE = funit.RawOne;
    const int Q30 = funit.FractionalBits;
    const long ONE_Q60 = 1L << 60;
    const long VALID_TOLERANCE_Q60 = 1L << 40;
    // |a x b| (Q2.60) below this is treated as parallel (sin(angle) < 2^-20).
    const long PARALLEL_THRESHOLD_Q60 = 1L << 40;
    // Slerp falls back to normalized lerp when the half angle is below ~0.3° (1 - cos < 2^-16).
    const long NLERP_THRESHOLD_Q60 = ONE_Q60 - (1L << 44);

    public readonly funit x;
    public readonly funit y;
    public readonly funit z;
    public readonly funit w;

    fQuaternion(int rawX, int rawY, int rawZ, int rawW)
    {
        x = funit.FromRaw(rawX);
        y = funit.FromRaw(rawY);
        z = funit.FromRaw(rawZ);
        w = funit.FromRaw(rawW);
    }

    public static readonly fQuaternion identity = new fQuaternion(0, 0, 0, ONE);

    #region Factories / normalization
    /// <summary>
    /// Wraps raw Q1.30 components that are already normalized (deserialization, snapshots).
    /// Validation builds report non-unit input.
    /// </summary>
    public static fQuaternion FromRawUnchecked(int rawX, int rawY, int rawZ, int rawW)
    {
        var q = new fQuaternion(rawX, rawY, rawZ, rawW);
#if FMATH_VALIDATE
        if (!q.IsNormalized)
            fMathValidation.Report(fMathValidationKind.InvalidQuaternion, "fQuaternion.FromRawUnchecked is not unit length");
#endif
        return q;
    }

    /// <summary>Normalizes arbitrary raw components. False for the zero quaternion.</summary>
    public static bool TryFromRaw(int rawX, int rawY, int rawZ, int rawW, out fQuaternion result)
    {
        return TryNormalizeWide(rawX, rawY, rawZ, rawW, out result);
    }

    /// <summary>|q|² in Q2.60 (saturates only for components far outside [-1, 1]).</summary>
    public ulong LengthSquaredWide
    {
        get
        {
            long rx = x.RawValue, ry = y.RawValue, rz = z.RawValue, rw = w.RawValue;
            ulong sum = (ulong)(rx * rx) + (ulong)(ry * ry) + (ulong)(rz * rz);
            return fWideMath.AddSaturating(sum, (ulong)(rw * rw));
        }
    }

    /// <summary>True when |q| is 1 within rounding tolerance.</summary>
    public bool IsNormalized
    {
        get
        {
            ulong l = LengthSquaredWide;
            return l <= (ulong)(ONE_Q60 + VALID_TOLERANCE_Q60) && l >= (ulong)(ONE_Q60 - VALID_TOLERANCE_Q60);
        }
    }

    public static bool TryNormalize(fQuaternion q, out fQuaternion result)
    {
        return TryNormalizeWide(q.x.RawValue, q.y.RawValue, q.z.RawValue, q.w.RawValue, out result);
    }

    /// <summary>Normalized q; the zero quaternion returns identity (reported as InvalidQuaternion).</summary>
    public static fQuaternion Normalize(fQuaternion q)
    {
        if (TryNormalize(q, out fQuaternion result))
            return result;
        fMathValidation.Report(fMathValidationKind.InvalidQuaternion, "fQuaternion.Normalize(zero)");
        return identity;
    }

    static bool TryNormalizeWide(long qx, long qy, long qz, long qw, out fQuaternion result)
    {
        bool ok = fWideMath.TryNormalizeQ30(qx, qy, qz, qw, out int rx, out int ry, out int rz, out int rw);
        result = ok ? new fQuaternion(rx, ry, rz, rw) : identity;
        return ok;
    }
    #endregion

    #region Conjugate / inverse / dot
    public static fQuaternion Conjugate(fQuaternion q) => new fQuaternion(-q.x.RawValue, -q.y.RawValue, -q.z.RawValue, q.w.RawValue);

    /// <summary>Inverse of a unit quaternion (its conjugate).</summary>
    public static fQuaternion Inverse(fQuaternion rotation) => Conjugate(rotation);

    /// <summary>
    /// Inverse of a general quaternion: conjugate / |q|² with a wide length. False for the zero
    /// quaternion or when a component of the result would leave the Q1.30 range.
    /// </summary>
    public static bool TryInverse(fQuaternion q, out fQuaternion result)
    {
        ulong lengthSquared = q.LengthSquaredWide >> Q30; // Q30
        if (lengthSquared == 0)
        {
            result = identity;
            fMathValidation.Report(fMathValidationKind.InvalidQuaternion, "fQuaternion.TryInverse(zero)");
            return false;
        }
        long den = (long)lengthSquared;
        long ix = fWideMath.DivideRoundToEven(-(long)q.x.RawValue << Q30, den);
        long iy = fWideMath.DivideRoundToEven(-(long)q.y.RawValue << Q30, den);
        long iz = fWideMath.DivideRoundToEven(-(long)q.z.RawValue << Q30, den);
        long iw = fWideMath.DivideRoundToEven((long)q.w.RawValue << Q30, den);
        bool ok = Fits(ix) && Fits(iy) && Fits(iz) && Fits(iw);
        result = new fQuaternion(fWideMath.SaturateToInt(ix), fWideMath.SaturateToInt(iy), fWideMath.SaturateToInt(iz), fWideMath.SaturateToInt(iw));
        return ok;
    }

    static bool Fits(long v) => v >= int.MinValue && v <= int.MaxValue;

    /// <summary>a · b in Q2.60.</summary>
    public static long DotWide(fQuaternion a, fQuaternion b)
    {
        return (long)a.x.RawValue * b.x.RawValue + (long)a.y.RawValue * b.y.RawValue
             + (long)a.z.RawValue * b.z.RawValue + (long)a.w.RawValue * b.w.RawValue;
    }

    public static funit Dot(fQuaternion a, fQuaternion b)
    {
        return funit.FromRaw(fWideMath.SaturateToInt(fWideMath.RoundShiftRightToEven(DotWide(a, b), Q30)));
    }
    #endregion

    #region Multiply / rotate
    /// <summary>Hamilton product; each component is a 4-term Q2.60 sum rounded once to Q1.30.</summary>
    public static fQuaternion operator *(fQuaternion a, fQuaternion b)
    {
        long ax = a.x.RawValue, ay = a.y.RawValue, az = a.z.RawValue, aw = a.w.RawValue;
        long bx = b.x.RawValue, by = b.y.RawValue, bz = b.z.RawValue, bw = b.w.RawValue;
        return new fQuaternion(
            RoundQ60(aw * bx + ax * bw + ay * bz - az * by),
            RoundQ60(aw * by + ay * bw + az * bx - ax * bz),
            RoundQ60(aw * bz + az * bw + ax * by - ay * bx),
            RoundQ60(aw * bw - ax * bx - ay * by - az * bz));
    }

    static int RoundQ60(long v) => fWideMath.SaturateToInt(fWideMath.RoundShiftRightToEven(v, Q30));

    /// <summary>Row-major Q1.30 rotation matrix of a unit quaternion.</summary>
    void GetMatrix(out long m00, out long m01, out long m02, out long m10, out long m11, out long m12, out long m20, out long m21, out long m22)
    {
        long qx = x.RawValue, qy = y.RawValue, qz = z.RawValue, qw = w.RawValue;
        long xx = qx * qx, yy = qy * qy, zz = qz * qz;
        long xy = qx * qy, xz = qx * qz, yz = qy * qz;
        long wx = qw * qx, wy = qw * qy, wz = qw * qz;
        m00 = fWideMath.RoundShiftRightToEven(ONE_Q60 - 2 * (yy + zz), Q30);
        m01 = fWideMath.RoundShiftRightToEven(2 * (xy - wz), Q30);
        m02 = fWideMath.RoundShiftRightToEven(2 * (xz + wy), Q30);
        m10 = fWideMath.RoundShiftRightToEven(2 * (xy + wz), Q30);
        m11 = fWideMath.RoundShiftRightToEven(ONE_Q60 - 2 * (xx + zz), Q30);
        m12 = fWideMath.RoundShiftRightToEven(2 * (yz - wx), Q30);
        m20 = fWideMath.RoundShiftRightToEven(2 * (xz - wy), Q30);
        m21 = fWideMath.RoundShiftRightToEven(2 * (yz + wx), Q30);
        m22 = fWideMath.RoundShiftRightToEven(ONE_Q60 - 2 * (xx + yy), Q30);
    }

    /// <summary>Rotates a Q16.16 vector: Q1.30 matrix × Q16.16 vector with 64-bit row sums.</summary>
    public static fVector3 operator *(fQuaternion rotation, fVector3 point)
    {
        rotation.GetMatrix(out long m00, out long m01, out long m02, out long m10, out long m11, out long m12, out long m20, out long m21, out long m22);
        long px = point.x.RawValue, py = point.y.RawValue, pz = point.z.RawValue;
        return fVector3.FromRaw(
            RoundQ60Sum(m00 * px + m01 * py + m02 * pz),
            RoundQ60Sum(m10 * px + m11 * py + m12 * pz),
            RoundQ60Sum(m20 * px + m21 * py + m22 * pz));
    }

    static int RoundQ60Sum(long v) => fWideMath.SaturateToInt(fWideMath.RoundShiftRightToEven(v, Q30));

    /// <summary>Rotates a unit direction; the result is renormalized from the exact Q2.60 sums.</summary>
    public static fUnitVector3 operator *(fQuaternion rotation, fUnitVector3 direction)
    {
        rotation.GetMatrix(out long m00, out long m01, out long m02, out long m10, out long m11, out long m12, out long m20, out long m21, out long m22);
        long dx = direction.x.RawValue, dy = direction.y.RawValue, dz = direction.z.RawValue;
        fWideMath.TryNormalizeQ30(m00 * dx + m01 * dy + m02 * dz, m10 * dx + m11 * dy + m12 * dz, m20 * dx + m21 * dy + m22 * dz, 0,
            out int rx, out int ry, out int rz, out _);
        return fUnitVector3.FromRawUnchecked(rx, ry, rz);
    }

    public fVector3 Rotate(fVector3 point) => this * point;
    #endregion

    #region Construction
    /// <summary>Rotation by angle around a unit axis: (axis * sin(angle/2), cos(angle/2)), normalized.</summary>
    public static fQuaternion AngleAxis(fAngle angle, fUnitVector3 axis) => AngleAxisHalf(angle.Half, axis);

    /// <summary>Signed variant (negative angles rotate clockwise around axis).</summary>
    public static fQuaternion AngleAxis(fAngleDelta angle, fUnitVector3 axis) => AngleAxisHalf(angle.Half.ToAngle(), axis);

    /// <summary>AngleAxis for an arbitrary axis vector; false when the axis is zero.</summary>
    public static bool TryAngleAxis(fAngle angle, fVector3 axis, out fQuaternion result)
    {
        if (!fVector3.TryNormalize(axis, out fUnitVector3 unitAxis))
        {
            result = identity;
            return false;
        }
        result = AngleAxisHalf(angle.Half, unitAxis);
        return true;
    }

    public static bool TryAngleAxis(fAngle angle, fUnitVector3 axis, out fQuaternion result)
    {
        if (!axis.IsValid)
        {
            result = identity;
            return false;
        }
        result = AngleAxisHalf(angle.Half, axis);
        return true;
    }

    static fQuaternion AngleAxisHalf(fAngle half, fUnitVector3 axis)
    {
        long s = fTrig.Sin(half).RawValue;
        long c = fTrig.Cos(half).RawValue;
        if (TryNormalizeWide(axis.x.RawValue * s, axis.y.RawValue * s, axis.z.RawValue * s, c << Q30, out fQuaternion q))
            return q;
        fMathValidation.Report(fMathValidationKind.InvalidArgument, "fQuaternion.AngleAxis with zero axis");
        return identity;
    }

    /// <summary>
    /// Unity-convention Euler rotation (applied Z, then X, then Y: q = qy * qx * qz).
    /// Intended for authoring and debugging; simulation state should store quaternions or fAngle.
    /// </summary>
    public static fQuaternion Euler(fAngle x, fAngle y, fAngle z)
    {
        return AngleAxis(y, fUnitVector3.up) * AngleAxis(x, fUnitVector3.right) * AngleAxis(z, fUnitVector3.forward);
    }

    /// <summary>
    /// Inverse of <see cref="Euler"/>: angles in Angle32 units (x within [-90°, 90°] as a wrapped fAngle).
    /// At the gimbal singularity z is 0 and the whole yaw goes into y.
    /// </summary>
    public static void ToEuler(fQuaternion rotation, out fAngle x, out fAngle y, out fAngle z)
    {
        rotation.GetMatrix(out long m00, out _, out long m02, out long m10, out long m11, out long m12, out long m20, out _, out long m22);
        ulong cosX = fWideMath.IntegerSqrtRounded((ulong)(m10 * m10 + m11 * m11)); // Q30
        x = fTrig.Atan2Wide(-m12, (long)cosX);
        if (cosX < 1UL << 10) // |cos x| < ~1e-6: gimbal lock
        {
            y = fTrig.Atan2Wide(-m20, m00);
            z = fAngle.Zero;
        }
        else
        {
            y = fTrig.Atan2Wide(m02, m22);
            z = fTrig.Atan2Wide(m10, m11);
        }
    }

    /// <summary>
    /// Shortest-arc rotation taking direction from onto direction to.
    /// Equal inputs give identity; opposite inputs (sin(angle) &lt; ~1e-6) give a half turn around a deterministic
    /// perpendicular axis (the world axis least aligned with from, crossed with from).
    /// </summary>
    public static fQuaternion FromToRotation(fUnitVector3 from, fUnitVector3 to)
    {
        // q = (from x to, |from||to| + from . to), normalized. Rounded unit vectors are not exactly
        // unit length, and for nearly opposite inputs 1 + from . to is tiny, so |from||to| is taken
        // as (|from|² + |to|²) / 2 (exact to second order) instead of 1.
        long dot = fUnitVector3.DotWide(from, to);
        fWideVector3 cross = fUnitVector3.CrossWide(from, to);
        long w = (from.LengthSquaredWide >> 1) + (to.LengthSquaredWide >> 1) + dot;

        bool crossTiny = Math.Abs(cross.x) < PARALLEL_THRESHOLD_Q60 && Math.Abs(cross.y) < PARALLEL_THRESHOLD_Q60 && Math.Abs(cross.z) < PARALLEL_THRESHOLD_Q60;
        if (dot < 0 && (crossTiny || w <= 0))
            return AngleAxis(fAngle.HalfTurn, PerpendicularAxis(from));
        if (cross.IsZero && dot > 0)
            return identity;

        return TryNormalizeWide(cross.x, cross.y, cross.z, w, out fQuaternion q) ? q : identity;
    }

    /// <summary>FromToRotation for arbitrary vectors; false when either is zero.</summary>
    public static bool TryFromToRotation(fVector3 from, fVector3 to, out fQuaternion result)
    {
        if (!fVector3.TryNormalize(from, out fUnitVector3 f) || !fVector3.TryNormalize(to, out fUnitVector3 t))
        {
            result = identity;
            return false;
        }
        result = FromToRotation(f, t);
        return true;
    }

    static fUnitVector3 PerpendicularAxis(fUnitVector3 v)
    {
        long ax = Math.Abs((long)v.x.RawValue), ay = Math.Abs((long)v.y.RawValue), az = Math.Abs((long)v.z.RawValue);
        fUnitVector3 reference = ax <= ay && ax <= az ? fUnitVector3.right : ay <= az ? fUnitVector3.up : fUnitVector3.forward;
        fUnitVector3.TryNormalizedCross(v, reference, out fUnitVector3 axis);
        return axis;
    }

    /// <summary>
    /// Rotation whose +Z looks along forward and whose +Y is as close to up as possible.
    /// The basis is built as right = normalize(up × forward), up' = normalize(forward × right) and
    /// converted from the orthonormal matrix. When up is zero or (nearly) parallel to forward, the
    /// result is FromToRotation(+Z, forward). False only when forward is zero.
    /// </summary>
    public static bool TryLookRotation(fVector3 forward, fVector3 up, out fQuaternion result)
    {
        if (!fVector3.TryNormalize(forward, out fUnitVector3 f))
        {
            result = identity;
            return false;
        }
        if (!fVector3.TryNormalize(up, out fUnitVector3 u))
            u = fUnitVector3.up;
        result = LookRotation(f, u);
        return true;
    }

    public static bool TryLookRotation(fVector3 forward, out fQuaternion result) => TryLookRotation(forward, fVector3.up, out result);

    public static fQuaternion LookRotation(fUnitVector3 forward, fUnitVector3 up)
    {
        fWideVector3 rightWide = fUnitVector3.CrossWide(up, forward);
        if (Math.Abs(rightWide.x) < PARALLEL_THRESHOLD_Q60 && Math.Abs(rightWide.y) < PARALLEL_THRESHOLD_Q60 && Math.Abs(rightWide.z) < PARALLEL_THRESHOLD_Q60)
            return FromToRotation(fUnitVector3.forward, forward);

        rightWide.TryNormalize(out fUnitVector3 r);
        fUnitVector3.CrossWide(forward, r).TryNormalize(out fUnitVector3 u);

        // columns: right, up, forward
        long m00 = r.x.RawValue, m10 = r.y.RawValue, m20 = r.z.RawValue;
        long m01 = u.x.RawValue, m11 = u.y.RawValue, m21 = u.z.RawValue;
        long m02 = forward.x.RawValue, m12 = forward.y.RawValue, m22 = forward.z.RawValue;
        long trace = m00 + m11 + m22;

        // Each branch is the quaternion scaled by 4 * (its largest component); normalization removes
        // the scale, so no square root or small divisor is needed.
        long qx, qy, qz, qw;
        if (trace > 0)
        {
            qx = m21 - m12; qy = m02 - m20; qz = m10 - m01; qw = ONE + trace;
        }
        else if (m00 >= m11 && m00 >= m22)
        {
            qx = ONE + m00 - m11 - m22; qy = m01 + m10; qz = m02 + m20; qw = m21 - m12;
        }
        else if (m11 > m22)
        {
            qx = m01 + m10; qy = ONE + m11 - m00 - m22; qz = m12 + m21; qw = m02 - m20;
        }
        else
        {
            qx = m02 + m20; qy = m12 + m21; qz = ONE + m22 - m00 - m11; qw = m10 - m01;
        }
        return TryNormalizeWide(qx, qy, qz, qw, out fQuaternion q) ? q : identity;
    }
    #endregion

    #region Interpolation
    /// <summary>Normalized linear interpolation along the shorter arc; t is clamped to [0, 1].</summary>
    public static fQuaternion Lerp(fQuaternion a, fQuaternion b, ffloat t) => LerpUnclamped(a, b, ffloat.Clamp01(t));

    public static fQuaternion LerpUnclamped(fQuaternion a, fQuaternion b, ffloat t)
    {
        if (DotWide(a, b) < 0)
            b = Negate(b);
        long tr = t.RawValue, ir = ffloat.RawOne - (long)t.RawValue;
        // Q1.30 * Q16.16 products; the common scale is removed by normalization
        return TryNormalizeWide(
            ir * a.x.RawValue + tr * b.x.RawValue,
            ir * a.y.RawValue + tr * b.y.RawValue,
            ir * a.z.RawValue + tr * b.z.RawValue,
            ir * a.w.RawValue + tr * b.w.RawValue, out fQuaternion q) ? q : a;
    }

    /// <summary>
    /// Spherical interpolation along the shorter arc; t is clamped to [0, 1].
    /// A negative dot flips all four components of b (never a conjugate). Very close inputs fall back
    /// to normalized lerp. The result is always normalized.
    /// </summary>
    public static fQuaternion Slerp(fQuaternion a, fQuaternion b, ffloat t)
    {
        if (t.RawValue <= 0)
            return a;
        long dot = DotWide(a, b);
        if (dot < 0)
        {
            b = Negate(b);
            dot = -dot;
        }
        if (t.RawValue >= ffloat.RawOne)
            return b;
        if (dot >= NLERP_THRESHOLD_Q60)
            return LerpUnclamped(a, b, t);

        fAngle theta = HalfAngleFromDot(dot);
        return SlerpByAngle(a, b, theta, theta * t);
    }

    /// <summary>Half angle between two unit quaternions from their (non-negative) Q2.60 dot.</summary>
    static fAngle HalfAngleFromDot(long dotQ60)
    {
        long d = fWideMath.RoundShiftRightToEven(dotQ60, Q30);
        if (d > ONE) d = ONE;
        long sin = (long)fWideMath.IntegerSqrtRounded((ulong)(ONE_Q60 - d * d));
        return fTrig.Atan2Wide(sin, d);
    }

    /// <summary>sin(θ - φ) a + sin(φ) b, normalized (the 1 / sin θ factor cancels).</summary>
    static fQuaternion SlerpByAngle(fQuaternion a, fQuaternion b, fAngle theta, fAngle phi)
    {
        long wa = fTrig.Sin(theta.Sub(phi)).RawValue;
        long wb = fTrig.Sin(phi).RawValue;
        return TryNormalizeWide(
            wa * a.x.RawValue + wb * b.x.RawValue,
            wa * a.y.RawValue + wb * b.y.RawValue,
            wa * a.z.RawValue + wb * b.z.RawValue,
            wa * a.w.RawValue + wb * b.w.RawValue, out fQuaternion q) ? q : a;
    }

    /// <summary>Rotates from towards to by at most maxDelta (rotation angle, not half angle).</summary>
    public static fQuaternion RotateTowards(fQuaternion from, fQuaternion to, fAngle maxDelta)
    {
        fAngle angle = Angle(from, to);
        if (angle.RawValue <= maxDelta.RawValue)
            return to;
        long dot = DotWide(from, to);
        if (dot < 0)
        {
            to = Negate(to);
            dot = -dot;
        }
        fAngle theta = HalfAngleFromDot(dot);
        if (dot >= NLERP_THRESHOLD_Q60)
            return LerpUnclamped(from, to, ffloat.FromRaw(fWideMath.SaturateToInt(fWideMath.DivideRoundToEven((long)maxDelta.RawValue << ffloat.FractionalBits, angle.RawValue))));
        return SlerpByAngle(from, to, theta, maxDelta.Half);
    }

    static fQuaternion Negate(fQuaternion q) => new fQuaternion(-q.x.RawValue, -q.y.RawValue, -q.z.RawValue, -q.w.RawValue);
    #endregion

    #region Angle / comparison
    /// <summary>
    /// Rotation angle between a and b in [0°, 180°]: 2 * atan2(|r.xyz|, |r.w|) with r = conj(a) * b,
    /// accurate for tiny angles and invariant to the sign of either quaternion.
    /// </summary>
    public static fAngle Angle(fQuaternion a, fQuaternion b)
    {
        long ax = -(long)a.x.RawValue, ay = -(long)a.y.RawValue, az = -(long)a.z.RawValue, aw = a.w.RawValue;
        long bx = b.x.RawValue, by = b.y.RawValue, bz = b.z.RawValue, bw = b.w.RawValue;
        long rx = aw * bx + ax * bw + ay * bz - az * by;
        long ry = aw * by + ay * bw + az * bx - ax * bz;
        long rz = aw * bz + az * bw + ax * by - ay * bx;
        long rw = aw * bw - ax * bx - ay * by - az * bz;
        ulong imaginary = fWideMath.LengthWide(rx, ry, rz, 0, out int shift);
        long real = fWideMath.RoundShiftRightToEven(rw < 0 ? -rw : rw, shift);
        uint half = fTrig.Atan2Wide((long)imaginary, real).RawValue; // [0°, 90°]
        return fAngle.FromRaw(half << 1);
    }

    /// <summary>True when a and b are raw-identical or exact negatives (the same rotation).</summary>
    public static bool IsSameRotation(fQuaternion a, fQuaternion b) => a == b || a == Negate(b);

    /// <summary>True when the rotation angle between a and b is at most tolerance.</summary>
    public static bool Approximately(fQuaternion a, fQuaternion b, fAngle tolerance) => Angle(a, b).RawValue <= tolerance.RawValue;

    /// <summary>Raw-exact component equality (q and -q are different values).</summary>
    public static bool operator ==(fQuaternion a, fQuaternion b) => a.x == b.x && a.y == b.y && a.z == b.z && a.w == b.w;
    public static bool operator !=(fQuaternion a, fQuaternion b) => !(a == b);
    public bool Equals(fQuaternion other) => this == other;
    public override bool Equals(object obj) => obj is fQuaternion other && this == other;
    public override int GetHashCode() => unchecked(((x.RawValue * 397 ^ y.RawValue) * 397 ^ z.RawValue) * 397 ^ w.RawValue);
    #endregion

    public override string ToString() => "(" + x + ", " + y + ", " + z + ", " + w + ")";
}
