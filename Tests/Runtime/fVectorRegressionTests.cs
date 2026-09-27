using NUnit.Framework;

namespace fMath.Tests
{
    /// <summary>v1 vector defects, asserting the corrected v2 behaviour.</summary>
    [TestFixture]
    public class fVectorRegressionTests
    {
        [Test]
        public void AngleOfVectorWithItself_IsZero()
        {
            var v = fVector3.FromInt(3, 4, 5);
            Assert.That(fVector3.Angle(v, v), Is.EqualTo(fAngle.Zero));
        }

        [Test]
        public void TinyVector_KeepsSquaredLengthAndNormalizes()
        {
            var tiny = fVector3.FromRaw(1, 1, 0);
            Assert.That(tiny.LengthSquaredWide, Is.EqualTo(2UL));
            Assert.That(tiny.TryNormalize(out fUnitVector3 u), Is.True);
            Assert.That(u.x, Is.EqualTo(u.y));
            Assert.That(u.IsValid, Is.True);
        }

        [Test]
        public void LargeVector_SquaredLengthDoesNotOverflow()
        {
            var big = fVector3.FromInt(1000, 0, 0);
            Assert.That(big.LengthSquaredWide, Is.EqualTo(1000000UL << 32));
            Assert.That(fVector3.FromInt(2048, 2048, 2048).magnitude.ToDouble(), Is.EqualTo(3547.2401).Within(1e-4));
        }

        [Test]
        public void VectorEquality_IsRawExact()
        {
            Assert.That(fVector3.zero == fVector3.FromInt(800, 0, 0), Is.False);
            Assert.That(fVector3.FromRaw(0, 0, 1) == fVector3.zero, Is.False);
        }
    }
}
