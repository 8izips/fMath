using System;
using NUnit.Framework;

namespace fMath.Tests
{
    [TestFixture]
    public class fQuaternionTests
    {
        const double Q30 = 1073741824.0;
        const double TurnToRad = 2 * Math.PI / 4294967296.0;
        static readonly fAngle Tolerance = fAngle.FromRaw(64); // ~9e-8 rad

        static double NormError(fQuaternion q) => Math.Abs(Math.Sqrt(q.LengthSquaredWide / (Q30 * Q30)) - 1.0);

        static void AssertRotation(fQuaternion actual, fQuaternion expected, uint toleranceRaw = 64)
        {
            Assert.That(fQuaternion.Angle(actual, expected).RawValue, Is.LessThanOrEqualTo(toleranceRaw), $"{actual} vs {expected}");
        }

        static void AssertVector(fVector3 actual, fVector3 expected, int toleranceRaw = 2)
        {
            Assert.That(fVector3.Approximately(actual, expected, ffloat.FromRaw(toleranceRaw)), Is.True, $"{actual} vs {expected}");
        }

        static fQuaternion RandomRotation(ref SplitMix64 rng)
        {
            while (true)
            {
                fVector3 axis = fVector3.FromRaw(rng.Range(-65536, 65536), rng.Range(-65536, 65536), rng.Range(-65536, 65536));
                if (fQuaternion.TryAngleAxis(fAngle.FromRaw((uint)rng.Next()), axis, out fQuaternion q))
                    return q;
            }
        }

        [Test]
        public void IdentityAndLayout()
        {
            Assert.That(fQuaternion.identity.w, Is.EqualTo(funit.One));
            Assert.That(fQuaternion.identity.IsNormalized, Is.True);
            Assert.That(default(fQuaternion).IsNormalized, Is.False);
            Assert.That(fQuaternion.identity * fVector3.FromInt(1, 2, 3), Is.EqualTo(fVector3.FromInt(1, 2, 3)));
            Assert.That(fQuaternion.identity * fQuaternion.identity, Is.EqualTo(fQuaternion.identity));
        }

        [Test]
        public void AxisRotations90And180()
        {
            fVector3 v = fVector3.FromInt(10, 20, 30);
            fQuaternion y90 = fQuaternion.AngleAxis(fAngle.QuarterTurn, fUnitVector3.up);
            AssertVector(y90 * fVector3.FromInt(10, 0, 0), fVector3.FromInt(0, 0, -10), 0);
            fQuaternion x90 = fQuaternion.AngleAxis(fAngle.QuarterTurn, fUnitVector3.right);
            AssertVector(x90 * fVector3.FromInt(0, 10, 0), fVector3.FromInt(0, 0, 10), 0);
            fQuaternion z90 = fQuaternion.AngleAxis(fAngle.QuarterTurn, fUnitVector3.forward);
            AssertVector(z90 * fVector3.FromInt(10, 0, 0), fVector3.FromInt(0, 10, 0), 0);

            AssertVector(fQuaternion.AngleAxis(fAngle.HalfTurn, fUnitVector3.up) * v, fVector3.FromInt(-10, 20, -30), 0);
            AssertVector(fQuaternion.AngleAxis(fAngle.HalfTurn, fUnitVector3.right) * v, fVector3.FromInt(10, -20, -30), 0);
            AssertVector(fQuaternion.AngleAxis(fAngle.HalfTurn, fUnitVector3.forward) * v, fVector3.FromInt(-10, -20, 30), 0);

            // 360 degrees: raw angle wraps to 0 -> identity
            Assert.That(fQuaternion.AngleAxis(fAngle.FromDegrees(360), fUnitVector3.up), Is.EqualTo(fQuaternion.identity));
            // -90 (signed) == 270 (unsigned) as a rotation
            fQuaternion neg = fQuaternion.AngleAxis(fAngleDelta.MinusQuarterTurn, fUnitVector3.up);
            fQuaternion pos = fQuaternion.AngleAxis(fAngle.ThreeQuarterTurn, fUnitVector3.up);
            Assert.That(fQuaternion.IsSameRotation(neg, pos), Is.True);
            AssertVector(neg * fVector3.FromInt(10, 0, 0), fVector3.FromInt(0, 0, 10), 0);
        }

