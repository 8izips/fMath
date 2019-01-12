using System;
using System.Runtime.InteropServices;
using UnityEngine;

[StructLayout(LayoutKind.Explicit)]
public struct fQuaternion
{
    [FieldOffset(0)]
    public ffloat x;
    [FieldOffset(4)]
    public ffloat y;
    [FieldOffset(8)]
    public ffloat z;
    [FieldOffset(12)]
    public ffloat w;

    #region Defines
    static readonly fQuaternion identityQuaternion = new fQuaternion(0, 0, 0, 1);

    public static fQuaternion identity { get { return identityQuaternion; } }
    #endregion

    #region Construtor
    public fQuaternion(ffloat x, ffloat y, ffloat z, ffloat w)
    {
        this.x = x;
        this.y = y;
        this.z = z;
        this.w = w;
    }

    public fQuaternion(int x, int y, int z, int w)
    {
        this.x = (ffloat)x;
        this.y = (ffloat)y;
        this.z = (ffloat)z;
        this.w = (ffloat)w;
    }

    public fQuaternion(float x, float y, float z, float w)
    {
        this.x = (ffloat)x;
        this.y = (ffloat)y;
        this.z = (ffloat)z;
        this.w = (ffloat)w;
    }

    public fQuaternion(fQuaternion other)
    {
        x = other.x;
        y = other.y;
        z = other.z;
        w = other.w;
    }

    public fQuaternion(Quaternion other)
    {
        x = (ffloat)other.x;
        y = (ffloat)other.y;
        z = (ffloat)other.z;
        w = (ffloat)other.w;
    }

    public fQuaternion(fVector3 vec, ffloat f)
    {
        x = vec.x;
        y = vec.y;
        z = vec.z;
        w = f;
    }
    #endregion

    fVector3 xyz {
        get {
            return new fVector3(x, y, z);
        }
    }

    ffloat length {
        get {
            return ffloat.Sqrt(sqrLength);
        }
    }

    ffloat sqrLength {
        get {
            return x * x + y * y + z * z + w * w;
        }
    }

    public fVector3 eulerAngles {
        get {
            return ToEuler(this);
        }
        set {
            this = Euler(value);
        }
    }

    public void Set(ffloat newX, ffloat newY, ffloat newZ, ffloat newW)
    {
        this.x = newX;
        this.y = newY;
        this.z = newZ;
        this.w = newW;
    }

    public void Scale(ffloat scale)
    {
        this.x *= scale;
        this.y *= scale;
        this.z *= scale;
        this.w *= scale;
    }

    void Normalize()
    {
        ffloat scale = ffloat.One / length;
        x *= scale;
        y *= scale;
        z *= scale;
        w *= scale;
    }

    void Conjugate()
    {
        x = -x;
        y = -y;
        z = -z;
    }

    public override string ToString()
    {
        return String.Format("({0:F2}, {1:F2}, {2:F2}, {3:F2})", new object[] { x.ToFloat(), y.ToFloat(), z.ToFloat(), w.ToFloat() });
    }

    #region Arithmetic Operators
    public static fQuaternion operator *(fQuaternion lhs, fQuaternion rhs)
    {
        return new fQuaternion(
            lhs.w * rhs.x + lhs.x * rhs.w + lhs.y * rhs.z - lhs.z * rhs.y,
            lhs.w * rhs.y + lhs.y * rhs.w + lhs.z * rhs.x - lhs.x * rhs.z,
            lhs.w * rhs.z + lhs.z * rhs.w + lhs.x * rhs.y - lhs.y * rhs.x,
            lhs.w * rhs.w - lhs.x * rhs.x - lhs.y * rhs.y - lhs.z * rhs.z);
    }

    public static fVector3 operator *(fQuaternion rotation, fVector3 point)
    {
        // nVidia SDK implementation
        fVector3 qvec = new fVector3(rotation.x, rotation.y, rotation.z);
        fVector3 uv = fVector3.Cross(qvec, point);
        fVector3 uuv = fVector3.Cross(qvec, uv);
        uv *= 2 * rotation.w;
        uuv *= 2;

        return point + uv + uuv;
    }
    #endregion

    #region Comparison Operators
    static bool IsEqualUsingDot(ffloat dot)
    {
        return dot > (ffloat.One - ffloat.Epsilon);
    }

    public static bool operator ==(fQuaternion lhs, fQuaternion rhs)
    {
        return IsEqualUsingDot(fQuaternion.Dot(lhs, rhs));
    }

    public static bool operator !=(fQuaternion lhs, fQuaternion rhs)
    {
        return !(lhs == rhs);
    }

    public override bool Equals(object obj)
    {
        if (!(obj is fQuaternion))
            return false;

        fQuaternion other = (fQuaternion)obj;
        return x.Equals(other.x) &&
            y.Equals(other.y) &&
            z.Equals(other.z) &&
            w.Equals(other.w);
    }

