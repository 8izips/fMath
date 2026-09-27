namespace fMath.Diagnostics
{
    /// <summary>Per-category raw hashes of one probe run. All must match across platforms.</summary>
    public struct fMathRawHashResult
    {
        public ulong Scalar;
        public ulong Trig;
        public ulong Vector;
        public ulong UnitVector;
        public ulong Quaternion;
        public ulong Combined;
        public long WordCount;

        public override string ToString()
        {
            return "combined " + Combined.ToString("X16") + " (scalar " + Scalar.ToString("X16") + ", trig " + Trig.ToString("X16")
                + ", vector " + Vector.ToString("X16") + ", unit " + UnitVector.ToString("X16") + ", quaternion " + Quaternion.ToString("X16")
                + ", words " + WordCount + ")";
        }
    }

    /// <summary>
    /// Runs every public fMath v2 operation over the fixed corpus in a fixed order and folds the raw
    /// outputs into hashes. Identical hashes on Editor Mono, Windows / Android / iOS IL2CPP prove
    /// bit-identical results for the same raw inputs.
    /// </summary>
    public static class fMathRawHashProbe
    {
        public static fMathRawHashResult Run(fMathDeterminismCorpus corpus)
        {
            fRawHash scalar = fRawHash.Create();
            fRawHash trig = fRawHash.Create();
            fRawHash vector = fRawHash.Create();
            fRawHash unit = fRawHash.Create();
            fRawHash quaternion = fRawHash.Create();

            for (int r = 0; r < corpus.RecordCount; r++)
            {
                int v0 = corpus.Get(r, 0), v1 = corpus.Get(r, 1), v2 = corpus.Get(r, 2), v3 = corpus.Get(r, 3);
                int v4 = corpus.Get(r, 4), v5 = corpus.Get(r, 5), v6 = corpus.Get(r, 6), v7 = corpus.Get(r, 7);
                HashScalar(ref scalar, v0, v1, v3, v5);
                HashTrig(ref trig, v0, v1, v3, v7);
                HashVector(ref vector, v1, v2, v3, v5, v6, v7, v0);
                HashUnitVector(ref unit, v1, v2, v3, v5, v6, v7, v0);
                HashQuaternion(ref quaternion, v0, v1, v2, v3, v4, v5, v6, v7);
            }

            fRawHash combined = fRawHash.Create();
            combined.Add(scalar.Value);
            combined.Add(trig.Value);
            combined.Add(vector.Value);
            combined.Add(unit.Value);
            combined.Add(quaternion.Value);

            return new fMathRawHashResult
            {
                Scalar = scalar.Value,
                Trig = trig.Value,
                Vector = vector.Value,
                UnitVector = unit.Value,
                Quaternion = quaternion.Value,
                Combined = combined.Value,
                WordCount = scalar.Count + trig.Count + vector.Count + unit.Count + quaternion.Count,
            };
        }

        static void HashScalar(ref fRawHash h, int a, int b, int c, int d)
        {
            ffloat x = ffloat.FromRaw(a), y = ffloat.FromRaw(b), s = ffloat.FromRaw(c), t = ffloat.FromRaw(d);
            h.Add(x + y); h.Add(x - y); h.Add(-x); h.Add(x * y); h.Add(s * t); h.Add(x / y); h.Add(s / t);
            h.Add(x % y); h.Add(x * 3); h.Add(x / 7); h.Add(ffloat.MulDiv(s, t, y));
            h.Add(ffloat.TryDivide(x, s, out ffloat q)); h.Add(q);
            h.Add(ffloat.Abs(x)); h.Add(ffloat.Floor(x)); h.Add(ffloat.Ceiling(x)); h.Add(ffloat.Round(x)); h.Add(ffloat.Truncate(x));
            h.Add(x.FloorToInt()); h.Add(x.CeilToInt()); h.Add(x.RoundToInt());
            h.Add(ffloat.Min(x, y)); h.Add(ffloat.Clamp(x, s, t)); h.Add(ffloat.Lerp(x, y, t)); h.Add(ffloat.LerpUnclamped(s, t, y));
            h.Add(ffloat.MoveTowards(x, y, ffloat.Abs(s)));
            h.Add(ffloat.TrySqrt(x, out ffloat root)); h.Add(root); h.Add(ffloat.Sqrt(ffloat.Abs(y)));
            h.Add(ffloat.SqrtWide((ulong)((long)a * a)));
            h.Add(ffloat.FromFraction(c, d == 0 ? 1 : d)); h.Add(ffloat.FromInt(c >> 16));
            funit u = funit.FromRaw(b >> 1), w = funit.FromRaw(d >> 1);
            h.Add(u * w); h.Add(u + w); h.Add(u - w); h.Add(u * x); h.Add(u.ToFfloat()); h.Add(funit.FromFfloat(ffloat.FromRaw(c >> 14)));
        }

        static void HashTrig(ref fRawHash h, int a, int b, int c, int d)
        {
            fAngle angle = fAngle.FromRaw((uint)a);
            fAngle other = fAngle.FromRaw((uint)d);
            h.Add(fTrig.Sin(angle)); h.Add(fTrig.Cos(angle)); h.Add(fTrig.Sin(other)); h.Add(fTrig.Cos(other));
            h.Add(fTrig.TryTan(angle, out ffloat tan)); h.Add(tan);
            h.Add(fTrig.Atan2(ffloat.FromRaw(b), ffloat.FromRaw(c)));
            h.Add(fTrig.Atan2Wide((long)a * b, (long)c * d));
            h.Add(fTrig.Atan(ffloat.FromRaw(c)));
            funit u = funit.FromRaw(b >> 1);
            h.Add(fTrig.Acos(u)); h.Add(fTrig.Asin(u));
            h.Add(angle + other); h.Add(angle.SignedDeltaTo(other)); h.Add(angle.Half); h.Add(angle * ffloat.FromRaw(c));
            h.Add(fAngle.MoveTowards(angle, other, fAngleDelta.FromRaw(c)));
            h.Add(fAngle.FromTurnsFraction(c, d == 0 ? 7 : d)); h.Add(fAngle.FromDegrees(c >> 12));
            h.Add(fAngleDelta.FromRaw(b) * ffloat.FromRaw(c)); h.Add(fAngleDelta.FromRaw(b).Half);
            h.Add(fAngleDelta.ClampMagnitude(fAngleDelta.FromRaw(b), fAngleDelta.FromRaw(d)));
        }

        static void HashVector(ref fRawHash h, int ax, int ay, int az, int bx, int by, int bz, int s)
        {
            fVector3 a = fVector3.FromRaw(ax, ay, az), b = fVector3.FromRaw(bx, by, bz);
            ffloat scalar = ffloat.FromRaw(s >> 8);
            h.Add(a + b); h.Add(a - b); h.Add(a * scalar); h.Add(a / 3); h.Add(a * funit.FromRaw(s >> 1));
            h.Add(a.LengthSquaredWide); h.Add(a.sqrMagnitude); h.Add(a.magnitude);
            h.Add(fVector3.DistanceSquaredWide(a, b)); h.Add(fVector3.Distance(a, b));
            h.Add(fVector3.CompareDistanceSquared(a, b, scalar)); h.Add(fVector3.IsWithinDistance(a, b, scalar));
            h.Add(fVector3.DotWide(a, b)); h.Add(fVector3.Dot(a, b)); h.Add(fVector3.CrossWide(a, b)); h.Add(fVector3.Cross(a, b));
            h.Add(fVector3.TryNormalize(a, out fUnitVector3 na)); h.Add(na);
            h.Add(fVector3.TryNormalize(b, out fUnitVector3 nb)); h.Add(nb);
            h.Add(fVector3.ClampMagnitude(a, ffloat.Abs(scalar)));
            h.Add(fVector3.AngleUsingAtan2(a, b)); h.Add(fVector3.WithinAngle(a, b, fAngle.FromRaw((uint)s)));
            if (nb.IsValid)
            {
                h.Add(fVector3.SignedAngle(a, b, nb));
                h.Add(fVector3.Reflect(a, nb)); h.Add(fVector3.Project(a, nb)); h.Add(fVector3.ProjectOnPlane(a, nb));
            }
            h.Add(fVector3.Lerp(a, b, scalar)); h.Add(fVector3.MoveTowards(a, b, ffloat.Abs(scalar)));
            h.Add(fVector3.Scale(a, b)); h.Add(fVector3.Min(a, b));

            fVector2 p = fVector2.FromRaw(ax, bz), q = fVector2.FromRaw(bx, az);
            h.Add(p + q); h.Add(p.LengthSquaredWide); h.Add(p.magnitude); h.Add(fVector2.DistanceSquaredWide(p, q));
            h.Add(fVector2.DotWide(p, q)); h.Add(fVector2.CrossWide(p, q)); h.Add(fVector2.Cross(p, q));
            h.Add(fVector2.TryNormalize(p, out fUnitVector2 np)); h.Add(np);
            h.Add(p.ToAngle()); h.Add(fVector2.SignedAngle(p, q)); h.Add(fVector2.AngleUsingAtan2(p, q));
            h.Add(fVector2.Rotate(p, fAngle.FromRaw((uint)s)));
            h.Add(fVector2.ClampMagnitude(p, ffloat.Abs(scalar))); h.Add(fVector2.MoveTowards(p, q, ffloat.Abs(scalar)));
        }

        static void HashUnitVector(ref fRawHash h, int ax, int ay, int az, int bx, int by, int bz, int s)
        {
            fUnitVector3.TryFromRaw(ax, ay, az, out fUnitVector3 a);
            fUnitVector3.TryFromRaw(bx, by, bz, out fUnitVector3 b);
            h.Add(a); h.Add(b);
            h.Add(fUnitVector3.DotWide(a, b)); h.Add(fUnitVector3.Dot(a, b)); h.Add(fUnitVector3.CrossWide(a, b));
            h.Add(fUnitVector3.TryNormalizedCross(a, b, out fUnitVector3 c)); h.Add(c);
            h.Add(fUnitVector3.WithinAngle(a, b, fAngle.FromRaw((uint)s))); h.Add(fUnitVector3.AngleUsingAtan2(a, b));
            h.Add(a * ffloat.FromRaw(s >> 4)); h.Add(-a);
            fUnitVector2 d = fUnitVector2.FromAngle(fAngle.FromRaw((uint)s));
            fUnitVector2.TryFromRaw(ax, bx, out fUnitVector2 e);
            h.Add(d); h.Add(e); h.Add(d.ToAngle()); h.Add(fUnitVector2.DotWide(d, e)); h.Add(fUnitVector2.CrossWide(d, e));
            h.Add(fUnitVector2.SignedAngle(d, e)); h.Add(fUnitVector2.Rotate(e, fAngle.FromRaw((uint)ay)));
        }

        static void HashQuaternion(ref fRawHash h, int a, int b, int c, int d, int e, int f, int g, int k)
        {
            fUnitVector3.TryFromRaw(b, c, d, out fUnitVector3 axis);
            if (!axis.IsValid) axis = fUnitVector3.up;
            fUnitVector3.TryFromRaw(e, f, g, out fUnitVector3 dir);
            if (!dir.IsValid) dir = fUnitVector3.forward;

            fQuaternion p = fQuaternion.AngleAxis(fAngle.FromRaw((uint)a), axis);
            fQuaternion q = fQuaternion.AngleAxis(fAngleDelta.FromRaw(k), dir);
            fVector3 v = fVector3.FromRaw(c, e, g);
            ffloat t = ffloat.FromRaw((int)((uint)k >> 16));

            h.Add(p); h.Add(q); h.Add(p * q); h.Add(q * p); h.Add(p * v); h.Add(p * dir);
            h.Add(fQuaternion.DotWide(p, q)); h.Add(fQuaternion.Dot(p, q));
            h.Add(fQuaternion.Inverse(p)); h.Add(fQuaternion.TryInverse(q, out fQuaternion inv)); h.Add(inv);
            h.Add(fQuaternion.TryFromRaw(a, b, c, d, out fQuaternion raw)); h.Add(raw);
            h.Add(fQuaternion.Normalize(fQuaternion.FromRawUnchecked(a >> 2, b >> 2, c >> 2, d >> 2)));
            h.Add(fQuaternion.FromToRotation(axis, dir));
            h.Add(fQuaternion.FromToRotation(axis, -axis));
            h.Add(fQuaternion.TryLookRotation(fVector3.FromRaw(b, c, d), fVector3.FromRaw(e, f, g), out fQuaternion look)); h.Add(look);
            h.Add(fQuaternion.Lerp(p, q, t)); h.Add(fQuaternion.Slerp(p, q, t));
            h.Add(fQuaternion.RotateTowards(p, q, fAngle.FromRaw((uint)f >> 2)));
            h.Add(fQuaternion.Angle(p, q)); h.Add(fQuaternion.Approximately(p, q, fAngle.FromRaw((uint)g)));
            fQuaternion euler = fQuaternion.Euler(fAngle.FromRaw((uint)a), fAngle.FromRaw((uint)b), fAngle.FromRaw((uint)c));
            h.Add(euler);
            fQuaternion.ToEuler(euler, out fAngle ex, out fAngle ey, out fAngle ez);
            h.Add(ex); h.Add(ey); h.Add(ez);
        }
    }
}