        [Test]
        public void AngleAxisIsNormalized()
        {
            var rng = new SplitMix64(41);
            for (int i = 0; i < 5000; i++)
            {
                fQuaternion q = RandomRotation(ref rng);
                Assert.That(NormError(q), Is.LessThan(3e-9));
            }
            Assert.That(fQuaternion.TryAngleAxis(fAngle.QuarterTurn, fVector3.zero, out _), Is.False);
            Assert.That(fQuaternion.TryAngleAxis(fAngle.QuarterTurn, default(fUnitVector3), out _), Is.False);
            // non-unit axis vector is normalized first (v1 produced |q|^2 = 1.75)
            Assert.That(fQuaternion.TryAngleAxis(fAngle.FromDegrees(60), fVector3.FromInt(0, 3, 0), out fQuaternion q60), Is.True);
            Assert.That(NormError(q60), Is.LessThan(3e-9));
        }

        [Test]
        public void RotationPreservesLength()
        {
            var rng = new SplitMix64(42);
            for (int i = 0; i < 5000; i++)
            {
                fQuaternion q = RandomRotation(ref rng);
                fVector3 v = fVector3.FromRaw(rng.Range(-(2048 << 16), 2048 << 16), rng.Range(-(2048 << 16), 2048 << 16), rng.Range(-(2048 << 16), 2048 << 16));
                long before = v.magnitude.RawValue, after = (q * v).magnitude.RawValue;
                Assert.That(after, Is.EqualTo(before).Within(3), $"{q} {v}");
            }
        }

        [Test]
        public void RotationMatchesDoubleReference()
        {
            var rng = new SplitMix64(43);
            for (int i = 0; i < 2000; i++)
            {
                fQuaternion q = RandomRotation(ref rng);
                fVector3 v = fVector3.FromRaw(rng.Range(-(500 << 16), 500 << 16), rng.Range(-(500 << 16), 500 << 16), rng.Range(-(500 << 16), 500 << 16));
                double qx = q.x.ToDouble(), qy = q.y.ToDouble(), qz = q.z.ToDouble(), qw = q.w.ToDouble();
                double vx = v.x.ToDouble(), vy = v.y.ToDouble(), vz = v.z.ToDouble();
                // v' = v + 2w (q x v) + 2 q x (q x v)
                double cx = qy * vz - qz * vy, cy = qz * vx - qx * vz, cz = qx * vy - qy * vx;
                double ex = vx + 2 * (qw * cx + qy * cz - qz * cy);
                double ey = vy + 2 * (qw * cy + qz * cx - qx * cz);
                double ez = vz + 2 * (qw * cz + qx * cy - qy * cx);
                fVector3 r = q * v;
                Assert.That(r.x.ToDouble(), Is.EqualTo(ex).Within(3.0 / 65536));
                Assert.That(r.y.ToDouble(), Is.EqualTo(ey).Within(3.0 / 65536));
                Assert.That(r.z.ToDouble(), Is.EqualTo(ez).Within(3.0 / 65536));
            }
        }

        [Test]
        public void MultiplyComposesRotations()
        {
            fQuaternion a = fQuaternion.AngleAxis(fAngle.FromDegrees(30), fUnitVector3.up);
            fQuaternion b = fQuaternion.AngleAxis(fAngle.FromDegrees(60), fUnitVector3.up);
            AssertRotation(a * b, fQuaternion.AngleAxis(fAngle.QuarterTurn, fUnitVector3.up));

            var rng = new SplitMix64(44);
            for (int i = 0; i < 2000; i++)
            {
                fQuaternion p = RandomRotation(ref rng), q = RandomRotation(ref rng);
                fVector3 v = fVector3.FromInt(rng.Range(-100, 100), rng.Range(-100, 100), rng.Range(-100, 100));
                AssertVector((p * q) * v, p * (q * v), 3);
                Assert.That(NormError(p * q), Is.LessThan(5e-9));
            }
        }

