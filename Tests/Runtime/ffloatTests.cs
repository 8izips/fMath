using System;
using System.Numerics;
using NUnit.Framework;

namespace fMath.Tests
{
    [TestFixture]
    public class ffloatTests
    {
        static ffloat R(int raw) => ffloat.FromRaw(raw);

        [SetUp]
        public void ResetValidation() => fMathValidation.ResetCounts();

        [Test]
        public void Format_IsQ16_16()
        {
            Assert.That(ffloat.One.RawValue, Is.EqualTo(65536));
            Assert.That(ffloat.Half.RawValue, Is.EqualTo(32768));
            Assert.That(ffloat.Epsilon.RawValue, Is.EqualTo(1));
            Assert.That(ffloat.FractionalBits, Is.EqualTo(16));
        }

        [Test]
        public void Constants_MatchRoundedExactValues()
        {
            Assert.That(ffloat.Pi.RawValue, Is.EqualTo((int)Math.Round(Math.PI * 65536)));
            Assert.That(ffloat.TwoPi.RawValue, Is.EqualTo((int)Math.Round(2 * Math.PI * 65536)));
            Assert.That(ffloat.HalfPi.RawValue, Is.EqualTo((int)Math.Round(0.5 * Math.PI * 65536)));
            Assert.That(ffloat.Deg2Rad.RawValue, Is.EqualTo((int)Math.Round(Math.PI / 180 * 65536)));
            Assert.That(ffloat.Rad2Deg.RawValue, Is.EqualTo((int)Math.Round(180 / Math.PI * 65536)));
        }

        [Test]
        public void Factories()
        {
            Assert.That(ffloat.FromInt(3).RawValue, Is.EqualTo(3 << 16));
            Assert.That(ffloat.FromInt(-32768).RawValue, Is.EqualTo(int.MinValue));
            Assert.That(ffloat.FromInt(32767).RawValue, Is.EqualTo(32767 << 16));
            Assert.That(ffloat.FromInt(40000), Is.EqualTo(ffloat.MaxValue));
            Assert.That(ffloat.FromInt(-40000), Is.EqualTo(ffloat.MinValue));
            Assert.That(ffloat.FromFraction(1, 3).RawValue, Is.EqualTo(21845));
            Assert.That(ffloat.FromFraction(2, 3).RawValue, Is.EqualTo(43691));
            Assert.That(ffloat.FromFraction(-1, 3).RawValue, Is.EqualTo(-21845));
            Assert.That(ffloat.FromFraction(1, 131072).RawValue, Is.EqualTo(0));   // 0.5 raw -> even
            Assert.That(ffloat.FromFraction(3, 131072).RawValue, Is.EqualTo(2));   // 1.5 raw -> even
            Assert.That(ffloat.FromFraction(1250L, 1000L).RawValue, Is.EqualTo(81920));
            Assert.That(ffloat.FromFraction(long.MaxValue, 1L), Is.EqualTo(ffloat.MaxValue));
            Assert.That(ffloat.FromRaw(-7).RawValue, Is.EqualTo(-7));
            Assert.That(((ffloat)5).RawValue, Is.EqualTo(5 << 16));
        }

        [Test]
        public void AddSub_Saturate()
        {
            Assert.That((ffloat.One + ffloat.Half).RawValue, Is.EqualTo(98304));
            Assert.That(ffloat.MaxValue + ffloat.Epsilon, Is.EqualTo(ffloat.MaxValue));
            Assert.That(ffloat.MinValue - ffloat.Epsilon, Is.EqualTo(ffloat.MinValue));
            Assert.That(-ffloat.MinValue, Is.EqualTo(ffloat.MaxValue));
            Assert.That((ffloat.One + 2).RawValue, Is.EqualTo(3 << 16));
            Assert.That((5 - ffloat.One).RawValue, Is.EqualTo(4 << 16));
            Assert.That(ffloat.MaxValue + 1, Is.EqualTo(ffloat.MaxValue));
            if (fMathValidation.IsEnabled)
                Assert.That(fMathValidation.GetCount(fMathValidationKind.Saturation), Is.GreaterThanOrEqualTo(4));
        }

