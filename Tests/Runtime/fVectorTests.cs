using System;
using System.Numerics;
using NUnit.Framework;

namespace fMath.Tests
{
    [TestFixture]
    public class fVectorTests
    {
        const double Q16 = 65536.0;
        const double Q30 = 1073741824.0;
        const double TurnToRad = 2 * Math.PI / 4294967296.0;

        static fVector3 M(double x, double y, double z) => fVector3.FromRaw((int)Math.Round(x * Q16), (int)Math.Round(y * Q16), (int)Math.Round(z * Q16));

        static double UnitLengthError(fUnitVector3 u)
        {
            double x = u.x.RawValue / Q30, y = u.y.RawValue / Q30, z = u.z.RawValue / Q30;
            return Math.Abs(Math.Sqrt(x * x + y * y + z * z) - 1.0);
        }

        // Small: 1, 2, 4 raw, ~0.1 mm, ~1 mm, ~1 cm. Gameplay: 1 m .. 2048 m.
        static readonly int[] SmallRaw = { 1, 2, 4, 7, 66, 655 };
        static readonly int[] GameplayMeters = { 1, 10, 100, 500, 1000, 2048 };

        [Test]
        public void EqualityIsRawExact()
        {
            Assert.That(fVector3.FromRaw(1, 2, 3) == fVector3.FromRaw(1, 2, 3), Is.True);
            Assert.That(fVector3.FromRaw(1, 2, 3) == fVector3.FromRaw(1, 2, 4), Is.False);
            // v1 reported these as equal because (a-b).sqrMagnitude overflowed
            Assert.That(fVector3.zero == fVector3.FromInt(800, 0, 0), Is.False);
            Assert.That(fVector3.Approximately(fVector3.FromRaw(10, 10, 10), fVector3.FromRaw(11, 9, 10), ffloat.Epsilon), Is.True);
            Assert.That(fVector3.Approximately(fVector3.FromRaw(10, 10, 10), fVector3.FromRaw(12, 9, 10), ffloat.Epsilon), Is.False);
            Assert.That(fVector2.FromRaw(1, 2) == fVector2.FromRaw(1, 2), Is.True);
            Assert.That(fVector2.FromRaw(1, 2) != fVector2.FromRaw(2, 1), Is.True);
        }

        [Test]
        public void Operators()
        {
            fVector3 a = fVector3.FromInt(1, 2, 3), b = fVector3.FromInt(4, 5, 6);
            Assert.That(a + b, Is.EqualTo(fVector3.FromInt(5, 7, 9)));
            Assert.That(b - a, Is.EqualTo(fVector3.FromInt(3, 3, 3)));
            Assert.That(-a, Is.EqualTo(fVector3.FromInt(-1, -2, -3)));
            Assert.That(a * ffloat.Two, Is.EqualTo(fVector3.FromInt(2, 4, 6)));
            Assert.That(a * 3, Is.EqualTo(fVector3.FromInt(3, 6, 9)));
            Assert.That(b / ffloat.Two, Is.EqualTo(M(2, 2.5, 3)));
            Assert.That(b / 2, Is.EqualTo(M(2, 2.5, 3)));
            Assert.That(a * funit.Half, Is.EqualTo(M(0.5, 1, 1.5)));
            Assert.That(fVector3.FromInt(30000, 0, 0) + fVector3.FromInt(30000, 0, 0), Is.EqualTo(new fVector3(ffloat.MaxValue, ffloat.Zero, ffloat.Zero)));
        }