        [Test]
        public void InverseAndConjugate()
        {
            var rng = new SplitMix64(45);
            for (int i = 0; i < 2000; i++)
            {
                fQuaternion q = RandomRotation(ref rng);
                AssertRotation(q * fQuaternion.Inverse(q), fQuaternion.identity, 16);
                fVector3 v = fVector3.FromInt(rng.Range(-100, 100), rng.Range(-100, 100), rng.Range(-100, 100));
                AssertVector(fQuaternion.Inverse(q) * (q * v), v, 3);
            }
            // general quaternion with |q| = 0.5: the inverse has w = 2, outside Q1.30
            Assert.That(fQuaternion.TryInverse(fQuaternion.FromRawUnchecked(0, 0, 0, 1 << 29), out _), Is.False);
        }

        [Test]
        public void TryInverseGeneral()
        {
            fQuaternion scaled = fQuaternion.FromRawUnchecked(0, 0, (int)(0.6 * 0.9 * Q30), (int)(0.8 * 0.9 * Q30)); // |q| = 0.9
            Assert.That(fQuaternion.TryInverse(scaled, out fQuaternion inv), Is.True);
            Assert.That(inv.z.ToDouble(), Is.EqualTo(-0.6 / 0.9).Within(1e-8));
            Assert.That(inv.w.ToDouble(), Is.EqualTo(0.8 / 0.9).Within(1e-8));
            Assert.That(fQuaternion.TryInverse(default(fQuaternion), out fQuaternion zero), Is.False);
            Assert.That(zero, Is.EqualTo(fQuaternion.identity));
            Assert.That(fQuaternion.TryInverse(fQuaternion.FromRawUnchecked(0, 0, 0, 1 << 28), out _), Is.False); // 1/0.25 = 4 does not fit
        }

        [Test]
        public void QAndMinusQAreTheSameRotation()
        {
            fQuaternion q = fQuaternion.AngleAxis(fAngle.FromDegrees(70), fUnitVector3.right);
            fQuaternion n = fQuaternion.FromRawUnchecked(-q.x.RawValue, -q.y.RawValue, -q.z.RawValue, -q.w.RawValue);
            Assert.That(q == n, Is.False);
            Assert.That(fQuaternion.IsSameRotation(q, n), Is.True);
            Assert.That(fQuaternion.Angle(q, n), Is.EqualTo(fAngle.Zero));
            Assert.That(q * fVector3.FromInt(3, 4, 5), Is.EqualTo(n * fVector3.FromInt(3, 4, 5)));
        }

        [Test]
        public void AngleBetweenRotations()
        {
            fQuaternion a = fQuaternion.AngleAxis(fAngle.FromDegrees(10), fUnitVector3.up);
            fQuaternion b = fQuaternion.AngleAxis(fAngle.FromDegrees(50), fUnitVector3.up);
            Assert.That(fQuaternion.Angle(a, b).RawValue, Is.EqualTo(fAngle.FromDegrees(40).RawValue).Within(64));
            Assert.That(fQuaternion.Angle(a, a), Is.EqualTo(fAngle.Zero));
            fQuaternion tiny = fQuaternion.AngleAxis(fAngle.FromRaw(20000), fUnitVector3.up);
            Assert.That(fQuaternion.Angle(fQuaternion.identity, tiny).RawValue, Is.EqualTo(20000u).Within(16));
        }

