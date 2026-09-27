using NUnit.Framework;

namespace fMath.Tests
{
    [TestFixture]
    public class funitTests
    {
        [Test]
        public void Format_IsQ1_30()
        {
            Assert.That(funit.One.RawValue, Is.EqualTo(1 << 30));
            Assert.That(funit.MinusOne.RawValue, Is.EqualTo(-(1 << 30)));
            Assert.That(funit.Zero.RawValue, Is.EqualTo(0));
            Assert.That(funit.FromRaw(123).RawValue, Is.EqualTo(123));
        }

        [Test]
        public void ConversionsAndArithmetic()
        {
            Assert.That(funit.FromFfloat(ffloat.Half), Is.EqualTo(funit.Half));
            Assert.That(funit.Half.ToFfloat(), Is.EqualTo(ffloat.Half));
            Assert.That(funit.FromFraction(1, 3).RawValue, Is.EqualTo(357913941));
            Assert.That(funit.FromRaw(1 << 13).ToFfloat().RawValue, Is.EqualTo(0));  // 0.5 raw Q16 -> even
            Assert.That(funit.FromRaw(3 << 13).ToFfloat().RawValue, Is.EqualTo(2));  // 1.5 raw Q16 -> even
            Assert.That(funit.Half * funit.Half, Is.EqualTo(funit.FromFraction(1, 4)));
            Assert.That(funit.Half * ffloat.FromInt(10), Is.EqualTo(ffloat.FromInt(5)));
            Assert.That(ffloat.FromInt(-10) * funit.Half, Is.EqualTo(ffloat.FromInt(-5)));
            Assert.That(-funit.One, Is.EqualTo(funit.MinusOne));
            Assert.That(-funit.FromRaw(int.MinValue), Is.EqualTo(funit.FromRaw(int.MaxValue)));
            Assert.That(funit.One + funit.One, Is.EqualTo(funit.FromRaw(int.MaxValue))); // saturates at ~2
            Assert.That(funit.ClampUnit(funit.FromRaw(int.MaxValue)), Is.EqualTo(funit.One));
            Assert.That(funit.FromRaw((1 << 30) + 1).IsInUnitRange, Is.False);
            Assert.That(funit.One.IsInUnitRange, Is.True);
        }

        [Test]
        public void OutOfRangeIsReported()
        {
            fMathValidation.ResetCounts();
            funit.FromFfloat(ffloat.Two);
            if (fMathValidation.IsEnabled)
                Assert.That(fMathValidation.GetCount(fMathValidationKind.UnitOutOfRange), Is.EqualTo(1));
        }
    }

    [TestFixture]
    public class fAngleTests
    {
        [Test]
        public void Constants()
        {
            Assert.That(fAngle.Zero.RawValue, Is.EqualTo(0u));
            Assert.That(fAngle.QuarterTurn.RawValue, Is.EqualTo(0x40000000u));
            Assert.That(fAngle.HalfTurn.RawValue, Is.EqualTo(0x80000000u));
            Assert.That(fAngle.ThreeQuarterTurn.RawValue, Is.EqualTo(0xC0000000u));
        }

        [Test]
        public void Factories()
        {
            Assert.That(fAngle.FromDegrees(90), Is.EqualTo(fAngle.QuarterTurn));
            Assert.That(fAngle.FromDegrees(180), Is.EqualTo(fAngle.HalfTurn));
            Assert.That(fAngle.FromDegrees(-90), Is.EqualTo(fAngle.ThreeQuarterTurn));
            Assert.That(fAngle.FromDegrees(450), Is.EqualTo(fAngle.QuarterTurn));
            Assert.That(fAngle.FromDegrees(360), Is.EqualTo(fAngle.Zero));
            Assert.That(fAngle.FromDegrees(1).RawValue, Is.EqualTo(11930465u)); // 2^32/360 = 11930464.7
            Assert.That(fAngle.FromTurnsFraction(1, 8).RawValue, Is.EqualTo(0x20000000u));
            Assert.That(fAngle.FromTurnsFraction(-1, 8).RawValue, Is.EqualTo(0xE0000000u));
            Assert.That(fAngle.FromDegreesFraction(45, 2), Is.EqualTo(fAngle.FromTurnsFraction(1, 16)));
            Assert.That(fAngle.FromTurnsFraction(int.MaxValue, 3), Is.EqualTo(fAngle.FromTurnsFraction(1, 3)));
            Assert.That(fAngle.FromTurnsFraction(1, 0), Is.EqualTo(fAngle.Zero));
        }