        [Test]
        public void LengthSquaredWide_IsExactForAllScales()
        {
            fVector3 max = fVector3.FromRaw(int.MinValue, int.MinValue, int.MinValue);
            Assert.That(max.LengthSquaredWide, Is.EqualTo(3UL << 62));
            foreach (int raw in SmallRaw)
                Assert.That(fVector3.FromRaw(raw, raw, 0).LengthSquaredWide, Is.EqualTo(2UL * (ulong)raw * (ulong)raw));
            foreach (int m in GameplayMeters)
            {
                fVector3 v = fVector3.FromInt(m, -m, m / 2);
                BigInteger expected = BigInteger.Pow((long)m << 16, 2) * 2 + BigInteger.Pow(((long)m / 2) << 16, 2);
                Assert.That((BigInteger)v.LengthSquaredWide, Is.EqualTo(expected));
            }
            // v1: 1000^2 overflowed and wrapped negative
            Assert.That(fVector3.FromInt(1000, 0, 0).LengthSquaredWide, Is.EqualTo((1000UL << 16) * (1000UL << 16)));
            Assert.That(fVector3.FromInt(1000, 0, 0).magnitude, Is.EqualTo(ffloat.FromInt(1000)));
        }

        [Test]
        public void DistanceSquaredWide_DoesNotOverflowOnDifference()
        {
            fVector3 a = fVector3.FromRaw(int.MaxValue, 0, 0), b = fVector3.FromRaw(int.MinValue, 0, 0);
            Assert.That(fVector3.DistanceSquaredWide(a, b), Is.EqualTo(((1UL << 32) - 1) * ((1UL << 32) - 1)));
            fVector3 p = fVector3.FromInt(-2048, 0, 2048), q = fVector3.FromInt(2048, 0, -2048);
            Assert.That(fVector3.Distance(p, q).ToDouble(), Is.EqualTo(Math.Sqrt(2) * 4096).Within(1.0 / Q16));
        }

        [Test]
        public void DistanceComparisonsWithoutSqrt()
        {
            fVector3 a = fVector3.FromInt(3, 0, 0), b = fVector3.FromInt(0, 4, 0);
            Assert.That(fVector3.CompareDistanceSquared(a, b, ffloat.FromInt(5)), Is.EqualTo(0));
            Assert.That(fVector3.CompareDistanceSquared(a, b, ffloat.FromInt(5) - ffloat.Epsilon), Is.EqualTo(1));
            Assert.That(fVector3.CompareDistanceSquared(a, b, ffloat.FromInt(5) + ffloat.Epsilon), Is.EqualTo(-1));
            Assert.That(fVector3.IsWithinDistance(a, b, ffloat.FromInt(5)), Is.True);
            // 1 raw resolution at 2048 m
            fVector3 far = fVector3.FromInt(2048, 0, 0);
            Assert.That(fVector3.IsWithinDistance(fVector3.zero, far, ffloat.FromInt(2048)), Is.True);
            Assert.That(fVector3.IsWithinDistance(fVector3.zero, far, ffloat.FromInt(2048) - ffloat.Epsilon), Is.False);
            Assert.That(fVector2.IsWithinDistance(fVector2.FromInt(3, 0), fVector2.FromInt(0, 4), ffloat.FromInt(5)), Is.True);
        }

        [Test]
        public void DotAndCrossAreWide()
        {
            fVector3 a = fVector3.FromInt(2048, 1000, -500), b = fVector3.FromInt(-700, 2048, 300);
            long expectedDot = (2048L * -700 + 1000L * 2048 + -500L * 300) << 32;
            Assert.That(fVector3.DotWide(a, b), Is.EqualTo(expectedDot));
            fWideVector3 c = fVector3.CrossWide(a, b);
            Assert.That(c.x, Is.EqualTo((1000L * 300 - -500L * 2048) << 32));
            Assert.That(c.y, Is.EqualTo((-500L * -700 - 2048L * 300) << 32));
            Assert.That(c.z, Is.EqualTo((2048L * 2048 - 1000L * -700) << 32));
            Assert.That(fVector3.Dot(fVector3.FromInt(1, 2, 3), fVector3.FromInt(4, 5, 6)), Is.EqualTo(ffloat.FromInt(32)));
            Assert.That(fVector3.Cross(fVector3.right, fVector3.up), Is.EqualTo(fVector3.forward));
            Assert.That(fVector2.CrossWide(fVector2.right, fVector2.up), Is.EqualTo(1L << 32));
            // tiny vectors keep their dot exactly (v1 truncated each product to zero)
            Assert.That(fVector3.DotWide(fVector3.FromRaw(1, 1, 1), fVector3.FromRaw(1, 1, 1)), Is.EqualTo(3L));
        }