        [Test]
        public void FromToRotation()
        {
            // same -> identity
            Assert.That(fQuaternion.FromToRotation(fUnitVector3.up, fUnitVector3.up), Is.EqualTo(fQuaternion.identity));
            // 90
            fQuaternion q = fQuaternion.FromToRotation(fUnitVector3.right, fUnitVector3.up);
            AssertRotation(q, fQuaternion.AngleAxis(fAngle.QuarterTurn, fUnitVector3.forward));
            // opposite -> exact half turn around a perpendicular axis (v1 mixed radians into degrees)
            foreach (fUnitVector3 d in new[] { fUnitVector3.right, fUnitVector3.up, fUnitVector3.forward, fUnitVector3.left })
            {
                fQuaternion h = fQuaternion.FromToRotation(d, -d);
                Assert.That(h.w, Is.EqualTo(funit.Zero));
                Assert.That(h * d, Is.EqualTo(-d));
                Assert.That(fQuaternion.Angle(fQuaternion.identity, h), Is.EqualTo(fAngle.HalfTurn));
            }
            // random directions, including nearly opposite
            var rng = new SplitMix64(46);
            for (int i = 0; i < 3000; i++)
            {
                fVector3 a = fVector3.FromRaw(rng.Range(-65536, 65536), rng.Range(-65536, 65536), rng.Range(-65536, 65536));
                fVector3 b = (i % 3 == 0) ? -a + fVector3.FromRaw(rng.Range(-40, 40), rng.Range(-40, 40), rng.Range(-40, 40)) : fVector3.FromRaw(rng.Range(-65536, 65536), rng.Range(-65536, 65536), rng.Range(-65536, 65536));
                if (!fVector3.TryNormalize(a, out fUnitVector3 ua) || !fVector3.TryNormalize(b, out fUnitVector3 ub)) continue;
                fQuaternion r = fQuaternion.FromToRotation(ua, ub);
                Assert.That(NormError(r), Is.LessThan(3e-9));
                Assert.That(fUnitVector3.AngleUsingAtan2(r * ua, ub).RawValue, Is.LessThan(256u), $"{ua} -> {ub}"); // < 4e-7 rad, including nearly opposite inputs
            }
            Assert.That(fQuaternion.TryFromToRotation(fVector3.zero, fVector3.up, out _), Is.False);
            Assert.That(fQuaternion.TryFromToRotation(fVector3.FromInt(0, 0, 5), fVector3.FromInt(3, 0, 0), out fQuaternion ft), Is.True);
            AssertVector(ft * fVector3.FromInt(0, 0, 3), fVector3.FromInt(3, 0, 0), 1);
        }

        [Test]
        public void LookRotationBuildsOrthonormalBasis()
        {
            Assert.That(fQuaternion.TryLookRotation(fVector3.forward, fVector3.up, out fQuaternion id), Is.True);
            Assert.That(id, Is.EqualTo(fQuaternion.identity));

            var rng = new SplitMix64(47);
            for (int i = 0; i < 3000; i++)
            {
                fVector3 f = fVector3.FromRaw(rng.Range(-65536, 65536), rng.Range(-65536, 65536), rng.Range(-65536, 65536));
                fVector3 u = fVector3.FromRaw(rng.Range(-65536, 65536), rng.Range(-65536, 65536), rng.Range(-65536, 65536));
                if (!fQuaternion.TryLookRotation(f, u, out fQuaternion q)) continue;
                Assert.That(NormError(q), Is.LessThan(3e-9));
                fVector3.TryNormalize(f, out fUnitVector3 uf);
                Assert.That(fUnitVector3.AngleUsingAtan2(q * fUnitVector3.forward, uf).RawValue, Is.LessThan(64u));
                // rotated up lies in the plane of (forward, up) on the up side
                fUnitVector3 ru = q * fUnitVector3.up;
                fVector3.TryNormalize(u, out fUnitVector3 uu);
                if (fUnitVector3.TryNormalizedCross(uu, uf, out fUnitVector3 right))
                {
                    Assert.That(Math.Abs(fUnitVector3.DotWide(ru, right)), Is.LessThan(1L << 36));
                    Assert.That(fUnitVector3.DotWide(ru, uu), Is.GreaterThanOrEqualTo(0L));
                }
            }
            // v1 regression: basis not normalized for non-orthogonal up
            Assert.That(fQuaternion.TryLookRotation(fVector3.FromInt(1, 0, 1), fVector3.FromInt(0, 1, 1), out fQuaternion lr), Is.True);
            Assert.That(NormError(lr), Is.LessThan(3e-9));
            // parallel up -> deterministic FromTo(+Z, forward)
            Assert.That(fQuaternion.TryLookRotation(fVector3.up, fVector3.up, out fQuaternion par), Is.True);
            Assert.That(par, Is.EqualTo(fQuaternion.FromToRotation(fUnitVector3.forward, fUnitVector3.up)));
            Assert.That(fQuaternion.TryLookRotation(fVector3.zero, fVector3.up, out _), Is.False);
        }