    public override int GetHashCode()
    {
        return x.GetHashCode() ^ y.GetHashCode() << 2 ^ z.GetHashCode() >> 2 ^ w.GetHashCode() >> 1;
    }
    #endregion

    #region Conversion Operators
    public static fQuaternion Euler(ffloat x, ffloat y, ffloat z)
    {
        ffloat halfRad = ffloat.Deg2Rad * ffloat.Half;
        x *= halfRad;
        y *= halfRad;
        z *= halfRad;
        ffloat cx = ffloat.Cos(x);
        ffloat cy = ffloat.Cos(y);
        ffloat cz = ffloat.Cos(z);
        ffloat sx = ffloat.Sin(x);
        ffloat sy = ffloat.Sin(y);
        ffloat sz = ffloat.Sin(z);

        return new fQuaternion(
            sx * cy * cz + cx * sy * sz,
            cx * sy * cz - sx * cy * sz,
            cx * cy * sz - sx * sy * cz,
            cx * cy * cz + sx * sy * sz);
    }

    public static fQuaternion Euler(fVector3 euler)
    {
        return Euler(euler.x, euler.y, euler.z);
    }

    public static fQuaternion Euler(float x, float y, float z)
    {
        return Euler((ffloat)x, (ffloat)y, (ffloat)z);
    }

    public static fQuaternion AngleAxis(ffloat angle, fVector3 axis)
    {
        ffloat halfRad = angle * ffloat.Deg2Rad * ffloat.Half;

        return new fQuaternion((axis * ffloat.Sin(halfRad)).normalized, ffloat.Cos(halfRad));
    }

    public static fQuaternion LookRotation(fVector3 position)
    {
        return LookRotation(position, fVector3.up);
    }

    public static fQuaternion LookRotation(fVector3 position, fVector3 upVector)
    {
        fVector3 forward = position.normalized;
        fVector3 right = fVector3.Cross(upVector, forward);
        upVector = fVector3.Cross(forward, right);

        ffloat m00 = right.x;
        ffloat m01 = right.y;
        ffloat m02 = right.z;
        ffloat m10 = upVector.x;
        ffloat m11 = upVector.y;
        ffloat m12 = upVector.z;
        ffloat m20 = forward.x;
        ffloat m21 = forward.y;
        ffloat m22 = forward.z;

        ffloat diag = m00 + m11 + m22;

        fQuaternion result = new fQuaternion();
        if (diag > ffloat.Zero) {
            ffloat scale = ffloat.Sqrt(diag + ffloat.One);
            result.w = scale * ffloat.Half;
            scale = ffloat.Half / scale;

            result.x = (m12 - m21) * scale;
            result.y = (m20 - m02) * scale;
            result.z = (m01 - m10) * scale;
        }
        else if ((m00 >= m11) && (m00 >= m22)) {
            ffloat scale = ffloat.Sqrt(((ffloat.One + m00) - m11) - m22);
            result.x = ffloat.Half * scale;
            scale = ffloat.Half / scale;
            result.y = (m01 + m10) * scale;
            result.z = (m02 + m20) * scale;
            result.w = (m12 - m21) * scale;
        }
        else if (m11 > m22) {
            ffloat scale = ffloat.Sqrt(((ffloat.One + m11) - m00) - m22);
            result.y = ffloat.Half * scale;
            scale = ffloat.Half / scale;
            result.x = (m10 + m01) * scale;
            result.z = (m21 + m12) * scale;
            result.w = (m20 - m02) * scale;
        }
        else {
            ffloat scale = ffloat.Sqrt(((ffloat.One + m22) - m00) - m11);
            result.z = ffloat.Half * scale;
            scale = ffloat.Half / scale;
            result.x = (m20 + m02) * scale;
            result.y = (m21 + m12) * scale;
            result.w = (m01 - m10) * scale;
        }

        return result;
    }

    public static fQuaternion FromToRotation(fVector3 from, fVector3 to)
    {
        fVector3 fn = from.normalized;
        fVector3 tn = to.normalized;

        ffloat dot = fVector3.Dot(fn, tn);
        if (dot <= -ffloat.One) {
            fVector3 tmp = fVector3.Cross(fVector3.right, fn);
            if (tmp.magnitude < ffloat.Epsilon)
                tmp = fVector3.Cross(fVector3.up, fn);
            tmp.Normalize();

            return AngleAxis(ffloat.Pi, tmp);
        }
        else if (dot >= ffloat.One) {
            return identity;
        }
        ffloat scale = ffloat.Sqrt((ffloat.One + dot) * 2);
        ffloat inv = ffloat.One / scale;
        fVector3 cross = fVector3.Cross(fn, tn);
        fQuaternion result = new fQuaternion(cross * inv, scale * ffloat.Half);
        result.Normalize();

        return result;
    }