        [Test]
        public void Normalize_SmallGameplayAndDirections()
        {
            foreach (int raw in SmallRaw)
            {
                Assert.That(fVector3.TryNormalize(fVector3.FromRaw(raw, 0, 0), out fUnitVector3 axis), Is.True, raw.ToString());
                Assert.That(axis, Is.EqualTo(fUnitVector3.right));
                Assert.That(fVector3.TryNormalize(fVector3.FromRaw(raw, raw, raw), out fUnitVector3 diag), Is.True);
                Assert.That(diag.x.RawValue, Is.EqualTo(Q30 / Math.Sqrt(3)).Within(1.0));
                Assert.That(UnitLengthError(diag), Is.LessThan(4e-9));
            }
            foreach (int m in GameplayMeters)
            {
                Assert.That(fVector3.TryNormalize(fVector3.FromInt(m, -m, m), out fUnitVector3 u), Is.True);
                Assert.That(u.y.RawValue, Is.EqualTo(-Q30 / Math.Sqrt(3)).Within(1.0));
                Assert.That(UnitLengthError(u), Is.LessThan(4e-9));
                Assert.That(fVector3.TryNormalize(fVector3.FromInt(0, 0, -m), out u), Is.True);
                Assert.That(u, Is.EqualTo(fUnitVector3.back));
            }
            Assert.That(fVector3.TryNormalize(fVector3.zero, out _), Is.False);
            Assert.That(fVector3.TryNormalize(fVector3.FromRaw(int.MinValue, int.MaxValue, 0), out fUnitVector3 extreme), Is.True);
            Assert.That(UnitLengthError(extreme), Is.LessThan(4e-9));
        }

        [Test]
        public void Normalize_RandomAccuracyAndSymmetry()
        {
            var rng = new SplitMix64(31);
            for (int i = 0; i < 50000; i++)
            {
                int sh = (int)(rng.Next() % 31);
                fVector3 v = fVector3.FromRaw((int)rng.Next() >> sh, (int)rng.Next() >> sh, (int)rng.Next() >> sh);
                if (!fVector3.TryNormalize(v, out fUnitVector3 u)) continue;
                double len = Math.Sqrt((double)v.x.RawValue * v.x.RawValue + (double)v.y.RawValue * v.y.RawValue + (double)v.z.RawValue * v.z.RawValue);
                Assert.That(u.x.RawValue, Is.EqualTo(v.x.RawValue / len * Q30).Within(1.0));
                Assert.That(u.y.RawValue, Is.EqualTo(v.y.RawValue / len * Q30).Within(1.0));
                Assert.That(u.z.RawValue, Is.EqualTo(v.z.RawValue / len * Q30).Within(1.0));
                if (v.x.RawValue != int.MinValue && v.y.RawValue != int.MinValue && v.z.RawValue != int.MinValue)
                {
                    fVector3.TryNormalize(-v, out fUnitVector3 n);
                    Assert.That(n, Is.EqualTo(-u));
                }
            }
        }

