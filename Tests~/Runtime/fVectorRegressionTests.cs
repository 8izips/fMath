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
        [Category("ReviewFixes")]
        public void WithinAngle_SameRoundedDirectionIsWithinZero()
        {
            fVector3.TryNormalize(fVector3.FromInt(1, 1, 1), out fUnitVector3 u);
            Assert.That(fUnitVector3.WithinAngle(u, u, fAngle.Zero), Is.True);
            Assert.That(fVector3.WithinAngle(fVector3.FromInt(1, 1, 1), fVector3.FromInt(2, 2, 2), fAngle.Zero), Is.True);
            fUnitVector2 v = fUnitVector2.FromAngle(fAngle.FromRaw(1u << 17));
            Assert.That(fUnitVector2.WithinAngle(v, v, fAngle.Zero), Is.True);
        }

        [Test]
        [Category("ReviewFixes")]
        public void WithinAngle_RejectsTinyNonzeroAnglesAndKeepsEndpoints()
        {
            fUnitVector3.TryFromRaw(funit.RawOne, 1024, 0, out fUnitVector3 tiny3);
            fUnitVector2.TryFromRaw(funit.RawOne, 1024, out fUnitVector2 tiny2);
            Assert.That(fUnitVector3.WithinAngle(fUnitVector3.right, tiny3, fAngle.Zero), Is.False);
            Assert.That(fUnitVector2.WithinAngle(fUnitVector2.right, tiny2, fAngle.Zero), Is.False);
            Assert.That(fUnitVector3.WithinAngle(fUnitVector3.right, tiny3, fAngle.FromDegreesFraction(1, 1000)), Is.True);
            Assert.That(fUnitVector2.WithinAngle(fUnitVector2.right, tiny2, fAngle.FromDegreesFraction(1, 1000)), Is.True);
            Assert.That(fUnitVector3.WithinAngle(fUnitVector3.right, fUnitVector3.up, fAngle.QuarterTurn), Is.True);
            Assert.That(fUnitVector2.WithinAngle(fUnitVector2.right, fUnitVector2.up, fAngle.QuarterTurn), Is.True);
            Assert.That(fUnitVector3.WithinAngle(fUnitVector3.right, fUnitVector3.left, fAngle.HalfTurn), Is.True);
            Assert.That(fUnitVector2.WithinAngle(fUnitVector2.right, fUnitVector2.left, fAngle.HalfTurn), Is.True);
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
