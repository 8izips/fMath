using System.Numerics;
using NUnit.Framework;

namespace fMath.Tests
{
    [TestFixture]
    public class fWideMathTests
    {
        // Reference: round-to-nearest ties-to-even of n/d using exact BigInteger arithmetic.
        internal static BigInteger RoundDivEven(BigInteger n, BigInteger d)
        {
            if (d.Sign < 0) { n = -n; d = -d; }
            BigInteger q = BigInteger.DivRem(n, d, out BigInteger r);
            if (r.Sign < 0) { q -= 1; r += d; } // floor division
            BigInteger twice = r * 2;
            if (twice > d || (twice == d && !q.IsEven))
                q += 1;
            return q;
        }

        [TestCase(0L, 4, 0L)]
        [TestCase(8L, 4, 0L)]       // 0.5 -> 0 (even)
        [TestCase(24L, 4, 2L)]      // 1.5 -> 2 (even)
        [TestCase(40L, 4, 2L)]      // 2.5 -> 2 (even)
        [TestCase(-8L, 4, 0L)]      // -0.5 -> 0
        [TestCase(-24L, 4, -2L)]    // -1.5 -> -2
        [TestCase(-40L, 4, -2L)]    // -2.5 -> -2
        [TestCase(9L, 4, 1L)]       // 0.5625 -> 1
        [TestCase(-9L, 4, -1L)]
        [TestCase(7L, 4, 0L)]       // 0.4375 -> 0
        [TestCase(-7L, 4, 0L)]
        public void RoundShiftRightToEven_Ties(long value, int shift, long expected)
        {
            Assert.That(fWideMath.RoundShiftRightToEven(value, shift), Is.EqualTo(expected));
        }

        [Test]
        public void RoundShiftRightToEven_IsSymmetricAndMatchesReference()
        {
            var rng = new SplitMix64(1);
            for (int i = 0; i < 20000; i++)
            {
                long v = (long)rng.Next() >> (int)(rng.Next() % 40);
                int shift = 1 + (int)(rng.Next() % 40);
                long r = fWideMath.RoundShiftRightToEven(v, shift);
                Assert.That(fWideMath.RoundShiftRightToEven(-v, shift), Is.EqualTo(-r));
                Assert.That((BigInteger)r, Is.EqualTo(RoundDivEven(v, BigInteger.One << shift)));
            }
        }

        [Test]
        public void RoundShiftRightToEven_Extremes()
        {
            Assert.That(fWideMath.RoundShiftRightToEven(long.MaxValue, 62), Is.EqualTo(2L));
            Assert.That(fWideMath.RoundShiftRightToEven(long.MinValue, 62), Is.EqualTo(-2L));
            Assert.That(fWideMath.RoundShiftRightToEven(long.MinValue, 1), Is.EqualTo(long.MinValue / 2));
        }

        [Test]
        public void DivideRoundToEven_MatchesReference()
        {
            var rng = new SplitMix64(2);
            for (int i = 0; i < 20000; i++)
            {
                long n = (long)rng.Next() >> (int)(rng.Next() % 50);
                long d = (long)rng.Next() >> (int)(8 + rng.Next() % 55);
                if (d == 0) d = 3;
                Assert.That((BigInteger)fWideMath.DivideRoundToEven(n, d), Is.EqualTo(RoundDivEven(n, d)), $"{n}/{d}");
                Assert.That(fWideMath.DivideRoundToEven(-n, d), Is.EqualTo(-fWideMath.DivideRoundToEven(n, d)));
            }
        }

        [Test]
        public void DivideRoundToEven_TiesAndBoundaries()
        {
            Assert.That(fWideMath.DivideRoundToEven(5, 2), Is.EqualTo(2L));
            Assert.That(fWideMath.DivideRoundToEven(7, 2), Is.EqualTo(4L));
            Assert.That(fWideMath.DivideRoundToEven(-5, 2), Is.EqualTo(-2L));
            Assert.That(fWideMath.DivideRoundToEven(-7, 2), Is.EqualTo(-4L));
            Assert.That(fWideMath.DivideRoundToEven(5, -2), Is.EqualTo(-2L));
            Assert.That(fWideMath.DivideRoundToEven(long.MinValue, 1), Is.EqualTo(long.MinValue));
            Assert.That(fWideMath.DivideRoundToEven(long.MinValue, -1), Is.EqualTo(long.MaxValue));
            Assert.That(fWideMath.DivideRoundToEven(long.MaxValue, -1), Is.EqualTo(-long.MaxValue));
            Assert.That(fWideMath.DivideRoundToEven(10, 0), Is.EqualTo(long.MaxValue));
            Assert.That(fWideMath.DivideRoundToEven(-10, 0), Is.EqualTo(long.MinValue));
            Assert.That(fWideMath.DivideRoundToEven(0, 0), Is.EqualTo(0L));
        }

