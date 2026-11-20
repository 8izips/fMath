using System;
using NUnit.Framework;

namespace fMath.Tests
{
    [TestFixture]
    public class fTrigTests
    {
        const double TurnToRad = 2 * Math.PI / 4294967296.0;
        const double Q30 = 1073741824.0;
        // Upper bound for |LUT interpolation error| in Q1.30 raw units (h^2/8 * 2^30 ~ 19.7 + rounding)
        const double MaxSinErrorRaw = 22;
        // Upper bound for |atan2 error| in Angle32 raw units (interpolation ~3.3 + ratio rounding)
        const double MaxAtanErrorRaw = 6;

        static double SinRef(uint raw) => Math.Sin(raw * TurnToRad) * Q30;

        [Test]
        public void SpecialValuesAreExact()
        {
            Assert.That(fTrig.Sin(fAngle.Zero), Is.EqualTo(funit.Zero));
            Assert.That(fTrig.Cos(fAngle.Zero), Is.EqualTo(funit.One));
            Assert.That(fTrig.Sin(fAngle.QuarterTurn), Is.EqualTo(funit.One));
            Assert.That(fTrig.Cos(fAngle.QuarterTurn), Is.EqualTo(funit.Zero));
            Assert.That(fTrig.Sin(fAngle.HalfTurn), Is.EqualTo(funit.Zero));
            Assert.That(fTrig.Cos(fAngle.HalfTurn), Is.EqualTo(funit.MinusOne));
            Assert.That(fTrig.Sin(fAngle.ThreeQuarterTurn), Is.EqualTo(funit.MinusOne));
            Assert.That(fTrig.Cos(fAngle.ThreeQuarterTurn), Is.EqualTo(funit.Zero));
            Assert.That(fTrig.Sin(fAngle.FromDegrees(45)), Is.EqualTo(fTrig.Cos(fAngle.FromDegrees(45))));
            Assert.That(fTrig.Tan(fAngle.FromTurnsFraction(1, 8)), Is.EqualTo(ffloat.One));
        }

        [Test]
        public void CommonAngles()
        {
            int[] degrees = { 0, 15, 30, 45, 60, 75, 90, 120, 135, 150, 180, 210, 225, 270, 300, 315, 330, 359 };
            foreach (int d in degrees)
            {
                fAngle a = fAngle.FromDegrees(d);
                Assert.That(fTrig.Sin(a).RawValue, Is.EqualTo(SinRef(a.RawValue)).Within(MaxSinErrorRaw), $"sin {d}");
                Assert.That(fTrig.Cos(a).RawValue, Is.EqualTo(Math.Cos(a.RawValue * TurnToRad) * Q30).Within(MaxSinErrorRaw), $"cos {d}");
            }
            Assert.That(fTrig.Sin(fAngle.FromDegrees(30)).RawValue, Is.EqualTo(1 << 29).Within(MaxSinErrorRaw));
        }

        [Test]
        public void SweepErrorWithinBound()
        {
            var rng = new SplitMix64(21);
            double maxError = 0;
            for (int i = 0; i < 200000; i++)
            {
                uint raw = (uint)rng.Next();
                double err = Math.Abs(fTrig.Sin(fAngle.FromRaw(raw)).RawValue - SinRef(raw));
                if (err > maxError) maxError = err;
            }
            TestContext.WriteLine($"max |sin error| = {maxError:F2} raw Q1.30 ({maxError / Q30:E3})");
            Assert.That(maxError, Is.LessThanOrEqualTo(MaxSinErrorRaw));
        }

        [Test]
        public void SymmetriesAreExact()
        {
            var rng = new SplitMix64(22);
            for (int i = 0; i < 50000; i++)
            {
                fAngle a = fAngle.FromRaw((uint)rng.Next());
                Assert.That(fTrig.Sin(-a), Is.EqualTo(-fTrig.Sin(a)));
                Assert.That(fTrig.Cos(-a), Is.EqualTo(fTrig.Cos(a)));
                Assert.That(fTrig.Sin(a + fAngle.HalfTurn), Is.EqualTo(-fTrig.Sin(a)));
                Assert.That(fTrig.Cos(a + fAngle.HalfTurn), Is.EqualTo(-fTrig.Cos(a)));
                Assert.That(fTrig.Cos(a), Is.EqualTo(fTrig.Sin(a + fAngle.QuarterTurn)));
            }
        }

        [Test]
        public void PythagoreanIdentity()
        {
            var rng = new SplitMix64(23);
            for (int i = 0; i < 50000; i++)
            {
                fAngle a = fAngle.FromRaw((uint)rng.Next());
                long s = fTrig.Sin(a).RawValue, c = fTrig.Cos(a).RawValue;
                double len2 = (s * s + c * c) / (Q30 * Q30);
                Assert.That(len2, Is.EqualTo(1.0).Within(1e-7));
            }
        }

