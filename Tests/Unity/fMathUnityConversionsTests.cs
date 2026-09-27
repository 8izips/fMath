using fMath.Unity;
using NUnit.Framework;
using UnityEngine;

namespace fMath.Unity.Tests
{
    [TestFixture]
    public class fMathUnityConversionsTests
    {
        [Test]
        public void Scalars()
        {
            Assert.That(1.5f.ToFixed(), Is.EqualTo(ffloat.FromFraction(3, 2)));
            Assert.That((-0.25f).ToFixed().RawValue, Is.EqualTo(-16384));
            Assert.That(1e9f.ToFixed(), Is.EqualTo(ffloat.MaxValue));
            Assert.That(float.NaN.ToFixed(), Is.EqualTo(ffloat.Zero));
            Assert.That(ffloat.FromFraction(-7, 4).ToFloat(), Is.EqualTo(-1.75f));
            Assert.That(0.5f.ToUnit(), Is.EqualTo(funit.Half));
            Assert.That(3f.ToUnit(), Is.EqualTo(funit.One));
        }

        [Test]
        public void Angles()
        {
            Assert.That(fMathUnityConversions.DegreesToAngle(90f), Is.EqualTo(fAngle.QuarterTurn));
            Assert.That(fMathUnityConversions.DegreesToAngle(-90f), Is.EqualTo(fAngle.ThreeQuarterTurn));
            Assert.That(fMathUnityConversions.DegreesToAngle(720f), Is.EqualTo(fAngle.Zero));
            Assert.That(fAngle.HalfTurn.ToDegrees(), Is.EqualTo(180f));
            Assert.That(fAngleDelta.MinusQuarterTurn.ToDegrees(), Is.EqualTo(-90f));
        }

        [Test]
        public void Vectors()
        {
            Assert.That(new Vector3(1f, -2f, 0.5f).ToFixed(), Is.EqualTo(new fVector3(ffloat.One, ffloat.FromInt(-2), ffloat.Half)));
            Assert.That(fVector3.FromInt(3, 4, 5).ToVector3(), Is.EqualTo(new Vector3(3f, 4f, 5f)));
            Assert.That(new Vector3(0f, 0f, 1e-7f).TryToUnitVector(out fUnitVector3 tiny), Is.True);
            Assert.That(tiny, Is.EqualTo(fUnitVector3.forward));
            Assert.That(Vector3.zero.TryToUnitVector(out _), Is.False);
            Assert.That(new Vector3(1000f, 0f, 0f).TryToUnitVector(out fUnitVector3 right), Is.True);
            Assert.That(right, Is.EqualTo(fUnitVector3.right));
        }

        [Test]
        public void Quaternions()
        {
            Quaternion unity = Quaternion.AngleAxis(90f, Vector3.up);
            fQuaternion fixedQ = unity.ToFixed();
            Assert.That(fixedQ.IsNormalized, Is.True);
            Assert.That(fQuaternion.Angle(fixedQ, fQuaternion.AngleAxis(fAngle.QuarterTurn, fUnitVector3.up)).RawValue, Is.LessThan(2000u));
            Vector3 rotated = (fixedQ * fVector3.FromInt(10, 0, 0)).ToVector3();
            Vector3 expected = unity * new Vector3(10f, 0f, 0f);
            Assert.That((rotated - expected).magnitude, Is.LessThan(1e-4f));
            Assert.That(new Quaternion(0f, 0f, 0f, 0f).ToFixed(), Is.EqualTo(fQuaternion.identity));
            Quaternion back = fixedQ.ToQuaternion();
            Assert.That(Quaternion.Angle(back, unity), Is.LessThan(0.01f));
        }

        [Test]
        public void EulerMatchesUnityConvention()
        {
            Quaternion unity = Quaternion.Euler(30f, 50f, 70f);
            fQuaternion fixedQ = fQuaternion.Euler(fAngle.FromDegrees(30), fAngle.FromDegrees(50), fAngle.FromDegrees(70));
            Vector3 v = new Vector3(1f, 2f, 3f);
            Vector3 a = unity * v;
            Vector3 b = (fixedQ * v.ToFixed()).ToVector3();
            Assert.That((a - b).magnitude, Is.LessThan(1e-3f));
        }
    }
}
