using NUnit.Framework;

[TestFixture]
public class fVectorLegacyRegressionTests
{
    [Test]
    public void AngleOfVectorWithItself_IsNotZero()
    {
        var v = new fVector3(3, 4, 5);
        Assert.That(fVector3.Angle(v, v), Is.Not.EqualTo(ffloat.Zero));
    }

    [Test]
    public void TinyVector_SquaredLengthCollapsesToZero()
    {
        var tiny = fVector3.CreateFromRawValue(1, 1, 0);
        Assert.That(tiny.sqrMagnitude, Is.EqualTo(ffloat.Zero));
        // normalize silently leaves the vector unnormalized
        Assert.That(tiny.normalized.x.RawValue, Is.EqualTo(1));
    }

    [Test]
    public void LargeVector_SquaredLengthOverflows()
    {
        var big = new fVector3(1000, 0, 0);
        Assert.That(big.sqrMagnitude, Is.LessThan(ffloat.Zero), "1000^2 overflows Q20.12 and wraps negative");
    }

    [Test]
    public void VectorEquality_IsAffectedByOverflow()
    {
        Assert.That(new fVector3(0, 0, 0) == new fVector3(800, 0, 0), Is.True);
    }
}
