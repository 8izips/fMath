#if UNITY_6000_6_OR_NEWER
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

// Copy into the existing fMath.Tests assembly of the isolated measurement project.
public sealed class BurstBench : MonoBehaviour
{
    public Camera BenchmarkCamera;
    const int N = 1024, Repeats = 300, Iterations = N * Repeats;
    readonly ffloat[] _x = new ffloat[N], _sx = new ffloat[N], _t = new ffloat[N];
    readonly fVector3[] _v = new fVector3[N]; readonly fUnitVector3[] _nv = new fUnitVector3[N];
    readonly byte[] _ok = new byte[N]; readonly fQuaternion[] _a = new fQuaternion[N], _b = new fQuaternion[N], _close = new fQuaternion[N], _q = new fQuaternion[N];
    readonly List<Row> _rows = new List<Row>(96);
    struct Row { public int Mode, Rep, Order, Frame; public bool Native, Batch; public long Start, End, Sum, Bytes; }
    string _output; RenderTexture _frame; static long _sink;
    IEnumerator Start()
    {
        Application.runInBackground = true; QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1;
        _output = Path.GetDirectoryName(Application.dataPath);
        _frame = new RenderTexture(640, 360, 24); _frame.Create(); BenchmarkCamera.targetTexture = _frame;
        string parity = fMathBurstVerification.Run();
        File.WriteAllText(Path.Combine(_output, "parity.txt"), parity);
        File.WriteAllText(Path.Combine(_output, "environment.txt"), "Unity: " + Application.unityVersion + "\nCPU: " + SystemInfo.processorType + "\nOS: " + SystemInfo.operatingSystem + "\nGPU: " + SystemInfo.graphicsDeviceName + "\nAPI: " + SystemInfo.graphicsDeviceType + "\nDevelopment: " + UnityEngine.Debug.isDebugBuild + "\nBackend: Mono; Burst: built-in2.0.0 AOT, all six actual entry markers native\nFMATH_VALIDATE: " + fMathValidation.IsEnabled + "\nSeed617; inputs1024; operations307200; 6 AB/BA pairs; scalar checksum in timing, batch checksum outside timing; allocation before/after\nProfiler disconnected; Built-in pipeline, 640x360; FPS uncapped for fixed-operation throughput; camera rendering outside timing\n");
        if (Environment.GetCommandLineArgs().Length > 0 && Array.IndexOf(Environment.GetCommandLineArgs(), "-parityOnly") >= 0)
        { Capture("validation-frame.png"); File.WriteAllText(Path.Combine(_output, "complete.txt"), "Define-enabled raw and diagnostic checks complete"); Application.Quit(0); yield break; }
        ulong seed = 617;
        for (int i = 0; i < N; i++)
        {
            _x[i] = ffloat.FromRaw((int)(Next(ref seed) & int.MaxValue));
            _v[i] = fVector3.FromRaw((int)Next(ref seed) >> 4, (int)Next(ref seed) >> 4, (int)Next(ref seed) >> 4);
            fQuaternion.TryFromRaw((int)Next(ref seed), (int)Next(ref seed), (int)Next(ref seed), (int)Next(ref seed), out _a[i]);
            fQuaternion.TryFromRaw((int)Next(ref seed), (int)Next(ref seed), (int)Next(ref seed), (int)Next(ref seed), out _b[i]);
            _close[i] = _a[i] * fQuaternion.AngleAxis(fAngle.FromDegreesFraction(1, 10), fUnitVector3.up);
            _t[i] = ffloat.FromRaw((int)(Next(ref seed) % 65537));
        }
        for (int m = 0; m < 4; m++) for (int w = 0; w < 2; w++) { Measure(m, false, false); Measure(m, true, false); Measure(m, false, true); Measure(m, true, true); }
        for (int rep = 0; rep < 6; rep++)
        {
            for (int mode = 0; mode < 4; mode++) for (int batch = 0; batch < 2; batch++)
            {
                bool firstNative = (rep & 1) != 0;
                BenchmarkCamera.Render(); yield return null;
                Row a = Measure(mode, firstNative, batch != 0); a.Rep = rep; a.Order = 0; _rows.Add(a);
                BenchmarkCamera.Render(); yield return null;
                Row b = Measure(mode, !firstNative, batch != 0); b.Rep = rep; b.Order = 1; _rows.Add(b);
                if (a.Sum != b.Sum) throw new InvalidOperationException("Checksum mismatch");
            }
            if (rep == 0)
            {
                Save(); Capture("first-frame.png"); File.WriteAllText(Path.Combine(_output, "gate-ready.txt"), "16 blocks ready");
                double deadline = Time.realtimeSinceStartupAsDouble + 120;
                while (!File.Exists(Path.Combine(_output, "continue.txt"))) { if (Time.realtimeSinceStartupAsDouble > deadline) throw new TimeoutException("Review gate timeout"); BenchmarkCamera.Render(); yield return null; }
            }
        }
        Save(); Capture("final-frame.png"); File.WriteAllText(Path.Combine(_output, "complete.txt"), "96 blocks complete; per-pair checksums equal");
        BenchmarkCamera.targetTexture = null; _frame.Release(); Destroy(_frame); Application.Quit(0);
    }
    unsafe Row Measure(int mode, bool native, bool batch)
    {
        long sum = 0; fQuaternion[] b = mode == 3 ? _close : _b;
        fixed (ffloat* px = _x, ps = _sx, pt = _t)
        fixed (fVector3* pv = _v)
        fixed (fUnitVector3* pn = _nv)
        fixed (byte* po = _ok)
        fixed (fQuaternion* pa = _a, pb = b, pq = _q)
        {
            long before = GC.GetAllocatedBytesForCurrentThread(), start = Stopwatch.GetTimestamp();
            if (batch)
            {
                if (native)
                {
                    if (mode == 0) for (int r = 0; r < Repeats; r++) fMathBurst.SqrtBatch(px, ps, N);
                    else if (mode == 1) for (int r = 0; r < Repeats; r++) fMathBurst.NormalizeBatch(pv, pn, po, N);
                    else for (int r = 0; r < Repeats; r++) fMathBurst.SlerpBatch(pa, pb, pt, pq, N);
                }
                else
                {
                    if (mode == 0) for (int r = 0; r < Repeats; r++) for (int i = 0; i < N; i++) ps[i] = ffloat.Sqrt(px[i]);
                    else if (mode == 1) for (int r = 0; r < Repeats; r++) for (int i = 0; i < N; i++) po[i] = fVector3.TryNormalize(pv[i], out pn[i]) ? (byte)1 : (byte)0;
                    else for (int r = 0; r < Repeats; r++) for (int i = 0; i < N; i++) pq[i] = fQuaternion.Slerp(pa[i], pb[i], pt[i]);
                }
            }
            else if (native)
            {
                if (mode == 0) for (int i = 0; i < Iterations; i++) { fMathBurst.Sqrt(in _x[i & 1023], out ffloat s); sum += s.RawValue; }
                else if (mode == 1) for (int i = 0; i < Iterations; i++) { bool ok = fMathBurst.TryNormalize(in _v[i & 1023], out fUnitVector3 n); sum += (long)n.x.RawValue + n.y.RawValue + n.z.RawValue + (ok ? 1 : 0); }
                else for (int i = 0; i < Iterations; i++) { int j = i & 1023; fMathBurst.Slerp(in _a[j], in b[j], in _t[j], out fQuaternion q); sum += (long)q.x.RawValue + q.y.RawValue + q.z.RawValue + q.w.RawValue; }
            }
            else
            {
                if (mode == 0) for (int i = 0; i < Iterations; i++) sum += ffloat.Sqrt(_x[i & 1023]).RawValue;
                else if (mode == 1) for (int i = 0; i < Iterations; i++) { bool ok = fVector3.TryNormalize(_v[i & 1023], out fUnitVector3 n); sum += (long)n.x.RawValue + n.y.RawValue + n.z.RawValue + (ok ? 1 : 0); }
                else for (int i = 0; i < Iterations; i++) { int j = i & 1023; fQuaternion q = fQuaternion.Slerp(_a[j], b[j], _t[j]); sum += (long)q.x.RawValue + q.y.RawValue + q.z.RawValue + q.w.RawValue; }
            }
            long end = Stopwatch.GetTimestamp(), after = GC.GetAllocatedBytesForCurrentThread();
            if (batch)
            {
                for (int i = 0; i < N; i++) sum += mode == 0 ? ps[i].RawValue : mode == 1 ? (long)pn[i].x.RawValue + pn[i].y.RawValue + pn[i].z.RawValue + po[i] : (long)pq[i].x.RawValue + pq[i].y.RawValue + pq[i].z.RawValue + pq[i].w.RawValue;
                sum *= Repeats;
            }
            _sink = unchecked(_sink + sum);
            return new Row { Mode = mode, Native = native, Batch = batch, Frame = Time.frameCount, Start = start, End = end, Sum = sum, Bytes = after - before };
        }
    }
    void Save()
    {
        string[] names = { "sqrt", "normalize gameplay", "slerp random", "slerp close" };
        var csv = new StringBuilder("dataset,repetition,order,variant,call_mode,frame,iterations,start_ticks,end_ticks,ns_per_op,checksum,allocated_bytes\n");
        foreach (Row r in _rows) csv.Append(names[r.Mode]).Append(',').Append(r.Rep).Append(',').Append(r.Order).Append(',').Append(r.Native ? "burst" : "managed").Append(',').Append(r.Batch ? "batch1024" : "scalar").Append(',').Append(r.Frame).Append(',').Append(Iterations).Append(',').Append(r.Start).Append(',').Append(r.End).Append(',').Append(((r.End - r.Start) * (1e9 / Stopwatch.Frequency) / Iterations).ToString("G17", CultureInfo.InvariantCulture)).Append(',').Append(r.Sum).Append(',').Append(r.Bytes).Append('\n');
        File.WriteAllText(Path.Combine(_output, "results.csv"), csv.ToString());
    }
    void Capture(string name)
    {
        BenchmarkCamera.Render(); RenderTexture previous = RenderTexture.active; RenderTexture.active = _frame;
        var image = new Texture2D(640, 360, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, 640, 360), 0, 0); image.Apply(); RenderTexture.active = previous;
        Color pixel = image.GetPixel(320, 180); if (pixel.r == 0 && pixel.g == 0 && pixel.b == 0) throw new InvalidOperationException("Black rendered frame");
        File.WriteAllBytes(Path.Combine(_output, name), image.EncodeToPNG()); Destroy(image);
    }
    static ulong Next(ref ulong s) { ulong z = s += 0x9E3779B97F4A7C15UL; z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL; z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL; return z ^ (z >> 31); }
}
#endif
