#define FFLOAT_LUT_CREATED
//#define FFLOAT_FRACTION_8BIT
#define FFLOAT_FRACTION_12BIT
//#define FFLOAT_FRACTION_16BIT

using System;
using System.Runtime.InteropServices;

#if FMATH_ENABLE_NATIVE_PLUGIN
#if UNITY_IPHONE
    using fMathStaticLib;
#elif UNITY_ANDROID
    using fMathDynamicLib;
#endif
#endif

// 32bit Q23.8 fixed-point float
public partial struct ffloat : IEquatable<ffloat>, IComparable<ffloat>
{
    int _rawValue;
    public int RawValue { get { return _rawValue; } }

    #region Defines
    const int MAX_VALUE = int.MaxValue;
    const int MIN_VALUE = int.MinValue;
    const int NUM_BITS = 32;

#if FFLOAT_FRACTION_8BIT
    const int FRACTIONAL_PLACES = 8;
    const uint UPPER_MASK = 0xFFFFFF00;
    const uint LOWER_MASK = 0x000000FF;

    const int PI = 0x0000032F;          // 3.14159274 << 8 = 804.24774144
    const int PI_DOUBLE = 0x00000648;   // 6.28318531 << 8 = 1608.49543936
    const int PI_HALF = 0x00000192;     // 1.57079633 << 8 = 402.12386048
    const int DEG2RAD = 0x00000004;     // 0.0174532924 << 8 = 4.4680428544
    const int RAD2DEG = 0x0000394B;     // 57.29578 << 8 = 14667.71968
#elif FFLOAT_FRACTION_12BIT
    const int FRACTIONAL_PLACES = 12;
    const uint UPPER_MASK = 0xFFFFF000;
    const uint LOWER_MASK = 0x00000FFF;

    const int PI = 0x00003244;          // 3.14159274 << 12 = 12867.96386304
    const int PI_DOUBLE = 0x00006488;   // 6.28318531 << 12 = 25735.92702976
    const int PI_HALF = 0x00001922;     // 1.57079633 << 12 = 6433.98176768
    const int DEG2RAD = 0x00000047;     // 0.0174532924 << 12 = 71.4886856704
    const int RAD2DEG = 0x000394BC;     // 57.29578 << 12 = 234683.51488
#elif FFLOAT_FRACTION_16BIT
    const int FRACTIONAL_PLACES = 16;
    const uint UPPER_MASK = 0xFFFF0000;
    const uint LOWER_MASK = 0x0000FFFF;

    const int PI = 0x0003243F;          // 3.14159274 << 16 = 205887.42180864
    const int PI_DOUBLE = 0x0006487F;   // 6.28318531 << 16 = 411774.83247616
    const int PI_HALF = 0x00019220;     // 1.57079633 << 16 = 102943.70828288
    const int DEG2RAD = 0x00000478;     // 0.0174532924 << 16 = 1143.8189707264
    const int RAD2DEG = 0x00394BB8;     // 57.29578 << 16 = 3754936.23808   
#endif
    const int ONE = 1 << FRACTIONAL_PLACES;
    const int HALF = ONE >> 1;

    public static readonly ffloat MaxValue = CreateFromRawValue(MAX_VALUE);
    public static readonly ffloat MinValue = CreateFromRawValue(MIN_VALUE);
    public static readonly ffloat Zero = new ffloat();
    public static readonly ffloat One = CreateFromRawValue(ONE);
    public static readonly ffloat Half = CreateFromRawValue(HALF);
    public static readonly ffloat Epsilon = CreateFromRawValue(1);

    public static readonly ffloat Pi = CreateFromRawValue(PI);
    public static readonly ffloat DoublePi = CreateFromRawValue(PI_DOUBLE);
    public static readonly ffloat HalfPi = CreateFromRawValue(PI_HALF);
    public static readonly ffloat Deg2Rad = CreateFromRawValue(DEG2RAD);
    public static readonly ffloat Rad2Deg = CreateFromRawValue(RAD2DEG);
    #endregion

    #region Sin, Tangent LookUpTable
    const int LUT_SCALE = 4;
    const int LUT_SIZE = (int)(PI_HALF >> LUT_SCALE);
    static readonly ffloat LutInterval = new ffloat(LUT_SIZE - 1) / HalfPi;

#if !FFLOAT_LUT_CREATED
    static int[] _sinLut;
    static int[] _tanLut;