        [Test]
        public void LerpNormalizes()
        {
            fQuaternion a = fQuaternion.AngleAxis(fAngle.FromDegrees(0), fUnitVector3.up);
            fQuaternion b = fQuaternion.AngleAxis(fAngle.FromDegrees(90), fUnitVector3.up);
            fQuaternion m = fQuaternion.Lerp(a, b, ffloat.Half);
            Assert.That(NormError(m), Is.LessThan(3e-9));
            AssertRotation(m, fQuaternion.AngleAxis(fAngle.FromDegrees(45), fUnitVector3.up));
            // shorter arc through -b
            fQuaternion nb = fQuaternion.FromRawUnchecked(-b.x.RawValue, -b.y.RawValue, -b.z.RawValue, -b.w.RawValue);
            AssertRotation(fQuaternion.Lerp(a, nb, ffloat.Half), fQuaternion.AngleAxis(fAngle.FromDegrees(45), fUnitVector3.up));
        }

        [Test]
        public void Slerp()
        {
            fQuaternion a = fQuaternion.AngleAxis(fAngle.FromDegrees(10), fUnitVector3.up);
            fQuaternion b = fQuaternion.AngleAxis(fAngle.FromDegrees(130), fUnitVector3.up);
            Assert.That(fQuaternion.Slerp(a, b, ffloat.Zero), Is.EqualTo(a));
            Assert.That(fQuaternion.IsSameRotation(fQuaternion.Slerp(a, b, ffloat.One), b), Is.True);
            AssertRotation(fQuaternion.Slerp(a, b, ffloat.Half), fQuaternion.AngleAxis(fAngle.FromDegrees(70), fUnitVector3.up));
            AssertRotation(fQuaternion.Slerp(a, b, ffloat.FromFraction(1, 4)), fQuaternion.AngleAxis(fAngle.FromDegrees(40), fUnitVector3.up));

            // negative dot: all four components are flipped (v1 conjugated xyz only)
            fQuaternion nb = fQuaternion.FromRawUnchecked(-b.x.RawValue, -b.y.RawValue, -b.z.RawValue, -b.w.RawValue);
            fQuaternion viaNeg = fQuaternion.Slerp(a, nb, ffloat.Half);
            Assert.That(NormError(viaNeg), Is.LessThan(3e-9));
            AssertRotation(viaNeg, fQuaternion.AngleAxis(fAngle.FromDegrees(70), fUnitVector3.up));

            // very close: nlerp fallback, still accurate and normalized
            fQuaternion c = fQuaternion.AngleAxis(fAngle.FromRaw(10000), fUnitVector3.right);
            fQuaternion mid = fQuaternion.Slerp(fQuaternion.identity, c, ffloat.Half);
            AssertRotation(mid, fQuaternion.AngleAxis(fAngle.FromRaw(5000), fUnitVector3.right), 8);
            Assert.That(NormError(mid), Is.LessThan(3e-9));

            // constant angular speed along the arc
            fQuaternion prev = a;
            for (int i = 1; i <= 16; i++)
            {
                fQuaternion s = fQuaternion.Slerp(a, b, ffloat.FromFraction(i, 16));
                Assert.That(fQuaternion.Angle(prev, s).RawValue, Is.EqualTo(fAngle.FromDegreesFraction(120, 16).RawValue).Within(256));
                prev = s;
            }
        }