        [Test]
        public void UnitVectorApi()
        {
            Assert.That(fUnitVector3.up.IsValid, Is.True);
            Assert.That(default(fUnitVector3).IsValid, Is.False);
            Assert.That(fUnitVector3.Dot(fUnitVector3.up, fUnitVector3.up), Is.EqualTo(funit.One));
            Assert.That(fUnitVector3.Dot(fUnitVector3.up, fUnitVector3.down), Is.EqualTo(funit.MinusOne));
            Assert.That(fUnitVector3.TryNormalizedCross(fUnitVector3.right, fUnitVector3.up, out fUnitVector3 c), Is.True);
            Assert.That(c, Is.EqualTo(fUnitVector3.forward));
            Assert.That(fUnitVector3.TryNormalizedCross(fUnitVector3.right, fUnitVector3.left, out _), Is.False);
            Assert.That(fUnitVector3.right * ffloat.FromInt(5), Is.EqualTo(fVector3.FromInt(5, 0, 0)));
            Assert.That(fUnitVector3.back.ToVector(), Is.EqualTo(fVector3.back));
            Assert.That(fUnitVector3.TryFromRaw(3, 4, 0, out fUnitVector3 r), Is.True);
            Assert.That(r.x.RawValue, Is.EqualTo((int)Math.Round(0.6 * Q30)).Within(1));
        }

        [Test]
        public void AnglesUseAtan2AndDotThresholds()
        {
            fVector3 v = fVector3.FromInt(3, 4, 5);
            // v1 returned -4.66 degrees for Angle(v, v)
            Assert.That(fVector3.AngleUsingAtan2(v, v), Is.EqualTo(fAngle.Zero));
            Assert.That(fVector3.Angle(fVector3.right, fVector3.up), Is.EqualTo(fAngle.QuarterTurn));
            Assert.That(fVector3.Angle(fVector3.right, fVector3.left), Is.EqualTo(fAngle.HalfTurn));
            Assert.That(fVector3.Angle(fVector3.FromInt(1, 0, 0), fVector3.FromInt(1, 1, 0)), Is.EqualTo(fAngle.FromTurnsFraction(1, 8)));
            // almost parallel: 1 cm offset at 2048 m (4.88e-6 rad)
            fVector3 a = fVector3.FromInt(2048, 0, 0), b = new fVector3(ffloat.FromInt(2048), ffloat.FromFraction(1, 100), ffloat.Zero);
            double expected = Math.Atan2(655.0 / Q16, 2048.0);
            Assert.That(fVector3.AngleUsingAtan2(a, b).RawValue * TurnToRad, Is.EqualTo(expected).Within(3e-8));
            Assert.That(fVector3.WithinAngle(a, b, fAngle.FromDegreesFraction(1, 1000)), Is.True);
            Assert.That(fVector3.WithinAngle(fVector3.right, fVector3.FromInt(1, 1, 0), fAngle.FromDegrees(45)), Is.True);
            Assert.That(fVector3.WithinAngle(fVector3.right, fVector3.FromInt(1, 1, 0), fAngle.FromDegrees(44)), Is.False);
            Assert.That(fVector3.WithinAngle(fVector3.zero, fVector3.up, fAngle.HalfTurn), Is.False);
            Assert.That(fUnitVector3.AngleUsingAtan2(fUnitVector3.up, fUnitVector3.forward), Is.EqualTo(fAngle.QuarterTurn));

            Assert.That(fVector3.SignedAngle(fVector3.right, fVector3.forward, fUnitVector3.up), Is.EqualTo(fAngleDelta.MinusQuarterTurn));
            Assert.That(fVector3.SignedAngle(fVector3.forward, fVector3.right, fUnitVector3.up), Is.EqualTo(fAngleDelta.QuarterTurn));
            Assert.That(fVector2.SignedAngle(fVector2.right, fVector2.up), Is.EqualTo(fAngleDelta.QuarterTurn));
            Assert.That(fVector2.SignedAngle(fVector2.up, fVector2.right), Is.EqualTo(fAngleDelta.MinusQuarterTurn));
            Assert.That(fVector2.Angle(fVector2.FromInt(5, 5), fVector2.FromInt(5, 5)), Is.EqualTo(fAngle.Zero));
        }