    public static void CreateLookUpTable()
    {
        _sinLut = new int[LUT_SIZE];
        _tanLut = new int[LUT_SIZE];
    
        double interval = Math.PI * 0.5 / (LUT_SIZE - 1);
        for (int i = 0; i < LUT_SIZE; i++) {
            double angle = i * interval;

            // sin
            double sin = Math.Sin(angle);
            int sinValue = ((ffloat)sin)._rawValue;
            _sinLut[i] = sinValue;

            // tan
            double tan = Math.Tan(angle);
            if (tan < (double)MaxValue || tan >= 0.0) {
                _tanLut[i] = ((ffloat)tan)._rawValue;
            }
            else {
                _tanLut[i] = MAX_VALUE - 1;
            }
        }
    }
#endif

    internal static void GenerateLookUpTable()
    {
        using (var writer = new System.IO.StreamWriter("ffloat_sinLut.cs"))
        {
            double interval = Math.PI * 0.5 / (LUT_SIZE - 1);

            writer.Write(
@"partial struct ffloat {
    public static readonly int[] _sinLut = new int[] {");
            int lineCounter = 0;
            for (int i = 0; i < LUT_SIZE; ++i)
            {
                double angle = i * interval;
                if (lineCounter++ % 8 == 0)
                {
                    writer.WriteLine();
                    writer.Write("        ");
                }

                double sin = Math.Sin(angle);
                int rawValue = ((ffloat)sin)._rawValue;
                writer.Write(string.Format("0x{0:X}, ", rawValue));
            }
            writer.Write(
@"
    };
}");
        }

        using (var writer = new System.IO.StreamWriter("ffloat_tanLut.cs"))
        {
            double interval = Math.PI * 0.5 / (LUT_SIZE - 1);

            writer.Write(
@"partial struct ffloat {
    public static readonly int[] _tanLut = new int[] {");
            int lineCounter = 0;
            for (int i = 0; i < LUT_SIZE; ++i)
            {
                double angle = i * interval;
                if (lineCounter++ % 8 == 0)
                {
                    writer.WriteLine();
                    writer.Write("        ");
                }

                double tan = Math.Tan(angle);
                int rawValue = MAX_VALUE;
                if (tan < (double)MaxValue || tan >= 0.0)
                {
                    rawValue = ((ffloat)tan)._rawValue;
                }
                writer.Write(string.Format("0x{0:X}, ", rawValue));
            }
            writer.Write(
@"
    };
}");
        }
    }
    #endregion

    #region Constructors
    public ffloat(int intValue)
    {
        _rawValue = intValue << FRACTIONAL_PLACES;
    }

    public ffloat(float floatValue)
    {
#if FMATH_ENABLE_NATIVE_PLUGIN && !UNITY_EDITOR
        _rawValue = ffloat_fromFloat(floatValue);
#else
        int sign = (floatValue < 0) ? -1 : 1;
        if (floatValue < 0)
            floatValue *= -1;

        floatValue *= ONE;
        int integralPart = (int)floatValue;
        integralPart = (int)((uint)integralPart & UPPER_MASK);

        floatValue -= integralPart;
        floatValue = floatValue * 4;
        int choppedValue = (int)floatValue;
        choppedValue = choppedValue >> 2;
        choppedValue = (int)((uint)choppedValue & LOWER_MASK);

        _rawValue = (integralPart + choppedValue) * sign;
#endif
    }

    public ffloat(double doubleValue)
    {
        int sign = (doubleValue < 0) ? -1 : 1;
        if (doubleValue < 0)
            doubleValue *= -1;

        doubleValue *= ONE;
        int integralPart = (int)doubleValue;
        integralPart = (int)((uint)integralPart & UPPER_MASK);

        doubleValue -= integralPart;
        doubleValue = doubleValue * 4;
        int choppedValue = (int)doubleValue;
        choppedValue = choppedValue >> 2;
        choppedValue = (int)((uint)choppedValue & LOWER_MASK);

        _rawValue = (integralPart + choppedValue) * sign;
    }

    public static ffloat CreateFromRawValue(int rawValue)
    {
        ffloat result = new ffloat
        {
            _rawValue = rawValue
        };

        return result;
    }
    #endregion

    public override string ToString()
    {
        return String.Format("{0:F3}", new object[] { ToFloat() });
    }

    #region Arithmetic Operators
    public static ffloat operator +(ffloat lhs, ffloat rhs) => CreateFromRawValue(lhs._rawValue + rhs._rawValue);
    public static ffloat operator +(ffloat lhs, int rhs) => lhs + (ffloat)rhs;
    public static ffloat operator +(int lhs, ffloat rhs) => (ffloat)lhs + rhs;
    public static ffloat operator +(ffloat lhs, float rhs) => lhs + (ffloat)rhs;
    public static ffloat operator +(float lhs, ffloat rhs) => (ffloat)lhs + rhs;
    public static ffloat operator -(ffloat x) => x._rawValue == MIN_VALUE ? MaxValue : CreateFromRawValue(-x._rawValue);
    public static ffloat operator -(ffloat lhs, ffloat rhs) => CreateFromRawValue(lhs._rawValue - rhs._rawValue);
    public static ffloat operator -(ffloat lhs, int rhs) => lhs - (ffloat)rhs;
    public static ffloat operator -(int lhs, ffloat rhs) => (ffloat)lhs - rhs;
    public static ffloat operator -(ffloat lhs, float rhs) => lhs - (ffloat)rhs;
    public static ffloat operator -(float lhs, ffloat rhs) => (ffloat)lhs - rhs;
    public static ffloat operator *(ffloat lhs, ffloat rhs)
    {
#if FMATH_ENABLE_NATIVE_PLUGIN && !UNITY_EDITOR
        return ffloat_mul(lhs._rawValue, rhs._rawValue);
#else
        int xi = lhs._rawValue;
        int yi = rhs._rawValue;

        uint xlo = (uint)(xi & LOWER_MASK);
        int xhi = xi >> FRACTIONAL_PLACES;
        uint ylo = (uint)(yi & LOWER_MASK);
        int yhi = yi >> FRACTIONAL_PLACES;

        int lolo = (int)((xlo * ylo) >> FRACTIONAL_PLACES);
        int lohi = (int)xlo * yhi;
        int hilo = xhi * (int)ylo;
        int hihi = (xhi * yhi) << FRACTIONAL_PLACES;

        int sum = lolo + lohi + hilo + hihi;

        return CreateFromRawValue(sum);
#endif
    }
    public static ffloat operator *(ffloat lhs, int rhs) => lhs * (ffloat)rhs;
    public static ffloat operator *(int lhs, ffloat rhs) => (ffloat)lhs * rhs;
    public static ffloat operator *(ffloat lhs, float rhs) => lhs * (ffloat)rhs;
    public static ffloat operator *(float lhs, ffloat rhs) => (ffloat)lhs * rhs;

    static int CountLeadingZeroes(uint x)
    {
        int result = 0;

        while ((x & 0xF0000000) == 0)
        {
            result += 4;
            x <<= 4;
        }
        while ((x & 0x80000000) == 0)
        {
            result++;
            x <<= 1;
        }

        return result;
    }

    public static ffloat operator /(ffloat lhs, ffloat rhs)
    {
#if FMATH_ENABLE_NATIVE_PLUGIN && !UNITY_EDITOR
        return ffloat_div(lhs._rawValue, rhs._rawValue);
#else
        int xi = lhs._rawValue;
        int yi = rhs._rawValue;

        if (yi == 0)
            return MaxValue;

        uint remainder = (uint)(xi >= 0 ? xi : -xi);
        uint divider = (uint)(yi >= 0 ? yi : -yi);
        uint quotient = 0;
        int bitPos = (FRACTIONAL_PLACES) + 1;

        while ((divider & 0xF) == 0 && bitPos >= 4)
        {
            divider >>= 4;
            bitPos -= 4;
        }

        while (remainder != 0 && bitPos >= 0)
        {
            int shift = CountLeadingZeroes(remainder);
            if (shift > bitPos)
                shift = bitPos;

            remainder <<= shift;
            bitPos -= shift;

            uint div = remainder / divider;
            remainder = remainder % divider;
            quotient += div << bitPos;

            if ((div & ~(0xFFFFFFFF >> bitPos)) != 0)
            {
                return ((xi ^ yi) & MIN_VALUE) == 0 ? MaxValue : MinValue;
            }

            remainder <<= 1;
            bitPos--;
        }

        quotient++;

        int result = (int)(quotient >> 1);
        if (((xi ^ yi) & MIN_VALUE) != 0)
        {
            result = -result;
        }

        return CreateFromRawValue(result);
#endif
    }
    public static ffloat operator /(ffloat lhs, int rhs) => lhs / (ffloat)rhs;
    public static ffloat operator /(int lhs, ffloat rhs) => (ffloat)lhs / rhs;
    public static ffloat operator %(ffloat lhs, ffloat rhs) => CreateFromRawValue(lhs._rawValue % rhs._rawValue);
    public static ffloat operator <<(ffloat x, int amount) => CreateFromRawValue(x._rawValue << amount);
    public static ffloat operator >>(ffloat x, int amount) => CreateFromRawValue(x._rawValue >> amount);
    #endregion

    #region Comparison Operators
    public static bool operator ==(ffloat lhs, ffloat rhs) => lhs._rawValue == rhs._rawValue;
    public static bool operator !=(ffloat lhs, ffloat rhs) => lhs._rawValue != rhs._rawValue;
    public static bool operator >(ffloat lhs, ffloat rhs) => lhs._rawValue > rhs._rawValue;
    public static bool operator >(ffloat lhs, int rhs) => lhs._rawValue > (rhs * ONE);
    public static bool operator >(int lhs, ffloat rhs) => (lhs * ONE) > rhs._rawValue;
    public static bool operator <(ffloat lhs, ffloat rhs) => lhs._rawValue < rhs._rawValue;
    public static bool operator <(ffloat lhs, int rhs) => lhs._rawValue < (rhs * ONE);
    public static bool operator <(int lhs, ffloat rhs) => (lhs * ONE) < rhs._rawValue;
    public static bool operator >=(ffloat lhs, ffloat rhs) => lhs._rawValue >= rhs._rawValue;
    public static bool operator >=(ffloat lhs, int rhs) => lhs._rawValue >= (rhs * ONE);
    public static bool operator >=(int lhs, ffloat rhs) => (lhs * ONE) >= rhs._rawValue;
    public static bool operator <=(ffloat lhs, ffloat rhs) => lhs._rawValue <= rhs._rawValue;
    public static bool operator <=(ffloat lhs, int rhs) => lhs._rawValue <= (rhs * ONE);
    public static bool operator <=(int lhs, ffloat rhs) => (lhs * ONE) <= rhs._rawValue;
    public override bool Equals(object obj) => obj is ffloat && ((ffloat)obj)._rawValue == _rawValue;
    public bool Equals(ffloat other) => _rawValue == other._rawValue;
    public override int GetHashCode() => _rawValue.GetHashCode();
    public int CompareTo(ffloat other) => _rawValue.CompareTo(other._rawValue);
    #endregion

    #region Conversion Operators
    public static explicit operator int(ffloat source) => source.ToInt();
    public static explicit operator float(ffloat source) => source.ToFloat();
    public static explicit operator double(ffloat source) => source.ToDouble();
    public static explicit operator ffloat(int source) => new ffloat(source);
    public static explicit operator ffloat(float source) => new ffloat(source);
    public static explicit operator ffloat(double source) => new ffloat(source);
    public int ToInt() => (int)(_rawValue >> FRACTIONAL_PLACES);
    public float ToFloat() => (float)_rawValue / (float)ONE;
    public double ToDouble() => (double)_rawValue / (double)ONE;
    #endregion

    #region Math Functions
    public static int Sign(ffloat x) => x._rawValue < 0 ? -1 : x._rawValue > 0 ? 1 : 0;

    public static ffloat Abs(ffloat x)
    {
#if FMATH_ENABLE_NATIVE_PLUGIN && !UNITY_EDITOR
        return ffloat_abs(x._rawValue);
#else
        int mask = x._rawValue >> (NUM_BITS - 1);
        return CreateFromRawValue((x._rawValue + mask) ^ mask);
#endif
    }

    public static ffloat Floor(ffloat x)
    {
#if FMATH_ENABLE_NATIVE_PLUGIN && !UNITY_EDITOR
        return ffloat_floor(x._rawValue);
#else
        return CreateFromRawValue((int)((uint)x._rawValue & UPPER_MASK));
#endif
    }

    public static ffloat Ceiling(ffloat x)
    {
        bool hasFractionalPart = (x.RawValue & LOWER_MASK) != 0;
        return hasFractionalPart ? Floor(x) + One : x;
    }

    public static ffloat Round(ffloat x)
    {
#if FMATH_ENABLE_NATIVE_PLUGIN && !UNITY_EDITOR
        return ffloat_round(x._rawValue);
#else
        uint fractionalPart = (uint)x._rawValue & LOWER_MASK;
        ffloat integralPart = Floor(x);
        if (fractionalPart < HALF)
            return integralPart;
        if (fractionalPart > HALF)
            return integralPart + One;

        return (integralPart._rawValue & ONE) == 0 ? integralPart : integralPart + One;
#endif
    }

    public static ffloat Clamp(ffloat value, ffloat min, ffloat max)
    {
        if (value < min)
            value = min;
        else if (value > max)
            value = max;

        return value;
    }

    public static ffloat Min(ffloat lhs, ffloat rhs)
    {
        return lhs < rhs ? lhs : rhs;
    }

    public static ffloat Max(ffloat lhs, ffloat rhs)
    {
        return lhs > rhs ? lhs : rhs;
    }

    public static ffloat Sqrt(ffloat x)
    {
        int xi = x._rawValue;
        if (xi < 0)
            return Zero;

        uint num = (uint)xi;
        uint result = 0U;

        uint bit = 1U << (NUM_BITS - 2);
        while (bit > num)
            bit >>= 2;

        for (int i = 0; i < 2; i++)
        {
            while (bit != 0)
            {
                if (num >= result + bit)
                {
                    num -= result + bit;
                    result = (result >> 1) + bit;
                }
                else
                {
                    result = result >> 1;
                }
                bit >>= 2;
            }

            if (i == 0)
            {
                if (num > (1U << (FRACTIONAL_PLACES)) - 1)
                {
                    num -= result;
                    num = (num << (FRACTIONAL_PLACES)) - HALF;
                    result = (result << (FRACTIONAL_PLACES)) + HALF;
                }
                else
                {
                    num <<= (FRACTIONAL_PLACES);
                    result <<= (FRACTIONAL_PLACES);
                }

                bit = 1U << ((FRACTIONAL_PLACES) - 2);
            }
        }

        if (num > result)
        {
            result++;
        }

        return CreateFromRawValue((int)result);
    }

    // angle in radian, result is clamped to half Pi
    static int ClampSinValue(int rad, out bool flipHorizontal, out bool flipVertical)
    {
        int clamped2Pi = rad % PI_DOUBLE;
        if (rad < 0)
            clamped2Pi += PI_DOUBLE;
        flipVertical = clamped2Pi >= PI;

        int clampedPi = clamped2Pi;
        while (clampedPi >= PI)
            clampedPi -= PI;
        flipHorizontal = clampedPi >= PI_HALF;

        int clampedHalfPi = clampedPi;
        if (clampedHalfPi >= PI_HALF)
            clampedHalfPi -= PI_HALF;

        return clampedHalfPi;
    }

    static int _Sin(int rad)
    {
        bool flipHorizontal, flipVertical;
        int clamped = ClampSinValue(rad, out flipHorizontal, out flipVertical);

        int index = clamped >> LUT_SCALE;
        if (index >= LUT_SIZE)
            index = LUT_SIZE - 1;

        int nearestValue = _sinLut[flipHorizontal ? LUT_SIZE - 1 - index : index];
        return flipVertical ? -nearestValue : nearestValue;
    }

    public static ffloat Sin(ffloat rad)
    {
#if FMATH_ENABLE_NATIVE_PLUGIN && !UNITY_EDITOR
        return ffloat_sin(rad._rawValue);
#else
        return CreateFromRawValue(_Sin(rad._rawValue));
#endif
    }

    public static ffloat Cos(ffloat rad)
    {
#if FMATH_ENABLE_NATIVE_PLUGIN && !UNITY_EDITOR
        return ffloat_cos(rad._rawValue);
#else
        int xi = rad._rawValue;
        int angle = xi + (xi > 0 ? -PI - PI_HALF : PI_HALF);
        return CreateFromRawValue(_Sin(angle));
#endif
    }

    public static ffloat Tan(ffloat rad)
    {
#if FMATH_ENABLE_NATIVE_PLUGIN && !UNITY_EDITOR
        return ffloat_tan(rad._rawValue);
#else
        int clampedPi = rad._rawValue % PI;
        bool flip = false;
        if (clampedPi < 0)
        {
            clampedPi = -clampedPi;
            flip = true;
        }

        if (clampedPi > PI_HALF)
        {
            clampedPi = PI_HALF - (clampedPi - PI_HALF);
            flip = !flip;
        }

        clampedPi = clampedPi >> LUT_SCALE;

        ffloat rawIndex = CreateFromRawValue(clampedPi) * LutInterval;
        ffloat roundedIndex = Round(rawIndex);
        ffloat indexError = rawIndex - roundedIndex;
        
        if (_tanLut.Length <= roundedIndex.ToInt())
        {

        }
        ffloat nearestValue = CreateFromRawValue(_tanLut[(int)roundedIndex]);
        ffloat nearValue = CreateFromRawValue(_tanLut[(int)roundedIndex + Sign(indexError)]);

        int delta = (indexError * Abs(nearestValue - nearValue))._rawValue;
        int interpolatedValue = nearestValue._rawValue + delta;
        int finalValue = flip ? -interpolatedValue : interpolatedValue;

        return CreateFromRawValue(finalValue);
#endif
    }

    public static ffloat Asin(ffloat x)
    {
#if FMATH_ENABLE_NATIVE_PLUGIN && !UNITY_EDITOR
        return ffloat_asin(x._rawValue);
#else
        if (x > One)
            return ffloat.Zero;

        ffloat dx = x * x;        
        return -(new ffloat(-0.9391155) * x + new ffloat(0.92178415) * x * dx)
            / (1 + new ffloat(-1.28459062) * dx + new ffloat(0.29562414) * dx * dx);
#endif
    }

    public static ffloat Acos(ffloat x)
    {
#if FMATH_ENABLE_NATIVE_PLUGIN && !UNITY_EDITOR
        return ffloat_acos(x._rawValue);
#else
        if (x > One)
            return ffloat.Zero;

        ffloat dx = x * x;

        return HalfPi + (new ffloat(-0.9391155) * x + new ffloat(0.92178415) * x * dx)
            / (1 + new ffloat(-1.28459062) * dx + new ffloat(0.29562414) * dx * dx);
#endif
    }

    public static ffloat Atan(ffloat x)
    {
#if FMATH_ENABLE_NATIVE_PLUGIN && !UNITY_EDITOR
        return ffloat_atan(x._rawValue);
#else
        return Asin(x / Sqrt(One + (x * x)));
#endif
    }

    public static ffloat Atan2(ffloat y, ffloat x)
    {
#if FMATH_ENABLE_NATIVE_PLUGIN && !UNITY_EDITOR
        return ffloat_tan2(y._rawValue, x._rawValue);
#else
        if (x == Zero)
        {
            if (y > Zero)
            {
                return HalfPi;
            }
            else
            {
                return -HalfPi;
            }
        }

        if (Abs(x) > Abs(y))
        {
            ffloat z = y / x;

            if (x > Zero)
            {
                return ApproxAtan(z);
            }
            else if (y >= Zero)
            {
                return ApproxAtan(z) + Pi;
            }
            else
            {
                return ApproxAtan(z) - Pi;
            }
        }
        else
        {
            ffloat z = x / y;

            if (y > Zero)
            {
                return -ApproxAtan(z) + HalfPi;
            }
            else
            {
                return -ApproxAtan(z) - HalfPi;
            }
        }
#endif
    }

    static ffloat ApproxAtan(ffloat z)
    {
        ffloat n1 = new ffloat(0.97239411);
        ffloat n2 = new ffloat(-0.19194795);

        return (n1 + n2 * z * z) * z;
    }

    #endregion

    #region Native Plugin Interface
#if FMATH_ENABLE_NATIVE_PLUGIN
    [DllImport(fMathNativeImport.libName)]
    public static extern int ffloat_fromInt(int value);

    [DllImport(fMathNativeImport.libName)]
    public static extern int ffloat_fromFloat(float value);

    [DllImport(fMathNativeImport.libName)]
    public static extern int ffloat_mul(int lhs, int rhs);

    [DllImport(fMathNativeImport.libName)]
    public static extern int ffloat_div(int lhs, int rhs);
    
    [DllImport(fMathNativeImport.libName)]
    public static extern int ffloat_abs(int value);

    [DllImport(fMathNativeImport.libName)]
    public static extern int ffloat_floor(int value);

    [DllImport(fMathNativeImport.libName)]
    public static extern int ffloat_round(int value);

    [DllImport(fMathNativeImport.libName)]
    public static extern int ffloat_sqrt(int value);

    [DllImport(fMathNativeImport.libName)]
    public static extern int ffloat_sin(int value);

    [DllImport(fMathNativeImport.libName)]
    public static extern int ffloat_cos(int value);

    [DllImport(fMathNativeImport.libName)]
    public static extern int ffloat_tan(int value);

    [DllImport(fMathNativeImport.libName)]
    public static extern int ffloat_asin(int value);

    [DllImport(fMathNativeImport.libName)]
    public static extern int ffloat_acos(int value);

    [DllImport(fMathNativeImport.libName)]
    public static extern int ffloat_atan(int value);

    [DllImport(fMathNativeImport.libName)]
    public static extern int ffloat_atan2(int y, int x);
#endif
    #endregion
}