        [Test]
        public void WrapQuadrantBoundariesAndTinyAngles()
        {
            Assert.That(fTrig.Sin(fAngle.FromRaw(1)).RawValue, Is.EqualTo(2)); // 2*pi/2^32 * 2^30 = 1.57
            Assert.That(fTrig.Sin(fAngle.FromRaw(0xFFFFFFFFu)).RawValue, Is.EqualTo(-2));
            Assert.That(fTrig.Cos(fAngle.FromRaw(1)), Is.EqualTo(funit.One));
            uint[] boundaries = { 0u, 0x40000000u, 0x80000000u, 0xC0000000u };
            foreach (uint b in boundaries)
            {
                foreach (int d in new[] { -2, -1, 0, 1, 2 })
                {
                    uint raw = unchecked((uint)(b + d));
                    Assert.That(fTrig.Sin(fAngle.FromRaw(raw)).RawValue, Is.EqualTo(SinRef(raw)).Within(2), $"boundary {b:X8}{d:+0;-0}");
                }
            }
            Assert.That(fTrig.Sin(fAngle.FromDegrees(720 + 30)), Is.EqualTo(fTrig.Sin(fAngle.FromDegrees(30))));
        }

        [Test]
        public void SinIsMonotonicInFirstQuadrant()
        {
            int prev = int.MinValue;
            for (uint raw = 0; raw <= 0x40000000u; raw += 4099)
            {
                int s = fTrig.Sin(fAngle.FromRaw(raw)).RawValue;
                Assert.That(s >= prev, Is.True);
                prev = s;
            }
        }

        [Test]
        public void TanNearQuarterTurnSaturates()
        {
            Assert.That(fTrig.TryTan(fAngle.QuarterTurn, out ffloat t), Is.False);
            Assert.That(t, Is.EqualTo(ffloat.MaxValue));
            Assert.That(fTrig.TryTan(fAngle.ThreeQuarterTurn, out t), Is.False);
            Assert.That(t, Is.EqualTo(ffloat.MinValue));
            Assert.That(fTrig.Tan(fAngle.FromDegrees(-45)), Is.EqualTo(-ffloat.One));
            Assert.That(fTrig.Tan(fAngle.FromDegrees(60)).ToDouble(), Is.EqualTo(Math.Sqrt(3)).Within(2e-5));
        }

        [Test]
        public void Atan2_Axes()
        {
            ffloat one = ffloat.One, zero = ffloat.Zero;
            Assert.That(fTrig.Atan2(zero, zero), Is.EqualTo(fAngle.Zero));
            Assert.That(fTrig.Atan2(zero, one), Is.EqualTo(fAngle.Zero));
            Assert.That(fTrig.Atan2(one, zero), Is.EqualTo(fAngle.QuarterTurn));
            Assert.That(fTrig.Atan2(zero, -one), Is.EqualTo(fAngle.HalfTurn));
            Assert.That(fTrig.Atan2(-one, zero), Is.EqualTo(fAngle.ThreeQuarterTurn));
            Assert.That(fTrig.Atan2(one, one), Is.EqualTo(fAngle.FromTurnsFraction(1, 8)));
            Assert.That(fTrig.Atan2(-one, -one), Is.EqualTo(fAngle.FromTurnsFraction(5, 8)));
            Assert.That(fTrig.Atan2Wide(long.MinValue, 0), Is.EqualTo(fAngle.ThreeQuarterTurn));
        }

        [Test]
        public void Atan2_ErrorWithinBound()
        {
            var rng = new SplitMix64(24);
            double maxError = 0;
            for (int i = 0; i < 200000; i++)
            {
                int y = (int)rng.Next() >> (int)(rng.Next() % 24);
                int x = (int)rng.Next() >> (int)(rng.Next() % 24);
                if (x == 0 && y == 0) continue;
                uint got = fTrig.Atan2(ffloat.FromRaw(y), ffloat.FromRaw(x)).RawValue;
                double expected = Math.Atan2(y, x) / TurnToRad;
                double diff = got - expected;
                diff -= Math.Round(diff / 4294967296.0) * 4294967296.0;
                if (Math.Abs(diff) > maxError) maxError = Math.Abs(diff);
            }
            TestContext.WriteLine($"max |atan2 error| = {maxError:F2} raw angle ({maxError * TurnToRad:E3} rad)");
            Assert.That(maxError, Is.LessThanOrEqualTo(MaxAtanErrorRaw));
        }

