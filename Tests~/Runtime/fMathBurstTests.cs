#if UNITY_6000_6_OR_NEWER
using System;
using NUnit.Framework;

public static class fMathBurstVerification
{
    public const int Cases = 100000;
    public static unsafe string Run()
    {
        var x = new ffloat[Cases + 2]; var v = new fVector3[Cases + 2];
        var a = new fQuaternion[Cases + 2]; var b = new fQuaternion[Cases + 2]; var t = new ffloat[Cases + 2];
        var sx = new ffloat[Cases + 2]; var nv = new fUnitVector3[Cases + 2];
        var q = new fQuaternion[Cases + 2]; var ok = new byte[Cases + 2];
        sx[0] = sx[Cases + 1] = ffloat.FromRaw(617);
        nv[0] = nv[Cases + 1] = fUnitVector3.up;
        q[0] = q[Cases + 1] = fQuaternion.identity;
        ok[0] = ok[Cases + 1] = 123;
        ulong seed = 617;
        int[] edge = { int.MinValue, int.MaxValue, 0, 1, -1, 65535, 65536, 65537, 1073741823 };
        for (int i = 1; i <= Cases; i++)
        {
            x[i] = ffloat.FromRaw(i <= edge.Length ? edge[i - 1] : (int)(Next(ref seed) & int.MaxValue));
            int scale = i % 3 == 0 ? 25 : i % 3 == 1 ? 4 : 0;
            v[i] = i <= edge.Length ? fVector3.FromRaw(edge[i - 1], edge[(i + 1) % edge.Length], edge[(i + 3) % edge.Length])
                : fVector3.FromRaw((int)Next(ref seed) >> scale, (int)Next(ref seed) >> scale, (int)Next(ref seed) >> scale);
            if (i % 101 == 0) v[i] = default;
            fQuaternion.TryFromRaw((int)Next(ref seed), (int)Next(ref seed), (int)Next(ref seed), (int)Next(ref seed), out a[i]);
            fQuaternion.TryFromRaw((int)Next(ref seed), (int)Next(ref seed), (int)Next(ref seed), (int)Next(ref seed), out b[i]);
            // Both sides of the close-angle threshold (~0.633 degrees), plus tiny angles.
            if (i % 4 != 0) b[i] = a[i] * fQuaternion.AngleAxis(fAngle.FromRaw(i % 4 == 1 ? 4096u : i % 4 == 2 ? 7549746u : 7549748u), fUnitVector3.up);
            if (i % 5 == 0) b[i] = fQuaternion.FromRawUnchecked(-b[i].x.RawValue, -b[i].y.RawValue, -b[i].z.RawValue, -b[i].w.RawValue);
            t[i] = ffloat.FromRaw(i % 8 == 0 ? -1 : i % 8 == 1 ? 0 : i % 8 == 2 ? 65536 : i % 8 == 3 ? 65537 : (int)(Next(ref seed) % 65537));
        }
        // Explicit close-branch neighbors in Q1.30: w = threshold-1, threshold, threshold+1.
        for (int i = 0; i < 3; i++)
        {
            a[i + 1] = fQuaternion.identity;
            int w = funit.RawOne - (1 << 14) + i - 1;
            long sin2 = (1L << 60) - (long)w * w;
            b[i + 1] = fQuaternion.FromRawUnchecked((int)fWideMath.IntegerSqrtRounded((ulong)sin2), 0, 0, w);
            t[i + 1] = ffloat.Half;
        }
        fixed (ffloat* px = x, ps = sx, pt = t)
        fixed (fVector3* pv = v)
        fixed (fUnitVector3* pn = nv)
        fixed (fQuaternion* pa = a, pb = b, pq = q)
        fixed (byte* po = ok)
        {
            fMathBurst.SqrtBatchKernel(px + 1, ps + 1, Cases, out bool ns);
            fMathBurst.NormalizeBatchKernel(pv + 1, pn + 1, po + 1, Cases, out bool nn);
            fMathBurst.SlerpBatchKernel(pa + 1, pb + 1, pt + 1, pq + 1, Cases, out bool nq);
            Require(ns && nn && nq, "Batch managed fallback");
        }
        long checksum = 0;
        for (int i = 1; i <= Cases; i++)
        {
            fMathBurst.SqrtKernel(in x[i], out ffloat scalarS, out bool ns);
            fMathBurst.NormalizeKernel(in v[i], out fUnitVector3 scalarN, out bool success, out bool nn);
            fMathBurst.SlerpKernel(in a[i], in b[i], in t[i], out fQuaternion scalarQ, out bool nq);
            bool expectedOk = fVector3.TryNormalize(v[i], out fUnitVector3 expectedN);
            Require(ns && nn && nq, "Scalar managed fallback");
            Require(sx[i] == ffloat.Sqrt(x[i]) && scalarS == sx[i], "Sqrt raw mismatch");
            Require(nv[i] == expectedN && scalarN == nv[i] && success == expectedOk && ok[i] == (expectedOk ? 1 : 0), "Normalize raw/status mismatch");
            Require(q[i] == fQuaternion.Slerp(a[i], b[i], t[i]) && scalarQ == q[i], "Slerp raw mismatch");
            checksum = unchecked(checksum + sx[i].RawValue + nv[i].x.RawValue + q[i].w.RawValue);
        }
        Require(sx[0].RawValue == 617 && sx[Cases + 1].RawValue == 617 && nv[0] == fUnitVector3.up && nv[Cases + 1] == fUnitVector3.up && q[0] == fQuaternion.identity && q[Cases + 1] == fQuaternion.identity && ok[0] == 123 && ok[Cases + 1] == 123, "Batch sentinel overwritten");
        fMathBurst.SqrtBatchKernel(null, null, 0, out bool zs);
        fMathBurst.NormalizeBatchKernel(null, null, null, 0, out bool zn);
        fMathBurst.SlerpBatchKernel(null, null, null, null, 0, out bool zq);
        Require(zs && zn && zq, "Zero-count managed fallback");
        fMathBurst.SqrtBatch(null, null, 0); fMathBurst.NormalizeBatch(null, null, null, 0); fMathBurst.SlerpBatch(null, null, null, null, 0);
        int rejected = 0;
        try { fMathBurst.SqrtBatch(null, null, -1); } catch (ArgumentOutOfRangeException) { rejected++; }
        try { fMathBurst.NormalizeBatch(null, null, null, -1); } catch (ArgumentOutOfRangeException) { rejected++; }
        try { fMathBurst.SlerpBatch(null, null, null, null, -1); } catch (ArgumentOutOfRangeException) { rejected++; }
        Require(rejected == 3, "Negative counts not rejected");
        // Invalid raw quaternions have a separate fallback contract, not unit-rotation guarantees.
        fQuaternion invalid = default, identity = fQuaternion.identity; ffloat half = ffloat.Half;
        fMathBurst.SlerpKernel(in invalid, in identity, in half, out fQuaternion invalidQ, out bool ni);
        Require(ni && invalidQ == fQuaternion.Slerp(invalid, identity, half), "Invalid quaternion fallback mismatch");
        Action<fMathValidationKind, string> previous = fMathValidation.Handler;
        int callbacks = 0;
        try
        {
            fMathValidation.Handler = (kind, context) => callbacks++;
            fMathValidation.ResetCounts();
            ffloat negative = ffloat.FromRaw(-1);
            fMathBurst.SqrtKernel(in negative, out ffloat negativeResult, out bool native);
            Require(native && negativeResult == ffloat.Zero && callbacks == 0 && fMathValidation.TotalCount == 0, "Native reporting contract");
            ffloat.Sqrt(negative);
            Require(callbacks == (fMathValidation.IsEnabled ? 1 : 0) && fMathValidation.TotalCount == callbacks, "Managed reporting contract");
        }
        finally { fMathValidation.Handler = previous; fMathValidation.ResetCounts(); }
        return "100000 cases each; scalar/batch native=true; raw/status equal; sentinels/counts/diagnostics passed; checksum=" + checksum;
    }
    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    static ulong Next(ref ulong s) { ulong z = s += 0x9E3779B97F4A7C15UL; z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL; z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL; return z ^ (z >> 31); }
}
public class fMathBurstTests
{
    [Test] public void NativeScalarAndBatch_MatchManagedRaw() => Assert.DoesNotThrow(() => fMathBurstVerification.Run());
}
#endif
