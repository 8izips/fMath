using System;
using System.Runtime.InteropServices;
#if FMATH_ENABLE_NATIVE_PLUGIN
#if UNITY_IPHONE
    using fMathStaticLib;
#elif UNITY_ANDROID
using fMathDynamicLib;
#endif
#endif
using UnityEngine;

[StructLayout(LayoutKind.Explicit)]
public struct fVector3
{
    [FieldOffset(0)]
    public ffloat x;
    [FieldOffset(4)]
    public ffloat y;
    [FieldOffset(8)]
    public ffloat z;

    #region Defines
    static readonly fVector3 zeroVector = new fVector3(0, 0, 0);
    static readonly fVector3 oneVector = new fVector3(1, 1, 1);
    static readonly fVector3 upVector = new fVector3(0, 1, 0);
    static readonly fVector3 downVector = new fVector3(0, -1, 0);
    static readonly fVector3 leftVector = new fVector3(-1, 0, 0);
    static readonly fVector3 rightVector = new fVector3(1, 0, 0);
    static readonly fVector3 forwardVector = new fVector3(0, 0, 1);
    static readonly fVector3 backVector = new fVector3(0, 0, -1);

    public static fVector3 zero { get { return zeroVector; } }
    public static fVector3 one { get { return oneVector; } }
    public static fVector3 up { get { return upVector; } }
    public static fVector3 down { get { return downVector; } }
    public static fVector3 left { get { return leftVector; } }
    public static fVector3 right { get { return rightVector; } }
    public static fVector3 forward { get { return forwardVector; } }
    public static fVector3 back { get { return backVector; } }
    #endregion

    #region Constructors
    public fVector3(ffloat x, ffloat y, ffloat z)
    {
        this.x = x;
        this.y = y;
        this.z = z;
    }

    public fVector3(int x, int y, int z)
    {
        this.x = (ffloat)x;
        this.y = (ffloat)y;
        this.z = (ffloat)z;
    }

    public fVector3(float x, float y, float z)
    {
        this.x = (ffloat)x;
        this.y = (ffloat)y;
        this.z = (ffloat)z;
    }

    public fVector3(Vector3 vector)
    {
        x = (ffloat)vector.x;
        y = (ffloat)vector.y;
        z = (ffloat)vector.z;
    }

    public static fVector3 CreateFromRawValue(int rawX, int rawY, int rawZ)
    {
        return new fVector3(ffloat.CreateFromRawValue(rawX), ffloat.CreateFromRawValue(rawY), ffloat.CreateFromRawValue(rawZ));
    }
    #endregion

    public fVector3 normalized {
        get {
            fVector3 result = this;
            result.Normalize();
            return result;
        }
    }

    public ffloat magnitude => ffloat.Sqrt(x * x + y * y + z * z);
    public ffloat sqrMagnitude => x * x + y * y + z * z;

    public void Normalize()
    {
        ffloat mag = magnitude;
        if (mag > ffloat.Epsilon)
        {
            x /= mag;
            y /= mag;
            z /= mag;
        }
    }

    public void Scale(fVector3 scale)
    {
        this.x *= scale.x;
        this.y *= scale.y;
        this.z *= scale.z;
    }

    public void Set(ffloat newX, ffloat newY, ffloat newZ)
    {
        this.x = newX;
        this.y = newY;
        this.z = newZ;
    }

    public override string ToString()
    {
        return String.Format("({0:F2}, {1:F2}, {2:F2})", new object[] { x.ToFloat(), y.ToFloat(), z.ToFloat() });
    }

    #region Arithmetic Operators
    public static fVector3 operator +(fVector3 lhs, fVector3 rhs)
    {
#if FMATH_ENABLE_NATIVE_PLUGIN && !UNITY_EDITOR
        fVector3 result;
        fvector3_add(ref lhs, ref rhs, out result);
        return result;
#else
        return new fVector3(lhs.x + rhs.x, lhs.y + rhs.y, lhs.z + rhs.z);
#endif
    }

    public static fVector3 operator -(fVector3 vec)
    {
        return new fVector3(-vec.x, -vec.y, -vec.z);
    }

    public static fVector3 operator -(fVector3 lhs, fVector3 rhs)
    {
#if FMATH_ENABLE_NATIVE_PLUGIN && !UNITY_EDITOR
        fVector3 result;
        fvector3_sub(ref lhs, ref rhs, out result);
        return result;
#else
        return new fVector3(lhs.x - rhs.x, lhs.y - rhs.y, lhs.z - rhs.z);
#endif
    }

    public static fVector3 operator *(fVector3 lhs, ffloat rhs)
    {
#if FMATH_ENABLE_NATIVE_PLUGIN && !UNITY_EDITOR
        fVector3 result;
        fvector3_mul(ref lhs, rhs, out result);
        return result;
#else
        return new fVector3(lhs.x * rhs, lhs.y * rhs, lhs.z * rhs);
#endif
    }

    public static fVector3 operator *(ffloat lhs, fVector3 rhs)
    {
#if FMATH_ENABLE_NATIVE_PLUGIN && !UNITY_EDITOR
        fVector3 result;
        fvector3_mul(ref rhs, lhs, out result);
        return result;
#else
        return new fVector3(lhs * rhs.x, lhs * rhs.y, lhs * rhs.z);
#endif
    }