        [Test]
        public void RotateTowards()
        {
            fQuaternion a = fQuaternion.identity;
            fQuaternion b = fQuaternion.AngleAxis(fAngle.FromDegrees(90), fUnitVector3.up);
            fQuaternion step = fQuaternion.RotateTowards(a, b, fAngle.FromDegrees(30));
            Assert.That(fQuaternion.Angle(a, step).RawValue, Is.EqualTo(fAngle.FromDegrees(30).RawValue).Within(64));
            Assert.That(fQuaternion.RotateTowards(a, b, fAngle.FromDegrees(120)), Is.EqualTo(b));
        }

        [Test]
        public void EulerRoundTrip()
        {
            fQuaternion e = fQuaternion.Euler(fAngle.FromDegrees(30), fAngle.Zero, fAngle.Zero);
            fQuaternion.ToEuler(e, out fAngle ex, out fAngle ey, out fAngle ez);
            Assert.That(ex.RawValue, Is.EqualTo(fAngle.FromDegrees(30).RawValue).Within(64));
            Assert.That(ey, Is.EqualTo(fAngle.Zero));
            Assert.That(ez, Is.EqualTo(fAngle.Zero));

            // Unity convention: y applied last (world), z first
            fQuaternion yx = fQuaternion.Euler(fAngle.FromDegrees(90), fAngle.FromDegrees(90), fAngle.Zero);
            AssertVector(yx * fVector3.FromInt(0, 0, 10), fVector3.FromInt(0, -10, 0), 1);

            var rng = new SplitMix64(48);
            for (int i = 0; i < 3000; i++)
            {
                fAngle x = fAngle.FromDegrees(rng.Range(-89, 89)), y = fAngle.FromRaw((uint)rng.Next()), z = fAngle.FromRaw((uint)rng.Next());
                fQuaternion q = fQuaternion.Euler(x, y, z);
                fQuaternion.ToEuler(q, out fAngle rx, out fAngle ry, out fAngle rz);
                AssertRotation(fQuaternion.Euler(rx, ry, rz), q, 256);
            }

            // gimbal lock: every output is an fAngle (v1 returned radians here and degrees elsewhere)
            fQuaternion g = fQuaternion.Euler(fAngle.QuarterTurn, fAngle.FromDegrees(20), fAngle.FromDegrees(10));
            fQuaternion.ToEuler(g, out fAngle gx, out fAngle gy, out fAngle gz);
            Assert.That(gx.RawValue, Is.EqualTo(fAngle.QuarterTurn.RawValue).Within(4096));
            Assert.That(gz, Is.EqualTo(fAngle.Zero));
            AssertRotation(fQuaternion.Euler(gx, gy, gz), g, 8192);
        }

        [Test]
        public void RotateUnitVector()
        {
            fQuaternion q = fQuaternion.AngleAxis(fAngle.QuarterTurn, fUnitVector3.up);
            Assert.That(q * fUnitVector3.right, Is.EqualTo(fUnitVector3.back));
            var rng = new SplitMix64(49);
            for (int i = 0; i < 1000; i++)
            {
                fQuaternion r = RandomRotation(ref rng);
                fVector3.TryNormalize(fVector3.FromInt(rng.Range(-9, 9), rng.Range(-9, 9), 10), out fUnitVector3 d);
                Assert.That((r * d).IsValid, Is.True);
            }
        }

        [Test]
        public void NormalizeAndValidation()
        {
            Assert.That(fQuaternion.TryNormalize(default(fQuaternion), out fQuaternion n), Is.False);
            Assert.That(n, Is.EqualTo(fQuaternion.identity));
            fMathValidation.ResetCounts();
            Assert.That(fQuaternion.Normalize(default(fQuaternion)), Is.EqualTo(fQuaternion.identity));
            if (fMathValidation.IsEnabled)
                Assert.That(fMathValidation.GetCount(fMathValidationKind.InvalidQuaternion), Is.EqualTo(1));
            Assert.That(fQuaternion.TryFromRaw(0, 0, 0, 5, out fQuaternion w), Is.True);
            Assert.That(w, Is.EqualTo(fQuaternion.identity));
        }
    }
}