        [Test]
        public void SaturateToInt_Boundaries()
        {
            Assert.That(fWideMath.SaturateToInt((long)int.MaxValue), Is.EqualTo(int.MaxValue));
            Assert.That(fWideMath.SaturateToInt((long)int.MaxValue + 1), Is.EqualTo(int.MaxValue));
            Assert.That(fWideMath.SaturateToInt((long)int.MinValue), Is.EqualTo(int.MinValue));
            Assert.That(fWideMath.SaturateToInt((long)int.MinValue - 1), Is.EqualTo(int.MinValue));
            Assert.That(fWideMath.SaturateToInt(long.MaxValue), Is.EqualTo(int.MaxValue));
            Assert.That(fWideMath.SaturateToInt(long.MinValue), Is.EqualTo(int.MinValue));
        }

        [Test]
        public void AbsToLong_HandlesMinValue()
        {
            Assert.That(fWideMath.AbsToLong(int.MinValue), Is.EqualTo(2147483648L));
            Assert.That(fWideMath.AbsToULong(long.MinValue), Is.EqualTo(9223372036854775808UL));
            Assert.That(fWideMath.AbsToLong(-5), Is.EqualTo(5L));
        }

        [Test]
        public void AddSaturating_Long()
        {
            Assert.That(fWideMath.AddSaturating(long.MaxValue, 1L), Is.EqualTo(long.MaxValue));
            Assert.That(fWideMath.AddSaturating(long.MinValue, -1L), Is.EqualTo(long.MinValue));
            Assert.That(fWideMath.AddSaturating(5L, -7L), Is.EqualTo(-2L));
        }

        [Test]
        public void IntegerSqrt_ExactFloor()
        {
            ulong[] samples = { 0, 1, 2, 3, 4, 15, 16, 17, 99, 100, 101, (1UL << 32) - 1, 1UL << 32, (1UL << 62) + 12345, ulong.MaxValue, ulong.MaxValue - 1, 18446744065119617025UL /* (2^32-1)^2 */ };
            foreach (ulong v in samples)
                AssertFloorSqrt(v);

            var rng = new SplitMix64(3);
            for (int i = 0; i < 20000; i++)
                AssertFloorSqrt(rng.Next() >> (int)(rng.Next() % 64));
        }

        static void AssertFloorSqrt(ulong v)
        {
            BigInteger s = fWideMath.IntegerSqrt(v);
            Assert.That(s * s <= v && (s + 1) * (s + 1) > v, Is.True, v.ToString());
        }

        [Test]
        public void IntegerSqrtRounded_IsNearestAndMonotonic()
        {
            ulong prev = 0;
            for (ulong v = 0; v < 200000; v++)
            {
                ulong s = fWideMath.IntegerSqrtRounded(v);
                Assert.That(s, Is.GreaterThanOrEqualTo(prev));
                // |s^2 - v| is minimal among s-1, s, s+1 (using 4v vs (2s±1)^2 to stay integral)
                BigInteger four = (BigInteger)v * 4;
                Assert.That((2 * (BigInteger)s - 1) * (2 * (BigInteger)s - 1) <= four || s == 0, Is.True);
                Assert.That((2 * (BigInteger)s + 1) * (2 * (BigInteger)s + 1) >= four, Is.True);
                prev = s;
            }
            Assert.That(fWideMath.IntegerSqrtRounded(ulong.MaxValue), Is.EqualTo(1UL << 32));
        }

        [Test]
        public void MulQ16_MatchesReferenceWithTiesToEven()
        {
            var rng = new SplitMix64(4);
            for (int i = 0; i < 20000; i++)
            {
                int a = (int)rng.Next() >> (int)(rng.Next() % 24);
                int b = (int)rng.Next() >> (int)(rng.Next() % 24);
                BigInteger expected = RoundDivEven((BigInteger)a * b, 1 << 16);
                if (expected > int.MaxValue) expected = int.MaxValue;
                if (expected < int.MinValue) expected = int.MinValue;
                Assert.That((BigInteger)fWideMath.MulQ16(a, b), Is.EqualTo(expected));
            }
            // exact tie: 0.5 raw * 1 raw => 0.5 raw => 0 (even); 3 raw * 0.5 => 1.5 raw => 2
            Assert.That(fWideMath.MulQ16(1, 1 << 15), Is.EqualTo(0));
            Assert.That(fWideMath.MulQ16(3, 1 << 15), Is.EqualTo(2));
            Assert.That(fWideMath.MulQ16(-3, 1 << 15), Is.EqualTo(-2));
            Assert.That(fWideMath.MulQ16(int.MaxValue, int.MaxValue), Is.EqualTo(int.MaxValue));
            Assert.That(fWideMath.MulQ16(int.MinValue, int.MaxValue), Is.EqualTo(int.MinValue));
        }