        [Test]
        public void Multiply_MatchesExactReference()
        {
            var rng = new SplitMix64(11);
            for (int i = 0; i < 50000; i++)
            {
                int a = (int)rng.Next() >> (int)(rng.Next() % 31);
                int b = (int)rng.Next() >> (int)(rng.Next() % 31);
                BigInteger expected = Clamp(fWideMathTests.RoundDivEven((BigInteger)a * b, 65536));
                Assert.That((BigInteger)(R(a) * R(b)).RawValue, Is.EqualTo(expected), $"{a}*{b}");
            }
            Assert.That((ffloat.FromInt(3) * ffloat.FromFraction(1, 2)).RawValue, Is.EqualTo(3 << 15));
            Assert.That(R(-1) * ffloat.Half, Is.EqualTo(ffloat.Zero)); // -0.5 raw -> 0
            Assert.That(R(-3) * ffloat.Half, Is.EqualTo(R(-2)));      // -1.5 raw -> -2
            Assert.That(ffloat.FromInt(300) * ffloat.FromInt(300), Is.EqualTo(ffloat.MaxValue));
            Assert.That(ffloat.FromInt(-300) * ffloat.FromInt(300), Is.EqualTo(ffloat.MinValue));
            Assert.That((ffloat.FromInt(7) * 3).RawValue, Is.EqualTo(21 << 16));
        }

        [Test]
        public void Divide_MatchesExactReference()
        {
            var rng = new SplitMix64(12);
            for (int i = 0; i < 50000; i++)
            {
                int a = (int)rng.Next() >> (int)(rng.Next() % 31);
                int b = (int)rng.Next() >> (int)(rng.Next() % 31);
                if (b == 0) continue;
                BigInteger expected = Clamp(fWideMathTests.RoundDivEven((BigInteger)a << 16, b));
                Assert.That((BigInteger)(R(a) / R(b)).RawValue, Is.EqualTo(expected), $"{a}/{b}");
            }
            Assert.That(ffloat.One / ffloat.FromInt(3), Is.EqualTo(ffloat.FromFraction(1, 3)));
            Assert.That(ffloat.FromInt(-7) / ffloat.FromInt(2), Is.EqualTo(ffloat.FromFraction(-7, 2)));
            Assert.That((ffloat.FromInt(7) / 2).RawValue, Is.EqualTo(7 << 15));
        }

        [Test]
        public void Divide_ZeroContract()
        {
            Assert.That(ffloat.One / ffloat.Zero, Is.EqualTo(ffloat.MaxValue));
            Assert.That(-ffloat.One / ffloat.Zero, Is.EqualTo(ffloat.MinValue));
            Assert.That(ffloat.Zero / ffloat.Zero, Is.EqualTo(ffloat.Zero));
            Assert.That(ffloat.TryDivide(ffloat.One, ffloat.Zero, out ffloat r), Is.False);
            Assert.That(r, Is.EqualTo(ffloat.MaxValue));
            Assert.That(ffloat.TryDivide(ffloat.MaxValue, ffloat.Epsilon, out r), Is.False);
            Assert.That(r, Is.EqualTo(ffloat.MaxValue));
            Assert.That(ffloat.TryDivide(ffloat.One, ffloat.Two, out r), Is.True);
            Assert.That(r, Is.EqualTo(ffloat.Half));
            if (fMathValidation.IsEnabled)
                Assert.That(fMathValidation.GetCount(fMathValidationKind.DivideByZero), Is.EqualTo(3));
        }

        [Test]
        public void Modulo()
        {
            Assert.That(ffloat.FromFraction(7, 2) % ffloat.One, Is.EqualTo(ffloat.Half));
            Assert.That(ffloat.FromFraction(-7, 2) % ffloat.One, Is.EqualTo(-ffloat.Half));
            Assert.That(ffloat.One % ffloat.Zero, Is.EqualTo(ffloat.Zero));
            Assert.That(ffloat.MinValue % R(-1), Is.EqualTo(ffloat.Zero));
        }

        [Test]
        public void MulDiv_SingleRounding()
        {
            // 300 * 300 / 600 = 150 without intermediate overflow
            Assert.That(ffloat.MulDiv(ffloat.FromInt(300), ffloat.FromInt(300), ffloat.FromInt(600)), Is.EqualTo(ffloat.FromInt(150)));
        }

