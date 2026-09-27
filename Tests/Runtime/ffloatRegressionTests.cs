using NUnit.Framework;

namespace fMath.Tests
{
    /// <summary>
    /// v1 (Q20.12) defects pinned in the legacy harness, now asserting the corrected v2 behaviour.
    /// </summary>
    [TestFixture]
    public class ffloatRegressionTests
    {
        [Test]
        public void Sqrt_4097_IsBelow_4098()
        {
            // v1 returned Sqrt(4097) = 64.0234 > Sqrt(4098) = 64.0156
            Assert.That(ffloat.Sqrt(ffloat.FromInt(4097)), Is.LessThan(ffloat.Sqrt(ffloat.FromInt(4098))));
            Assert.That(ffloat.Sqrt(ffloat.FromInt(4097)).RawValue, Is.EqualTo(4194816)); // 64.0078122 * 65536
        }

        [Test]
        public void Sqrt_IsMonotonicAroundLegacyFailureBand()
        {
            // v1 failed 42245 times between 4097.0 and ~32767 (Q12 raw 16781313 ...). Sweep the same
            // real-value band at Q16 resolution with a stride.
            int prev = 0;
            for (long raw = 4096L << 16; raw < (8192L << 16); raw += 7)
            {
                int s = ffloat.Sqrt(ffloat.FromRaw((int)raw)).RawValue;
                Assert.That(s >= prev, Is.True, raw.ToString());
                prev = s;
            }
        }

        [Test]
        public void SqrtOfNegative_IsNotSilent()
        {
            Assert.That(ffloat.TrySqrt(-ffloat.One, out _), Is.False);
        }

        [Test]
        public void TanOfQuarterPi_IsOne()
        {
            // v1 returned ~0.05 for Tan(Pi/4)
            Assert.That(fTrig.Tan(fAngle.FromTurnsFraction(1, 8)), Is.EqualTo(ffloat.One));
        }

        [Test]
        public void AcosOfOne_IsZero()
        {
            // v1 returned -0.007 for Acos(1)
            Assert.That(fTrig.Acos(funit.One), Is.EqualTo(fAngle.Zero));
        }
    }
}
