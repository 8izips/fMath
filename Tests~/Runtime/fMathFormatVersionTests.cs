using System;
using NUnit.Framework;

namespace fMath.Tests
{
    [TestFixture]
    public class fMathFormatVersionTests
    {
        [Test]
        public void FormatVersion()
        {
            Assert.That(fMathFormatVersion.Current, Is.EqualTo(fMathFormatVersion.V2));
            Assert.That(fMathFormatVersion.IsCompatible(2), Is.True);
            Assert.That(fMathFormatVersion.IsCompatible(1), Is.False);
        }

        [Test]
        public void SessionHeader()
        {
            var local = new fDeterministicSessionHeader(3, 7, 0xABCDEF);
            Assert.That(local.MathFormatVersion, Is.EqualTo(fMathFormatVersion.Current));
            Assert.That(local.IsCompatibleWith(new fDeterministicSessionHeader(3, 7, 0xABCDEF)), Is.True);
            Assert.That(local.IsCompatibleWith(new fDeterministicSessionHeader(3, fMathFormatVersion.V1, 7, 0xABCDEF)), Is.False);
            Assert.That(local.IsCompatibleWith(new fDeterministicSessionHeader(4, 7, 0xABCDEF)), Is.False);
            Assert.That(local.IsCompatibleWith(new fDeterministicSessionHeader(3, 8, 0xABCDEF)), Is.False);
            Assert.That(local.IsCompatibleWith(new fDeterministicSessionHeader(3, 7, 0xABCDEE)), Is.False);
        }

    }
}