        [Test]
        public void TryDivQ16_Contract()
        {
            Assert.That(fWideMath.TryDivQ16(1 << 16, 3 << 16, out int third), Is.True);
            Assert.That(third, Is.EqualTo(21845)); // 65536/3 = 21845.33
            Assert.That(fWideMath.TryDivQ16(5, 0, out int pos), Is.False);
            Assert.That(pos, Is.EqualTo(int.MaxValue));
            Assert.That(fWideMath.TryDivQ16(-5, 0, out int neg), Is.False);
            Assert.That(neg, Is.EqualTo(int.MinValue));
            Assert.That(fWideMath.TryDivQ16(0, 0, out int zero), Is.False);
            Assert.That(zero, Is.EqualTo(0));
            Assert.That(fWideMath.TryDivQ16(int.MaxValue, 1, out int sat), Is.False); // 32767.99 / 1 raw
            Assert.That(sat, Is.EqualTo(int.MaxValue));
            Assert.That(fWideMath.TryDivQ16(int.MinValue, -(1 << 16), out int minOverMinusOne), Is.False);
            Assert.That(minOverMinusOne, Is.EqualTo(int.MaxValue));
        }

        [Test]
        public void TryNormalizeQ30_AxisDiagonalTinyAndHuge()
        {
            Assert.That(fWideMath.TryNormalizeQ30(0, 0, 0, 0, out _, out _, out _, out _), Is.False);

            Assert.That(fWideMath.TryNormalizeQ30(1, 0, 0, 0, out int x, out int y, out int z, out int w), Is.True);
            Assert.That(new[] { x, y, z, w }, Is.EqualTo(new[] { 1 << 30, 0, 0, 0 }));

            Assert.That(fWideMath.TryNormalizeQ30(0, long.MinValue, 0, 0, out x, out y, out z, out w), Is.True);
            Assert.That(new[] { x, y, z, w }, Is.EqualTo(new[] { 0, -(1 << 30), 0, 0 }));

            fWideMath.TryNormalizeQ30(1, 1, 0, 0, out x, out y, out _, out _);
            Assert.That(x, Is.EqualTo(759250125).Within(1)); // 2^30 / sqrt(2)
            Assert.That(y, Is.EqualTo(x));

            fWideMath.TryNormalizeQ30(-3, 4, 0, 0, out x, out y, out _, out _);
            Assert.That(x, Is.EqualTo(-644245094).Within(1)); // -0.6
            Assert.That(y, Is.EqualTo(858993459).Within(1));  // 0.8
        }

        [Test]
        public void TryNormalizeQ30_UnitLengthAndSymmetry()
        {
            var rng = new SplitMix64(5);
            for (int i = 0; i < 20000; i++)
            {
                int sh = (int)(rng.Next() % 63);
                long a = (long)rng.Next() >> sh, b = (long)rng.Next() >> sh, c = (long)rng.Next() >> sh, d = (i & 1) == 0 ? 0 : (long)rng.Next() >> sh;
                if (a == 0 && b == 0 && c == 0 && d == 0) continue;
                fWideMath.TryNormalizeQ30(a, b, c, d, out int x, out int y, out int z, out int w);
                BigInteger len2 = (BigInteger)x * x + (BigInteger)y * y + (BigInteger)z * z + (BigInteger)w * w;
                BigInteger err = BigInteger.Abs(len2 - (BigInteger.One << 60));
                // |u|^2 within ~4 raw of 1.0 in Q30 => relative error < 2^-28
                Assert.That(err < (BigInteger.One << 33), Is.True, $"{a},{b},{c},{d}");

                fWideMath.TryNormalizeQ30(a == long.MinValue ? a : -a, b == long.MinValue ? b : -b, c == long.MinValue ? c : -c, d == long.MinValue ? d : -d, out int nx, out int ny, out int nz, out int nw);
                if (a != long.MinValue && b != long.MinValue && c != long.MinValue && d != long.MinValue)
                    Assert.That(new[] { nx, ny, nz, nw }, Is.EqualTo(new[] { -x, -y, -z, -w }));
            }
        }
    }
}
