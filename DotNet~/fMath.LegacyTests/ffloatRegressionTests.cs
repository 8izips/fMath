using NUnit.Framework;

// Pins the numeric defects of the legacy (v1, Q20.12) ffloat so that the v2 rewrite can prove
// each one is fixed. These tests assert the *broken* behaviour on purpose.
[TestFixture]
public class ffloatLegacyRegressionTests
{
    [Test]
    public void TanOfQuarterPi_IsNotOne()
    {
        double tan = ffloat.Tan(ffloat.Pi / (ffloat)4).ToDouble();
        Assert.That(tan, Is.LessThan(0.1), "legacy Tan(Pi/4) collapses to ~0.05 instead of 1");
    }

    [Test]
    public void Sqrt_IsNotMonotonicAround4097()
    {
        ffloat s4097 = ffloat.Sqrt((ffloat)4097);
        ffloat s4098 = ffloat.Sqrt((ffloat)4098);
        Assert.That(s4097, Is.GreaterThan(s4098), "legacy Sqrt(4097) > Sqrt(4098)");

        int prev = -1;
        int failures = 0;
        for (int raw = 16760000; raw < 16900000; raw++)
        {
            int s = ffloat.Sqrt(ffloat.CreateFromRawValue(raw)).RawValue;
            if (s < prev)
                failures++;
            prev = s;
        }
        Assert.That(failures, Is.GreaterThan(0));
    }

    [Test]
    public void SqrtOfNegative_SilentlyReturnsZero()
    {
        Assert.That(ffloat.Sqrt(-ffloat.One), Is.EqualTo(ffloat.Zero));
    }

    [Test]
    public void AcosOfOne_IsNotZero()
    {
        Assert.That(ffloat.Acos(ffloat.One), Is.Not.EqualTo(ffloat.Zero));
    }
}