        [Test]
        public void ArithmeticWraps()
        {
            Assert.That(fAngle.ThreeQuarterTurn + fAngle.HalfTurn, Is.EqualTo(fAngle.QuarterTurn));
            Assert.That(fAngle.Zero - fAngleDelta.QuarterTurn, Is.EqualTo(fAngle.ThreeQuarterTurn));
            Assert.That(-fAngle.QuarterTurn, Is.EqualTo(fAngle.ThreeQuarterTurn));
            Assert.That(fAngle.FromRaw(0xFFFFFFFFu) + fAngle.FromRaw(1u), Is.EqualTo(fAngle.Zero));
            Assert.That(fAngle.QuarterTurn.Sub(fAngle.HalfTurn), Is.EqualTo(fAngle.ThreeQuarterTurn));
            Assert.That(fAngle.ThreeQuarterTurn.Half, Is.EqualTo(fAngle.FromDegrees(135)));
            Assert.That(fAngle.QuarterTurn * ffloat.Half, Is.EqualTo(fAngle.FromDegrees(45)));
        }

        [Test]
        public void SignedDelta_IsShortestArc()
        {
            Assert.That(fAngle.FromDegrees(10).SignedDeltaTo(fAngle.FromDegrees(350)), Is.EqualTo(fAngleDelta.FromDegrees(-20)));
            Assert.That(fAngle.FromDegrees(350).SignedDeltaTo(fAngle.FromDegrees(10)), Is.EqualTo(fAngleDelta.FromDegrees(20)));
            Assert.That(fAngle.Zero.SignedDeltaTo(fAngle.HalfTurn), Is.EqualTo(fAngleDelta.HalfTurn));
            Assert.That(fAngle.FromDegrees(90) - fAngle.FromDegrees(30), Is.EqualTo(fAngleDelta.FromDegrees(60)));
            Assert.That(fAngle.FromDegrees(10).ToDelta(), Is.EqualTo(fAngleDelta.FromDegrees(10)));
            Assert.That(fAngle.FromDegrees(350).ToDelta(), Is.EqualTo(fAngleDelta.FromDegrees(-10)));
        }

        [Test]
        public void MoveTowardsAndApproximately()
        {
            fAngle a = fAngle.MoveTowards(fAngle.FromDegrees(350), fAngle.FromDegrees(20), fAngleDelta.FromDegrees(5));
            Assert.That(a, Is.EqualTo(fAngle.FromDegrees(350) + fAngleDelta.FromDegrees(5)));
            a = fAngle.MoveTowards(fAngle.FromDegrees(350), fAngle.FromDegrees(352), fAngleDelta.FromDegrees(5));
            Assert.That(a, Is.EqualTo(fAngle.FromDegrees(352)));
            Assert.That(fAngle.Approximately(fAngle.FromDegrees(359), fAngle.FromDegrees(1), fAngleDelta.FromDegreesFraction(201, 100)), Is.True);
            Assert.That(fAngle.Approximately(fAngle.FromDegrees(359), fAngle.FromDegrees(2), fAngleDelta.FromDegrees(2)), Is.False);
        }

        [Test]
        public void Delta()
        {
            Assert.That(fAngleDelta.HalfTurn.AbsRaw, Is.EqualTo(0x80000000u));
            Assert.That(-fAngleDelta.HalfTurn, Is.EqualTo(fAngleDelta.HalfTurn)); // +180 wraps to -180
            Assert.That(fAngleDelta.FromDegrees(-30).AbsRaw, Is.EqualTo(fAngleDelta.FromDegrees(30).AbsRaw));
            Assert.That(fAngleDelta.FromDegrees(100) + fAngleDelta.FromDegrees(100), Is.EqualTo(fAngleDelta.FromDegrees(-160)));
            Assert.That(fAngleDelta.FromDegrees(90) * ffloat.Half, Is.EqualTo(fAngleDelta.FromDegrees(45)));
            Assert.That(fAngleDelta.FromDegrees(-90).Half, Is.EqualTo(fAngleDelta.FromDegrees(-45)));
            Assert.That(fAngleDelta.ClampMagnitude(fAngleDelta.FromDegrees(-50), fAngleDelta.FromDegrees(10)), Is.EqualTo(fAngleDelta.FromDegrees(-10)));
            Assert.That(fAngleDelta.ClampMagnitude(fAngleDelta.FromDegrees(5), fAngleDelta.FromDegrees(10)), Is.EqualTo(fAngleDelta.FromDegrees(5)));
            Assert.That(fAngleDelta.FromDegrees(10) < fAngleDelta.FromDegrees(20), Is.True);
        }
    }
}