        [Test]
        public void Abs_Sign()
        {
            Assert.That(ffloat.Abs(ffloat.MinValue), Is.EqualTo(ffloat.MaxValue));
            Assert.That(ffloat.Abs(-ffloat.Half), Is.EqualTo(ffloat.Half));
            Assert.That(ffloat.Abs(ffloat.Zero), Is.EqualTo(ffloat.Zero));
            Assert.That(ffloat.Sign(-ffloat.Epsilon), Is.EqualTo(-1));
            Assert.That(ffloat.Sign(ffloat.Zero), Is.EqualTo(0));
        }

        [TestCase(1.5, 1, 2, 2)]
        [TestCase(2.5, 2, 3, 2)]
        [TestCase(3.5, 3, 4, 4)]
        [TestCase(-1.5, -2, -1, -2)]
        [TestCase(-2.5, -3, -2, -2)]
        [TestCase(-0.25, -1, 0, 0)]
        [TestCase(-3.0, -3, -3, -3)]
        [TestCase(0.75, 0, 1, 1)]
        public void FloorCeilRound(double value, int floor, int ceil, int round)
        {
            ffloat x = R((int)(value * 65536));
            Assert.That(ffloat.Floor(x), Is.EqualTo(ffloat.FromInt(floor)));
            Assert.That(ffloat.Ceiling(x), Is.EqualTo(ffloat.FromInt(ceil)));
            Assert.That(ffloat.Round(x), Is.EqualTo(ffloat.FromInt(round)));
            Assert.That(x.FloorToInt(), Is.EqualTo(floor));
            Assert.That(x.CeilToInt(), Is.EqualTo(ceil));
            Assert.That(x.RoundToInt(), Is.EqualTo(round));
        }

        [Test]
        public void Ceiling_Saturates()
        {
            Assert.That(ffloat.Ceiling(ffloat.MaxValue), Is.EqualTo(ffloat.MaxValue));
            Assert.That(ffloat.Round(ffloat.MaxValue), Is.EqualTo(ffloat.MaxValue));
            Assert.That(ffloat.Floor(ffloat.MinValue), Is.EqualTo(ffloat.MinValue));
        }

        [Test]
        public void MinMaxClampLerp()
        {
            Assert.That(ffloat.Min(ffloat.One, ffloat.Half), Is.EqualTo(ffloat.Half));
            Assert.That(ffloat.Max(ffloat.One, ffloat.Half), Is.EqualTo(ffloat.One));
            Assert.That(ffloat.Clamp(ffloat.Two, ffloat.Zero, ffloat.One), Is.EqualTo(ffloat.One));
            Assert.That(ffloat.Clamp(-ffloat.Two, ffloat.Zero, ffloat.One), Is.EqualTo(ffloat.Zero));
            Assert.That(ffloat.Lerp(ffloat.Zero, ffloat.FromInt(10), ffloat.Half), Is.EqualTo(ffloat.FromInt(5)));
            Assert.That(ffloat.Lerp(ffloat.Zero, ffloat.FromInt(10), ffloat.Two), Is.EqualTo(ffloat.FromInt(10)));
            // difference of extreme values does not overflow
            Assert.That(ffloat.Lerp(ffloat.MinValue, ffloat.MaxValue, ffloat.Half).RawValue, Is.EqualTo(0));
            Assert.That(ffloat.MoveTowards(ffloat.Zero, ffloat.FromInt(10), ffloat.One), Is.EqualTo(ffloat.One));
            Assert.That(ffloat.MoveTowards(ffloat.Zero, ffloat.Half, ffloat.One), Is.EqualTo(ffloat.Half));
        }

        [Test]
        public void EqualityIsRawExact()
        {
            Assert.That(R(1) == R(1), Is.True);
            Assert.That(R(1) == R(2), Is.False);
            Assert.That(R(1).Equals(R(1)), Is.True);
            Assert.That(ffloat.Approximately(R(10), R(12), R(2)), Is.True);
            Assert.That(ffloat.Approximately(R(10), R(13), R(2)), Is.False);
            Assert.That(ffloat.Approximately(ffloat.MinValue, ffloat.MaxValue, ffloat.MaxValue), Is.False);
            Assert.That(ffloat.FromInt(2) > 1, Is.True);
            Assert.That(1 < ffloat.FromInt(2), Is.True);
            Assert.That(ffloat.FromInt(2) <= 2, Is.True);
        }

