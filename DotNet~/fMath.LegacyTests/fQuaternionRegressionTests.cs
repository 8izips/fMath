using NUnit.Framework;

[TestFixture]
public class fQuaternionLegacyRegressionTests
{
    static double LengthSquared(fQuaternion q)
    {
        double x = q.x.ToDouble(), y = q.y.ToDouble(), z = q.z.ToDouble(), w = q.w.ToDouble();
        return x * x + y * y + z * z + w * w;
    }

    [Test]
    public void AngleAxis_ProducesNonUnitQuaternion()
    {
        var q = fQuaternion.AngleAxis((ffloat)60, new fVector3(0, 3, 0));
        Assert.That(LengthSquared(q), Is.GreaterThan(1.5));
    }

    [Test]
    public void Rotation_ChangesVectorLength()
    {
        var q = fQuaternion.AngleAxis((ffloat)90, fVector3.up);
        double len = (q * new fVector3(10, 0, 0)).magnitude.ToDouble();
        Assert.That(len, Is.GreaterThan(15.0), "legacy 90 degree rotation stretches a 10m vector");
    }

    [Test]
    public void FromToRotation180_MixesRadiansIntoDegreeApi()
    {
        var q = fQuaternion.FromToRotation(new fVector3(1, 0, 0), new fVector3(-1, 0, 0));
        var rotated = q * new fVector3(1, 0, 0);
        // AngleAxis(Pi) interprets 3.14 as degrees, so the result is nowhere near a half turn.
        Assert.That(System.Math.Abs(rotated.x.ToDouble() + 1.0), Is.GreaterThan(0.1));
    }

    [Test]
    public void Slerp_NegativeDotUsesConjugate()
    {
        var a = fQuaternion.AngleAxis((ffloat)10, fVector3.up);
        var b = fQuaternion.AngleAxis((ffloat)20, fVector3.up);
        var negB = new fQuaternion(-b.x, -b.y, -b.z, -b.w);
        var viaNeg = fQuaternion.Slerp(a, negB, ffloat.Half);
        // Conjugating only xyz produces a non-unit, wrong rotation.
        Assert.That(LengthSquared(viaNeg), Is.GreaterThan(1.5));
    }

    [Test]
    public void LookRotation_BasisIsNotNormalized()
    {
        var q = fQuaternion.LookRotation(new fVector3(1, 0, 1), new fVector3(0, 1, 1));
        Assert.That(System.Math.Abs(LengthSquared(q) - 1.0), Is.GreaterThan(0.05));
    }

    [Test]
    public void ToEuler_ReturnsRadiansAtGimbalLock()
    {
        // At the singularity ToEuler returns HalfPi (radians) for x while the regular path returns degrees.
        var q = new fQuaternion(ffloat.One, ffloat.Zero, ffloat.Zero, ffloat.One);
        var euler = fQuaternion.ToEuler(q);
        Assert.That(euler.y, Is.EqualTo(ffloat.HalfPi));
    }
}