        [Test]
        public void ReflectProjectLerpMoveTowardsClamp()
        {
            Assert.That(fVector3.Reflect(fVector3.FromInt(1, -1, 0), fUnitVector3.up), Is.EqualTo(fVector3.FromInt(1, 1, 0)));
            Assert.That(fVector3.Project(fVector3.FromInt(3, 4, 5), fUnitVector3.up), Is.EqualTo(fVector3.FromInt(0, 4, 0)));
            Assert.That(fVector3.ProjectOnPlane(fVector3.FromInt(3, 4, 5), fUnitVector3.up), Is.EqualTo(fVector3.FromInt(3, 0, 5)));
            fVector3.TryNormalize(fVector3.FromInt(1, 1, 0), out fUnitVector3 diag);
            fVector3 r = fVector3.Reflect(fVector3.FromInt(-2048, 0, 0), diag);
            Assert.That(fVector3.Approximately(r, fVector3.FromInt(0, 2048, 0), ffloat.FromRaw(2)), Is.True, r.ToString());
            Assert.That(fVector3.Lerp(fVector3.zero, fVector3.FromInt(10, 20, 30), ffloat.Half), Is.EqualTo(fVector3.FromInt(5, 10, 15)));
            Assert.That(fVector3.MoveTowards(fVector3.zero, fVector3.FromInt(10, 0, 0), ffloat.FromInt(3)), Is.EqualTo(fVector3.FromInt(3, 0, 0)));
            Assert.That(fVector3.MoveTowards(fVector3.zero, fVector3.FromInt(1, 0, 0), ffloat.FromInt(3)), Is.EqualTo(fVector3.FromInt(1, 0, 0)));
            Assert.That(fVector3.ClampMagnitude(fVector3.FromInt(0, 30, 40), ffloat.FromInt(5)), Is.EqualTo(fVector3.FromInt(0, 3, 4)));
            Assert.That(fVector3.ClampMagnitude(fVector3.FromInt(0, 3, 4), ffloat.FromInt(5)), Is.EqualTo(fVector3.FromInt(0, 3, 4)));
            Assert.That(fVector2.Reflect(fVector2.FromInt(1, -1), fUnitVector2.up), Is.EqualTo(fVector2.FromInt(1, 1)));
            Assert.That(fVector2.Rotate(fVector2.FromInt(10, 0), fAngle.QuarterTurn), Is.EqualTo(fVector2.FromInt(0, 10)));
            Assert.That(fVector2.Rotate(fVector2.FromInt(2048, 0), fAngle.HalfTurn), Is.EqualTo(fVector2.FromInt(-2048, 0)));
        }

        [Test]
        public void UnitVector2Api()
        {
            Assert.That(fUnitVector2.FromAngle(fAngle.QuarterTurn), Is.EqualTo(fUnitVector2.up));
            Assert.That(fUnitVector2.FromAngle(fAngle.HalfTurn), Is.EqualTo(fUnitVector2.left));
            fAngle a = fAngle.FromDegrees(33);
            Assert.That(fUnitVector2.FromAngle(a).ToAngle().RawValue, Is.EqualTo(a.RawValue).Within(8));
            Assert.That(fUnitVector2.Rotate(fUnitVector2.right, fAngle.QuarterTurn), Is.EqualTo(fUnitVector2.up));
            Assert.That(fUnitVector2.SignedAngle(fUnitVector2.up, fUnitVector2.right), Is.EqualTo(fAngleDelta.MinusQuarterTurn));
            Assert.That(fUnitVector2.WithinAngle(fUnitVector2.right, fUnitVector2.FromAngle(fAngle.FromDegrees(10)), fAngle.FromDegrees(11)), Is.True);
            Assert.That(fUnitVector2.WithinAngle(fUnitVector2.right, fUnitVector2.FromAngle(fAngle.FromDegrees(10)), fAngle.FromDegrees(9)), Is.False);
            Assert.That(fVector2.TryNormalize(fVector2.FromRaw(1, 0), out fUnitVector2 u), Is.True);
            Assert.That(u, Is.EqualTo(fUnitVector2.right));
        }
    }
}