    public static fVector3 ToEuler(fQuaternion rot)
    {
        ffloat x2 = rot.x * rot.x;
        ffloat y2 = rot.y * rot.y;
        ffloat z2 = rot.z * rot.z;
        ffloat w2 = rot.w * rot.w;
        ffloat unit = x2 + y2 + z2 + w2;
        ffloat test = rot.x * rot.w - rot.y * rot.z;

        if (test >= ffloat.Half * unit) {
            return new fVector3(2 * ffloat.Atan2(rot.x, rot.w), ffloat.HalfPi, ffloat.Zero);
        }
        if (test <= -ffloat.Half * unit) {
            return new fVector3(-2 * ffloat.Atan2(rot.x, rot.w), -ffloat.HalfPi, ffloat.Zero);
        }

        return NormalizedAngleVector(
            ffloat.Asin(2 * test),
            ffloat.Atan2(2 * rot.w * rot.y + 2 * rot.z * rot.x, ffloat.One - 2 * (x2 + y2)),
            ffloat.Atan2(2 * rot.w * rot.z + 2 * rot.x * rot.y, ffloat.One - 2 * (z2 + x2)));
    }

    static fVector3 NormalizedAngleVector(ffloat x, ffloat y, ffloat z)
    {
        return new fVector3(NormalizeAngle(x), NormalizeAngle(y), NormalizeAngle(z));
    }

    static ffloat NormalizeAngle(ffloat rad)
    {
        ffloat oneRound = new ffloat(360);
        ffloat degree = rad * ffloat.Rad2Deg;

        while (degree > oneRound)
            degree -= oneRound;
        while (degree < ffloat.Zero)
            degree += oneRound;

        return degree;
    }

    public static fQuaternion Inverse(fQuaternion rotation)
    {
        ffloat inv = ffloat.One / (rotation.x * rotation.x + rotation.y * rotation.y + rotation.z * rotation.z + rotation.w * rotation.w);
        return new fQuaternion(-rotation.x * inv, -rotation.y * inv, -rotation.z * inv, rotation.w * inv);
    }

    public Quaternion ToUnityQuaternion()
    {
        return new Quaternion(x.ToFloat(), y.ToFloat(), z.ToFloat(), w.ToFloat());
    }
    #endregion

    #region MathFunctions
    public static ffloat Angle(fQuaternion lhs, fQuaternion rhs)
    {
        ffloat dot = Dot(lhs, rhs);
        return (!IsEqualUsingDot(dot)) ? ffloat.Acos(ffloat.Min(ffloat.Abs(dot), ffloat.One)) * 2 * ffloat.Rad2Deg : ffloat.Zero;
    }

    public static ffloat Dot(fQuaternion lhs, fQuaternion rhs)
    {
        return lhs.x * rhs.x + lhs.y * rhs.y + lhs.z * rhs.z + lhs.w * rhs.w;
    }

    public static fQuaternion Lerp(fQuaternion from, fQuaternion to, ffloat t)
    {
        fQuaternion result;
        ffloat inv = ffloat.One - t;
        if (Dot(from, to) > ffloat.Zero) {
            result = new fQuaternion(
                inv * from.x + t * to.x,
                inv * from.y + t * to.y,
                inv * from.z + t * to.z,
                inv * from.w + t * to.w);
        }
        else {
            result = new fQuaternion(
                inv * from.x - t * to.x,
                inv * from.y - t * to.y,
                inv * from.z - t * to.z,
                inv * from.w - t * to.w);
        }

        return result;
    }

    public static fQuaternion Slerp(fQuaternion from, fQuaternion to, ffloat t)
    {
        if (from.sqrLength == ffloat.Zero) {
            if (to.sqrLength == ffloat.Zero)
                return identity;
            return to;
        }
        else if (to.sqrLength == ffloat.Zero)
            return from;

        ffloat cosHalf = from.w * to.w + fVector3.Dot(from.xyz, to.xyz);
        if (cosHalf >= ffloat.One || cosHalf <= -ffloat.One)
            return from;
        else if (cosHalf <= ffloat.Zero) {
            to.Conjugate();
            cosHalf = -cosHalf;
        }

        ffloat fromRate = ffloat.One - t;
        ffloat toRate = t;
        if (cosHalf <= ffloat.One) {
            ffloat half = ffloat.Acos(cosHalf);
            ffloat sinHalf = ffloat.Sin(half);
            ffloat overSinHalf = ffloat.One / sinHalf;

            fromRate = ffloat.Sin(half * fromRate) * overSinHalf;
            toRate = ffloat.Sin(half * toRate) * overSinHalf;
        }

        return new fQuaternion(fromRate * from.xyz + toRate * to.xyz, fromRate * from.w + toRate * to.w);
    }

    public static fQuaternion RotateTowards(fQuaternion from, fQuaternion to, ffloat maxDegree)
    {
        ffloat angle = Angle(from, to);
        if (angle == ffloat.Zero)
            return to;

        return Slerp(from, to, ffloat.Min(ffloat.One, maxDegree / angle));
    }
    #endregion
}