        [Test]
        public void Sqrt_KnownValues()
        {
            Assert.That(ffloat.Sqrt(ffloat.Zero), Is.EqualTo(ffloat.Zero));
            Assert.That(ffloat.Sqrt(ffloat.One), Is.EqualTo(ffloat.One));
            Assert.That(ffloat.Sqrt(ffloat.FromInt(4)), Is.EqualTo(ffloat.FromInt(2)));
            Assert.That(ffloat.Sqrt(ffloat.FromInt(2)).RawValue, Is.EqualTo(92682)); // 1.41421356 * 65536 = 92681.9
            for (int n = 0; n <= 181; n++)
                Assert.That(ffloat.Sqrt(ffloat.FromInt(n * n)), Is.EqualTo(ffloat.FromInt(n)), $"perfect square {n}");
            Assert.That(ffloat.Sqrt(ffloat.Epsilon).RawValue, Is.EqualTo(256)); // sqrt(2^-16) = 2^-8
            Assert.That(ffloat.Sqrt(ffloat.MaxValue).RawValue, Is.EqualTo(11863283)); // sqrt(32768) * 65536
        }

        [Test]
        public void Sqrt_IsRoundedToNearest()
        {
            var rng = new SplitMix64(13);
            for (int i = 0; i < 50000; i++)
            {
                int raw = (int)(rng.Next() >> 33) >> (int)(rng.Next() % 31);
                int s = ffloat.Sqrt(R(raw)).RawValue;
                BigInteger v = (BigInteger)raw << 16;
                BigInteger lo = 2 * (BigInteger)s - 1, hi = 2 * (BigInteger)s + 1;
                Assert.That(lo * lo <= 4 * v || s == 0, Is.True, raw.ToString());
                Assert.That(hi * hi >= 4 * v, Is.True, raw.ToString());
            }
        }

        [Test]
        public void Sqrt_IsMonotonic_SmallRangeAndStrides()
        {
            int prev = 0;
            for (int raw = 0; raw < (1 << 21); raw++)
            {
                int s = ffloat.Sqrt(R(raw)).RawValue;
                Assert.That(s >= prev, Is.True, raw.ToString());
                prev = s;
            }
            prev = 0;
            for (long raw = 0; raw <= int.MaxValue; raw += 997)
            {
                int s = ffloat.Sqrt(R((int)raw)).RawValue;
                Assert.That(s >= prev, Is.True, raw.ToString());
                prev = s;
            }
        }

        [Test]
        public void Sqrt_Negative()
        {
            Assert.That(ffloat.TrySqrt(-ffloat.One, out ffloat r), Is.False);
            Assert.That(r, Is.EqualTo(ffloat.Zero));
            Assert.That(ffloat.Sqrt(-ffloat.One), Is.EqualTo(ffloat.Zero));
            if (fMathValidation.IsEnabled)
                Assert.That(fMathValidation.GetCount(fMathValidationKind.NegativeSqrt), Is.EqualTo(1));
        }

        [Test]
        public void SqrtWide()
        {
            // 2048^2 * 3 in Q32
            ulong q32 = 3UL * (2048UL << 16) * (2048UL << 16);
            Assert.That(ffloat.SqrtWide(q32).RawValue, Is.EqualTo((int)Math.Round(Math.Sqrt(3) * 2048 * 65536)));
            Assert.That(ffloat.SqrtWide(ulong.MaxValue), Is.EqualTo(ffloat.MaxValue));
        }

        [Test]
        public void ToStringIsInvariant()
        {
            Assert.That(ffloat.FromFraction(-3, 2).ToString(), Is.EqualTo("-1.5"));
            Assert.That(ffloat.Half.ToDouble(), Is.EqualTo(0.5));
        }

        static BigInteger Clamp(BigInteger v)
        {
            if (v > int.MaxValue) return int.MaxValue;
            if (v < int.MinValue) return int.MinValue;
            return v;
        }
    }
}
