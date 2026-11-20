using System;
using NUnit.Framework;

namespace fMath.Tests
{
    /// <summary>v1 quaternion defects, asserting the corrected v2 behaviour.</summary>
    [TestFixture]
    public class fQuaternionRegressionTests
    {
        static double LengthSquared(fQuaternion q) => q.LengthSquaredWide / Math.Pow(2, 60);

        [Test]
        public void AngleAxis_ProducesUnitQuaternion()
        {
            // v1: AngleAxis(60, (0,3,0)) had |q|^2 = 1.75
            Assert.That(fQuaternion.TryAngleAxis(fAngle.FromDegrees(60), fVector3.FromInt(0, 3, 0), out fQuaternion q), Is.True);
            Assert.That(LengthSquared(q), Is.EqualTo(1.0).Within(1e-8));
        }

        [Test]
        public void Rotation_PreservesVectorLength()
        {
            // v1: a 90 degree rotation turned a 10 m vector into 17.4 m
            fQuaternion q = fQuaternion.AngleAxis(fAngle.QuarterTurn, fUnitVector3.up);
            Assert.That((q * fVector3.FromInt(10, 0, 0)).magnitude, Is.EqualTo(ffloat.FromInt(10)));
        }

        [Test]
        public void FromToRotation180_IsAHalfTurn()
        {
            // v1 passed Pi (radians) into the degree-based AngleAxis
            fQuaternion q = fQuaternion.FromToRotation(fUnitVector3.right, fUnitVector3.left);
            Assert.That(q * fVector3.FromInt(1, 0, 0), Is.EqualTo(fVector3.FromInt(-1, 0, 0)));
            Assert.That(fQuaternion.Angle(fQuaternion.identity, q), Is.EqualTo(fAngle.HalfTurn));
        }

        [Test]
        public void Slerp_NegativeDotFlipsAllComponents()
        {
            // v1 conjugated xyz only and produced |q|^2 ~ 2
            fQuaternion a = fQuaternion.AngleAxis(fAngle.FromDegrees(10), fUnitVector3.up);
            fQuaternion b = fQuaternion.AngleAxis(fAngle.FromDegrees(20), fUnitVector3.up);
            fQuaternion negB = fQuaternion.FromRawUnchecked(-b.x.RawValue, -b.y.RawValue, -b.z.RawValue, -b.w.RawValue);
            fQuaternion s = fQuaternion.Slerp(a, negB, ffloat.Half);
            Assert.That(LengthSquared(s), Is.EqualTo(1.0).Within(1e-8));
            Assert.That(fQuaternion.Angle(s, fQuaternion.AngleAxis(fAngle.FromDegrees(15), fUnitVector3.up)).RawValue, Is.LessThan(64u));
        }

        [Test]
        public void LookRotation_BasisIsOrthonormal()
        {
            Assert.That(fQuaternion.TryLookRotation(fVector3.FromInt(1, 0, 1), fVector3.FromInt(0, 1, 1), out fQuaternion q), Is.True);
            Assert.That(LengthSquared(q), Is.EqualTo(1.0).Within(1e-8));
            fVector3.TryNormalize(fVector3.FromInt(1, 0, 1), out fUnitVector3 f);
            Assert.That(fUnitVector3.AngleUsingAtan2(q * fUnitVector3.forward, f).RawValue, Is.LessThan(64u));
        }

        [Test]
        public void ToEuler_ReturnsAnglesOnlyEvenAtGimbalLock()
        {
            // v1 returned HalfPi (radians) at the singularity and degrees elsewhere
            fQuaternion.ToEuler(fQuaternion.Euler(fAngle.QuarterTurn, fAngle.Zero, fAngle.Zero), out fAngle x, out fAngle y, out fAngle z);
            Assert.That(x.RawValue, Is.EqualTo(fAngle.QuarterTurn.RawValue).Within(4096));
            Assert.That(y, Is.EqualTo(fAngle.Zero));
            Assert.That(z, Is.EqualTo(fAngle.Zero));
        }
    }
}
