using System;
using System.Globalization;

/// <summary>
/// Absolute angle stored as an unsigned 32-bit fraction of a full turn (4 bytes).
/// 0x00000000 = 0°, 0x40000000 = 90°, 0x80000000 = 180°, 0xC0000000 = 270°.
/// Addition and subtraction wrap modulo one turn, so there is no normalization step and no
/// degree/radian ambiguity. Resolution is 2π / 2^32 (~1.46e-9 rad).
/// </summary>
public readonly struct fAngle : IEquatable<fAngle>
{
    const uint RAW_QUARTER = 0x40000000u;
    const uint RAW_HALF = 0x80000000u;
    const uint RAW_THREE_QUARTER = 0xC0000000u;

    readonly uint _rawValue;

    fAngle(uint rawValue) { _rawValue = rawValue; }

    public uint RawValue => _rawValue;

    public static readonly fAngle Zero = new fAngle(0u);
    public static readonly fAngle QuarterTurn = new fAngle(RAW_QUARTER);
    public static readonly fAngle HalfTurn = new fAngle(RAW_HALF);
    public static readonly fAngle ThreeQuarterTurn = new fAngle(RAW_THREE_QUARTER);

    #region Factories
    public static fAngle FromRaw(uint rawValue) => new fAngle(rawValue);

    /// <summary>numerator / denominator turns, rounded to nearest (ties to even) and wrapped.</summary>
    public static fAngle FromTurnsFraction(int numerator, int denominator) => FromTurnsFraction((long)numerator, denominator);

    /// <summary>numerator / denominator turns; |numerator| must stay below 2^31.</summary>
    public static fAngle FromTurnsFraction(long numerator, long denominator)
    {
        if (denominator == 0)
        {
            fMathValidation.Report(fMathValidationKind.DivideByZero, "fAngle.FromTurnsFraction / 0");
            return Zero;
        }
        // reduce whole turns first so numerator << 32 cannot overflow
        numerator %= denominator;
        long raw = fWideMath.DivideRoundToEven(numerator << 32, denominator);
        return new fAngle(unchecked((uint)raw));
    }

    /// <summary>Whole degrees (exact multiples of 1/360 turn, rounded to the nearest raw step).</summary>
    public static fAngle FromDegrees(int degrees) => FromTurnsFraction(degrees, 360);

    /// <summary>numerator / denominator degrees, e.g. FromDegreesFraction(45, 2) for 22.5°.</summary>
    public static fAngle FromDegreesFraction(int numerator, int denominator) => FromTurnsFraction(numerator, (long)denominator * 360);
    #endregion

    #region Arithmetic (wrapping)
    public static fAngle operator +(fAngle a, fAngle b) => new fAngle(unchecked(a._rawValue + b._rawValue));
    public static fAngle operator +(fAngle a, fAngleDelta b) => new fAngle(unchecked(a._rawValue + (uint)b.RawValue));
    public static fAngle operator -(fAngle a, fAngleDelta b) => new fAngle(unchecked(a._rawValue - (uint)b.RawValue));
    public static fAngle operator -(fAngle a) => new fAngle(unchecked(0u - a._rawValue));

    /// <summary>Shortest signed rotation from a to b is b - a; see <see cref="SignedDeltaTo"/>.</summary>
    public static fAngleDelta operator -(fAngle b, fAngle a) => fAngleDelta.FromRaw(unchecked((int)(b._rawValue - a._rawValue)));

    public fAngle Add(fAngle other) => this + other;
    public fAngle Sub(fAngle other) => new fAngle(unchecked(_rawValue - other._rawValue));

    /// <summary>Shortest signed difference target - this in [-180°, 180°).</summary>
    public fAngleDelta SignedDeltaTo(fAngle target) => target - this;

    /// <summary>Half of this angle measured in [0, 360°), i.e. in [0, 180°).</summary>
    public fAngle Half => new fAngle(_rawValue >> 1);

    /// <summary>This angle interpreted as a signed delta in [-180°, 180°).</summary>
    public fAngleDelta ToDelta() => fAngleDelta.FromRaw(unchecked((int)_rawValue));

    /// <summary>Scales the angle by a Q16.16 factor (wrapping).</summary>
    public static fAngle operator *(fAngle a, ffloat t)
    {
        long scaled = fWideMath.RoundShiftRightToEven((long)a._rawValue * t.RawValue, ffloat.FractionalBits);
        return new fAngle(unchecked((uint)scaled));
    }

    /// <summary>Moves current towards target along the shortest arc by at most maxDelta.</summary>
    public static fAngle MoveTowards(fAngle current, fAngle target, fAngleDelta maxDelta)
    {
        fAngleDelta delta = target - current;
        return current + fAngleDelta.ClampMagnitude(delta, maxDelta);
    }
    #endregion

    #region Comparison
    public static bool operator ==(fAngle a, fAngle b) => a._rawValue == b._rawValue;
    public static bool operator !=(fAngle a, fAngle b) => a._rawValue != b._rawValue;
    public bool Equals(fAngle other) => _rawValue == other._rawValue;
    public override bool Equals(object obj) => obj is fAngle other && other._rawValue == _rawValue;
    public override int GetHashCode() => unchecked((int)_rawValue);

    /// <summary>True when the shortest distance between a and b is at most tolerance.</summary>
    public static bool Approximately(fAngle a, fAngle b, fAngleDelta tolerance)
    {
        return (b - a).AbsRaw <= tolerance.AbsRaw;
    }
    #endregion

    /// <summary>Presentation and debugging only.</summary>
    public double ToDegreesDouble() => _rawValue * (360.0 / 4294967296.0);

    public override string ToString() => ToDegreesDouble().ToString("0.#####", CultureInfo.InvariantCulture) + "°";
}