    public static fVector3 operator *(fVector3 lhs, int rhs)
    {
        return new fVector3(lhs.x * rhs, lhs.y * rhs, lhs.z * rhs);
    }

    public static fVector3 operator *(int lhs, fVector3 rhs)
    {
        return new fVector3(lhs * rhs.x, lhs * rhs.y, lhs * rhs.z);
    }

    public static fVector3 operator *(fVector3 lhs, float rhs)
    {
        return new fVector3(lhs.x * (ffloat)rhs, lhs.y * (ffloat)rhs, lhs.z * (ffloat)rhs);
    }

    public static fVector3 operator *(float lhs, fVector3 rhs)
    {
        return new fVector3((ffloat)lhs * rhs.x, (ffloat)lhs * rhs.y, (ffloat)lhs * rhs.z);
    }

    public static fVector3 operator /(fVector3 lhs, ffloat rhs)
    {
#if FMATH_ENABLE_NATIVE_PLUGIN && !UNITY_EDITOR
        fVector3 result;
        fvector3_div(ref lhs, rhs, out result);
        return result;
#else
        return new fVector3(lhs.x / rhs, lhs.y / rhs, lhs.z / rhs);
#endif
    }
    #endregion

    #region Comparison Operators
    public static bool operator ==(fVector3 lhs, fVector3 rhs)
    {
        return (lhs - rhs).sqrMagnitude < ffloat.Epsilon;
    }

    public static bool operator !=(fVector3 lhs, fVector3 rhs)
    {
        return !(lhs == rhs);
    }

    public override bool Equals(object obj)
    {
        return obj is fVector3 &&
            (((fVector3)obj).x == x) &&
            (((fVector3)obj).y == y) &&
            (((fVector3)obj).z == z);
    }

    public bool Equals(fVector3 other)
    {
        return (x == other.x) && (y == other.y) && (z == other.z);
    }

    public override int GetHashCode()
    {
        return x.GetHashCode() ^ y.GetHashCode() << 2 ^ z.GetHashCode() >> 2;
    }
    #endregion

    #region Conversion Operators
    public Vector3 ToUnityVector3()
    {
        return new Vector3(x.ToFloat(), y.ToFloat(), z.ToFloat());
    }
    #endregion

    #region Math Functions
    public static ffloat Distance(fVector3 lhs, fVector3 rhs)
    {
        return (lhs - rhs).magnitude;
    }

    public static ffloat Angle(fVector3 from, fVector3 to)
    {
        return ffloat.Acos(ffloat.Clamp(fVector3.Dot(from.normalized, to.normalized), -ffloat.One, ffloat.One)) * ffloat.Rad2Deg;
    }

    public static ffloat Dot(fVector3 lhs, fVector3 rhs)
    {
#if FMATH_ENABLE_NATIVE_PLUGIN && !UNITY_EDITOR
        return fvector3_dot(ref lhs, ref rhs);
#else
        return lhs.x * rhs.x + lhs.y * rhs.y + lhs.z * rhs.z;
#endif
    }

    public static fVector3 Cross(fVector3 lhs, fVector3 rhs)
    {
#if FMATH_ENABLE_NATIVE_PLUGIN && !UNITY_EDITOR
        fVector3 result;
        fvector3_cross(ref lhs, ref rhs, out result);
        return result;
#else
        return new fVector3(
            lhs.y * rhs.z - lhs.z * rhs.y,
            lhs.z * rhs.x - lhs.x * rhs.z,
            lhs.x * rhs.y - lhs.y * rhs.x);
#endif
    }

    public static fVector3 Lerp(fVector3 start, fVector3 end, ffloat t)
    {
        return new fVector3(
            start.x + (end.x - start.x) * t,
            start.y + (end.y - start.y) * t,
            start.z + (end.z - start.z) * t);
    }

    public static fVector3 MoveTowards(fVector3 current, fVector3 target, ffloat maxDistance)
    {
        fVector3 diff = target - current;
        ffloat magnitude = diff.magnitude;

        if (magnitude <= maxDistance)
            return target;

        return current + diff / magnitude * maxDistance;
    }

    public static fVector3 Reflect(fVector3 direction, fVector3 normal)
    {
        return -2 * fVector3.Dot(normal, direction) * normal + direction;
    }
    #endregion

    #region Native Plugin Interface
#if FMATH_ENABLE_NATIVE_PLUGIN
    [DllImport(fMathNativeImport.libName)]
    public static extern void fvector3_add(ref fVector3 lhs, ref fVector3 rhs, out fVector3 result);

    [DllImport(fMathNativeImport.libName)]
    public static extern void fvector3_sub(ref fVector3 lhs, ref fVector3 rhs, out fVector3 result);

    [DllImport(fMathNativeImport.libName)]
    public static extern void fvector3_mul(ref fVector3 lhs, ffloat rhs, out fVector3 result);

    [DllImport(fMathNativeImport.libName)]
    public static extern void fvector3_div(ref fVector3 lhs, ffloat rhs, out fVector3 result);

    [DllImport(fMathNativeImport.libName)]
    public static extern ffloat fvector3_dot(ref fVector3 lhs, ref fVector3 rhs);

    [DllImport(fMathNativeImport.libName)]
    public static extern void fvector3_cross(ref fVector3 lhs, ref fVector3 rhs, out fVector3 result);
#endif
    #endregion
}