        [Test]
        public void Atan2_Symmetry()
        {
            var rng = new SplitMix64(25);
            for (int i = 0; i < 50000; i++)
            {
                int y = (int)(rng.Next() >> 34), x = (int)(rng.Next() >> 34);
                fAngle a = fTrig.Atan2(ffloat.FromRaw(y), ffloat.FromRaw(x));
                Assert.That(fTrig.Atan2(ffloat.FromRaw(-y), ffloat.FromRaw(x)), Is.EqualTo(-a));
                Assert.That(fTrig.Atan2(ffloat.FromRaw(y), ffloat.FromRaw(-x)), Is.EqualTo(fAngle.HalfTurn + (-a)));
            }
        }

        [Test]
        public void Atan2_IsMonotonicAroundTheCircle()
        {
            // walk (cos, sin) around a circle of radius 1000 raw with a fine step: angle must increase
            uint prev = 0;
            for (int i = 1; i < 1 << 16; i++)
            {
                uint theta = (uint)i << 16;
                fAngle t = fAngle.FromRaw(theta);
                long x = fWideMath.RoundShiftRightToEven((long)fTrig.Cos(t).RawValue * 1000000000L, 30);
                long y = fWideMath.RoundShiftRightToEven((long)fTrig.Sin(t).RawValue * 1000000000L, 30);
                uint a = fTrig.Atan2Wide(y, x).RawValue;
                Assert.That(a >= prev, Is.True, $"step {i}");
                prev = a;
            }
            // and along a line in the first octant (fixed x, increasing y)
            prev = 0;
            for (int y = 0; y <= 1 << 20; y += 3)
            {
                uint a = fTrig.Atan2Wide(y, 1 << 20).RawValue;
                Assert.That(a >= prev, Is.True);
                prev = a;
            }
        }

        [Test]
        public void Atan2_TinyAngles()
        {
            // 1 cm lateral offset at 2048 m range: 4.88e-6 rad
            fAngle a = fTrig.Atan2(ffloat.FromFraction(1, 100), ffloat.FromInt(2048));
            double expected = Math.Atan2(655.0 / 65536.0, 2048.0);
            Assert.That(a.RawValue * TurnToRad, Is.EqualTo(expected).Within(2e-8));
            Assert.That(fTrig.Atan2(ffloat.Epsilon, ffloat.One).RawValue, Is.EqualTo(10430u).Within(2)); // 1/65536 rad
        }

        [Test]
        public void AcosAsinAtan()
        {
            Assert.That(fTrig.Acos(funit.One), Is.EqualTo(fAngle.Zero));
            Assert.That(fTrig.Acos(funit.MinusOne), Is.EqualTo(fAngle.HalfTurn));
            Assert.That(fTrig.Acos(funit.Zero), Is.EqualTo(fAngle.QuarterTurn));
            Assert.That(fTrig.Acos(funit.Half).RawValue, Is.EqualTo(fAngle.FromDegrees(60).RawValue).Within(4));
            Assert.That(fTrig.Asin(funit.One), Is.EqualTo(fAngleDelta.QuarterTurn));
            Assert.That(fTrig.Asin(funit.MinusOne), Is.EqualTo(fAngleDelta.MinusQuarterTurn));
            Assert.That(fTrig.Asin(funit.Half).RawValue, Is.EqualTo(fAngleDelta.FromDegrees(30).RawValue).Within(4));
            Assert.That(fTrig.Asin(-funit.Half), Is.EqualTo(-fTrig.Asin(funit.Half)));
            Assert.That(fTrig.Atan(ffloat.One), Is.EqualTo(fAngleDelta.FromTurnsFraction(1, 8)));
            Assert.That(fTrig.Atan(-ffloat.One), Is.EqualTo(-fAngleDelta.FromTurnsFraction(1, 8)));
            Assert.That(fTrig.Acos(funit.FromRaw(int.MaxValue)), Is.EqualTo(fAngle.Zero)); // clamped

            uint prev = uint.MaxValue;
            for (int raw = -(1 << 30); raw <= 1 << 30; raw += 65537)
            {
                uint a = fTrig.Acos(funit.FromRaw(raw)).RawValue;
                Assert.That(a <= prev, Is.True, "acos must be non-increasing");
                prev = a;
            }
        }

        [Test]
        public void SinTableEndpoints()
        {
            Assert.That(fTrigSinQuarterLut.Table.Length, Is.EqualTo(4097));
            Assert.That(fTrigSinQuarterLut.Table[0], Is.EqualTo(0));
            Assert.That(fTrigSinQuarterLut.Table[4096], Is.EqualTo(1 << 30));
            Assert.That(fTrigAtanLut.Table.Length, Is.EqualTo(4097));
            Assert.That(fTrigAtanLut.Table[4096], Is.EqualTo(1u << 29));
            for (int i = 1; i < 4097; i++)
            {
                Assert.That(fTrigSinQuarterLut.Table[i] > fTrigSinQuarterLut.Table[i - 1], Is.True);
                Assert.That(fTrigAtanLut.Table[i] > fTrigAtanLut.Table[i - 1], Is.True);
            }
        }
    }
}