/// <summary>
/// Signed angle difference as a signed 32-bit fraction of a turn (4 bytes), range [-180°, 180°).
/// Used for shortest signed differences and per-tick angular steps. Arithmetic wraps modulo one
/// turn, consistent with <see cref="fAngle"/>.
/// </summary>
public readonly struct fAngleDelta : IEquatable<fAngleDelta>, IComparable<fAngleDelta>
{
    readonly int _rawValue;

    fAngleDelta(int rawValue) { _rawValue = rawValue; }

    public int RawValue => _rawValue;

    public static readonly fAngleDelta Zero = new fAngleDelta(0);
    public static readonly fAngleDelta QuarterTurn = new fAngleDelta(0x40000000);
    public static readonly fAngleDelta MinusQuarterTurn = new fAngleDelta(-0x40000000);
    /// <summary>-180° (the representation of a half turn; +180° wraps to it).</summary>
    public static readonly fAngleDelta HalfTurn = new fAngleDelta(int.MinValue);

    public static fAngleDelta FromRaw(int rawValue) => new fAngleDelta(rawValue);

    public static fAngleDelta FromTurnsFraction(int numerator, int denominator) => fAngle.FromTurnsFraction(numerator, denominator).ToDelta();

    public static fAngleDelta FromDegrees(int degrees) => fAngle.FromDegrees(degrees).ToDelta();

    public static fAngleDelta FromDegreesFraction(int numerator, int denominator) => fAngle.FromDegreesFraction(numerator, denominator).ToDelta();

    /// <summary>|delta| as an unsigned raw value (HalfTurn gives 2^31).</summary>
    public uint AbsRaw => _rawValue < 0 ? unchecked((uint)(-(long)_rawValue)) : (uint)_rawValue;

    public fAngle ToAngle() => fAngle.FromRaw(unchecked((uint)_rawValue));

    public static fAngleDelta operator +(fAngleDelta a, fAngleDelta b) => new fAngleDelta(unchecked(a._rawValue + b._rawValue));
    public static fAngleDelta operator -(fAngleDelta a, fAngleDelta b) => new fAngleDelta(unchecked(a._rawValue - b._rawValue));
    public static fAngleDelta operator -(fAngleDelta a) => new fAngleDelta(unchecked(0 - a._rawValue));
    public static fAngleDelta operator *(fAngleDelta a, int b) => new fAngleDelta(unchecked(a._rawValue * b));

    /// <summary>Scales by a Q16.16 factor (e.g. angular speed * dt), ties to even, wrapping.</summary>
    public static fAngleDelta operator *(fAngleDelta a, ffloat t)
    {
        long scaled = fWideMath.RoundShiftRightToEven((long)a._rawValue * t.RawValue, ffloat.FractionalBits);
        return new fAngleDelta(unchecked((int)scaled));
    }

    /// <summary>Half of the signed delta, rounded to nearest (ties to even).</summary>
    public fAngleDelta Half => new fAngleDelta((int)fWideMath.RoundShiftRightToEven(_rawValue, 1));

    /// <summary>Clamps |delta| to |maxMagnitude| keeping the sign.</summary>
    public static fAngleDelta ClampMagnitude(fAngleDelta delta, fAngleDelta maxMagnitude)
    {
        uint max = maxMagnitude.AbsRaw;
        if (delta.AbsRaw <= max)
            return delta;
        if (max >= 0x80000000u)
            return delta;
        return new fAngleDelta(delta._rawValue < 0 ? -(int)max : (int)max);
    }

    public static bool operator ==(fAngleDelta a, fAngleDelta b) => a._rawValue == b._rawValue;
    public static bool operator !=(fAngleDelta a, fAngleDelta b) => a._rawValue != b._rawValue;
    public static bool operator <(fAngleDelta a, fAngleDelta b) => a._rawValue < b._rawValue;
    public static bool operator >(fAngleDelta a, fAngleDelta b) => a._rawValue > b._rawValue;
    public static bool operator <=(fAngleDelta a, fAngleDelta b) => a._rawValue <= b._rawValue;
    public static bool operator >=(fAngleDelta a, fAngleDelta b) => a._rawValue >= b._rawValue;
    public bool Equals(fAngleDelta other) => _rawValue == other._rawValue;
    public override bool Equals(object obj) => obj is fAngleDelta other && other._rawValue == _rawValue;
    public override int GetHashCode() => _rawValue;
    public int CompareTo(fAngleDelta other) => _rawValue.CompareTo(other._rawValue);

    /// <summary>Presentation and debugging only.</summary>
    public double ToDegreesDouble() => _rawValue * (360.0 / 4294967296.0);

    public override string ToString() => ToDegreesDouble().ToString("0.#####", CultureInfo.InvariantCulture) + "°";
}
