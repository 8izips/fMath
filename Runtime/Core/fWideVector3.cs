using System;

/// <summary>
/// Three 64-bit components holding an exact intermediate result, e.g. a cross product of two
/// fVector3 (Q32.32) or of two fUnitVector3 (Q2.60). The fixed-point scale is defined by the
/// producing API. It is never part of stored state; reduce it with <see cref="TryNormalize"/> or a
/// scale-specific conversion.
/// </summary>
public readonly struct fWideVector3 : IEquatable<fWideVector3>
{
    public readonly long x;
    public readonly long y;
    public readonly long z;

    public fWideVector3(long x, long y, long z)
    {
        this.x = x;
        this.y = y;
        this.z = z;
    }

    public bool IsZero => x == 0 && y == 0 && z == 0;

    /// <summary>Direction of this vector in Q1.30 (scale-invariant). False only for the zero vector.</summary>
    public bool TryNormalize(out fUnitVector3 result)
    {
        bool ok = fWideMath.TryNormalizeQ30(x, y, z, 0, out int ux, out int uy, out int uz, out _);
        result = fUnitVector3.FromRawUnchecked(ux, uy, uz);
        return ok;
    }

    /// <summary>Interprets the components as Q32.32 and rounds them to a Q16.16 vector.</summary>
    public fVector3 ToVectorFromQ32()
    {
        return fVector3.FromRaw(
            fWideMath.SaturateToInt(fWideMath.RoundShiftRightToEven(x, ffloat.FractionalBits)),
            fWideMath.SaturateToInt(fWideMath.RoundShiftRightToEven(y, ffloat.FractionalBits)),
            fWideMath.SaturateToInt(fWideMath.RoundShiftRightToEven(z, ffloat.FractionalBits)));
    }

    public static bool operator ==(fWideVector3 a, fWideVector3 b) => a.x == b.x && a.y == b.y && a.z == b.z;
    public static bool operator !=(fWideVector3 a, fWideVector3 b) => !(a == b);
    public bool Equals(fWideVector3 other) => this == other;
    public override bool Equals(object obj) => obj is fWideVector3 other && this == other;
    public override int GetHashCode() => unchecked((x.GetHashCode() * 397 ^ y.GetHashCode()) * 397 ^ z.GetHashCode());
    public override string ToString() => "(" + x + ", " + y + ", " + z + ")";
}
