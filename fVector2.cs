using System;
using System.Runtime.InteropServices;
using UnityEngine;

[StructLayout(LayoutKind.Explicit)]
public struct fVector2
{
    [FieldOffset(0)]
    public ffloat x;
    [FieldOffset(4)]
    public ffloat y;

    #region Defines
    static readonly fVector2 zeroVector = new fVector2(0, 0);
    static readonly fVector2 oneVector = new fVector2(1, 1);
    static readonly fVector2 upVector = new fVector2(0, 1);
    static readonly fVector2 downVector = new fVector2(0, -1);
    static readonly fVector2 leftVector = new fVector2(-1, 0);
    static readonly fVector2 rightVector = new fVector2(1, 0);

    public static fVector2 zero { get { return zeroVector; } }
    public static fVector2 one { get { return oneVector; } }
    public static fVector2 up { get { return upVector; } }
    public static fVector2 down { get { return downVector; } }
    public static fVector2 left { get { return leftVector; } }
    public static fVector2 right { get { return rightVector; } }
    #endregion

    #region Constructors
    public fVector2(ffloat x, ffloat y)
    {
        this.x = x;
        this.y = y;
    }

    public fVector2(int x, int y)
    {
        this.x = (ffloat)x;
        this.y = (ffloat)y;
    }

    public fVector2(float x, float y)
    {
        this.x = (ffloat)x;
        this.y = (ffloat)y;
    }

    public fVector2(Vector2 vector)
    {
        x = (ffloat)vector.x;
        y = (ffloat)vector.y;
    }

    public static fVector2 CreateFromRawValue(int rawX, int rawY)
    {
        return new fVector2(ffloat.CreateFromRawValue(rawX), ffloat.CreateFromRawValue(rawY));
    }
    #endregion

    public fVector2 normalized {
        get {
            fVector2 result = this;
            result.Normalize();
            return result;
        }
    }

    public ffloat magnitude {
        get {
            return ffloat.Sqrt(x * x + y * y);
        }
    }

    public ffloat sqrMagnitude {
        get {
            return x * x + y * y;
        }
    }

    public void Normalize()
    {
        ffloat mag = magnitude;
        if (mag > ffloat.Epsilon)
        {
            x /= mag;
            y /= mag;
        }
    }

    public void Scale(fVector2 scale)
    {
        this.x *= scale.x;
        this.y *= scale.y;
    }

    public void Set(ffloat newX, ffloat newY)
    {
        this.x = newX;
        this.y = newY;
    }

    public void Rotate(ffloat angle)
    {
        ffloat rad = angle * ffloat.Deg2Rad;
        ffloat sin = ffloat.Sin(rad);
        ffloat cos = ffloat.Cos(rad);

        ffloat tx = x;
        ffloat ty = y;

        x = cos * tx - sin * ty;
        y = sin * tx + cos * ty;
    }

    public override string ToString()
    {
        return String.Format("({0:F2}, {1:F2})", new object[] { x.ToFloat(), y.ToFloat() });
    }

    #region Arithmetic Operators
    public static fVector2 operator +(fVector2 lhs, fVector2 rhs)
    {
        return new fVector2(lhs.x + rhs.x, lhs.y + rhs.y);
    }

    public static fVector2 operator -(fVector2 vec)
    {
        return new fVector2(-vec.x, -vec.y);
    }

    public static fVector2 operator -(fVector2 lhs, fVector2 rhs)
    {
        return new fVector2(lhs.x - rhs.x, lhs.y - rhs.y);
    }

    public static fVector2 operator *(fVector2 lhs, fVector2 rhs)
    {
        return new fVector2(lhs.x * rhs.x, lhs.y * rhs.y);
    }

    public static fVector2 operator *(fVector2 lhs, ffloat rhs)
    {
        return new fVector2(lhs.x * rhs, lhs.y * rhs);
    }

    public static fVector2 operator *(ffloat lhs, fVector2 rhs)
    {
        return new fVector2(lhs * rhs.x, lhs * rhs.y);
    }

    public static fVector2 operator /(fVector2 lhs, fVector2 rhs)
    {
        return new fVector2(lhs.x / rhs.x, lhs.y / rhs.y);
    }

    public static fVector2 operator /(fVector2 lhs, ffloat rhs)
    {
        return new fVector2(lhs.x / rhs, lhs.y / rhs);
    }
    #endregion

    #region Comparison Operators
    public static bool operator ==(fVector2 lhs, fVector2 rhs)
    {
        return (lhs - rhs).sqrMagnitude < ffloat.Epsilon;
    }

    public static bool operator !=(fVector2 lhs, fVector2 rhs)
    {
        return !(lhs == rhs);
    }

    public override bool Equals(object obj)
    {
        return obj is fVector2 && (((fVector2)obj).x == x) && (((fVector2)obj).y == y);
    }

    public bool Equals(fVector2 other)
    {
        return (x == other.x) && (y == other.y);
    }

    public override int GetHashCode()
    {
        return x.GetHashCode() ^ y.GetHashCode() << 2;
    }
    #endregion

    #region Conversion Operators
    public static implicit operator fVector2(fVector3 v)
    {
        return new fVector2(v.x, v.y);
    }
    public static implicit operator fVector3(fVector2 v)
    {
        return new fVector3(v.x, v.y, ffloat.Zero);
    }

    public Vector2 ToUnityVector2()
    {
        return new Vector2(x.ToFloat(), y.ToFloat());
    }
    #endregion

    #region Math Functions
    public static ffloat Angle(fVector2 from, fVector2 to)
    {
        return ffloat.Acos(ffloat.Clamp(fVector2.Dot(from.normalized, to.normalized), -ffloat.One, ffloat.One)) * new ffloat(57.29578);
    }

    public static fVector2 Rotate(fVector2 vector, ffloat angle)
    {
        ffloat sin = ffloat.Sin(angle);
        ffloat cos = ffloat.Cos(angle);

        ffloat tx = vector.x;
        ffloat ty = vector.y;

        return new fVector2(cos * tx - sin * ty, sin * tx + cos * ty);
    }

    public static ffloat Distance(fVector2 lhs, fVector2 rhs)
    {
        return (lhs - rhs).magnitude;
    }

    public static ffloat Dot(fVector2 lhs, fVector2 rhs)
    {
        return lhs.x * rhs.x + lhs.y * rhs.y;
    }

    public static fVector2 Lerp(fVector2 start, fVector2 end, ffloat t)
    {
        return new fVector2(start.x + (end.x - start.x) * t, start.y + (end.y - start.y) * t);
    }

    public static fVector2 MoveTowards(fVector2 current, fVector2 target, ffloat maxDistance)
    {
        fVector2 diff = target - current;
        ffloat magnitude = diff.magnitude;

        if (magnitude <= maxDistance)
            return target;

        return current + diff / magnitude * maxDistance;
    }

    public static fVector2 Reflect(fVector2 direction, fVector2 normal)
    {
        return -2 * fVector2.Dot(normal, direction) * normal + direction;
    }
    #endregion
}
