using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace fMath.Diagnostics
{
    public struct fMathBenchmarkResult
    {
        public string Name;
        /// <summary>Nanoseconds per operation (micro) or per simulated frame (game-like).</summary>
        public double NanosecondsPerOp;
        public long Operations;
        /// <summary>Managed bytes allocated inside the measured loop (-1 when unavailable).</summary>
        public long AllocatedBytes;

        public override string ToString()
        {
            return Name + ": " + NanosecondsPerOp.ToString("0.0", CultureInfo.InvariantCulture) + " ns/op, alloc " + AllocatedBytes + " B";
        }
    }

    /// <summary>
    /// Burst-free micro and game-like benchmarks. Runs on any runtime (Mono, IL2CPP, CoreCLR).
    /// The allocation probe defaults to GC.GetAllocatedBytesForCurrentThread and can be replaced
    /// (e.g. with a Unity ProfilerRecorder) through <see cref="AllocationProbe"/>.
    /// </summary>
    public static class fMathBenchmark
    {
        const int INPUT_COUNT = 1024;

        public static Func<long> AllocationProbe = DefaultAllocationProbe;

        static long DefaultAllocationProbe()
        {
            try { return GC.GetAllocatedBytesForCurrentThread(); }
            catch (Exception) { return -1; }
        }

        /// <summary>Sink that keeps results observable so the JIT/AOT cannot drop the work.</summary>
        public static long Sink;

        #region Micro
        public static List<fMathBenchmarkResult> RunMicro(int iterations = 2_000_000)
        {
            var results = new List<fMathBenchmarkResult>();
            var ff = new ffloat[INPUT_COUNT];
            var angles = new fAngle[INPUT_COUNT];
            var vectors = new fVector3[INPUT_COUNT];
            var units = new fUnitVector3[INPUT_COUNT];
            var quats = new fQuaternion[INPUT_COUNT];
            ulong state = 12345;
            for (int i = 0; i < INPUT_COUNT; i++)
            {
                int r0 = Rand(ref state), r1 = Rand(ref state), r2 = Rand(ref state), r3 = Rand(ref state);
                ff[i] = ffloat.FromRaw((r0 >> 10) | 1);
                angles[i] = fAngle.FromRaw((uint)r1);
                vectors[i] = fVector3.FromRaw(r0 >> 6, r1 >> 6, r2 >> 6);
                fVector3.TryNormalize(vectors[i], out units[i]);
                if (!units[i].IsValid) units[i] = fUnitVector3.up;
                quats[i] = fQuaternion.AngleAxis(fAngle.FromRaw((uint)r3), units[i]);
            }

            const int M = INPUT_COUNT - 1;
            results.Add(Measure("ffloat add", iterations, n => { ffloat acc = ffloat.Zero; for (int i = 0; i < n; i++) acc += ff[i & M]; Sink += acc.RawValue; }));
            results.Add(Measure("ffloat mul", iterations, n => { ffloat acc = ffloat.One; for (int i = 0; i < n; i++) acc = ff[i & M] * ff[(i + 1) & M]; Sink += acc.RawValue; }));
            results.Add(Measure("ffloat div", iterations, n => { ffloat acc = ffloat.Zero; for (int i = 0; i < n; i++) acc = ff[i & M] / ff[(i + 7) & M]; Sink += acc.RawValue; }));
            results.Add(Measure("ffloat sqrt", iterations, n => { int acc = 0; for (int i = 0; i < n; i++) acc += ffloat.Sqrt(ffloat.Abs(ff[i & M])).RawValue; Sink += acc; }));
            results.Add(Measure("fTrig sin+cos", iterations, n => { int acc = 0; for (int i = 0; i < n; i++) acc += fTrig.Sin(angles[i & M]).RawValue + fTrig.Cos(angles[i & M]).RawValue; Sink += acc; }));
            results.Add(Measure("fTrig atan2", iterations, n => { uint acc = 0; for (int i = 0; i < n; i++) acc += fTrig.Atan2(ff[i & M], ff[(i + 3) & M]).RawValue; Sink += acc; }));
            results.Add(Measure("fVector3 dot (wide)", iterations, n => { long acc = 0; for (int i = 0; i < n; i++) acc += fVector3.DotWide(vectors[i & M], vectors[(i + 1) & M]); Sink += acc; }));
            results.Add(Measure("fVector3 cross", iterations, n => { int acc = 0; for (int i = 0; i < n; i++) acc += fVector3.Cross(vectors[i & M], vectors[(i + 1) & M]).x.RawValue; Sink += acc; }));
            results.Add(Measure("fVector3 distance squared (wide)", iterations, n => { ulong acc = 0; for (int i = 0; i < n; i++) acc += fVector3.DistanceSquaredWide(vectors[i & M], vectors[(i + 1) & M]); Sink += (long)acc; }));
            results.Add(Measure("fVector3 normalize", iterations, n => { int acc = 0; for (int i = 0; i < n; i++) { fVector3.TryNormalize(vectors[i & M], out fUnitVector3 u); acc += u.x.RawValue; } Sink += acc; }));
            results.Add(Measure("fQuaternion multiply", iterations, n => { int acc = 0; for (int i = 0; i < n; i++) acc += (quats[i & M] * quats[(i + 1) & M]).w.RawValue; Sink += acc; }));
            results.Add(Measure("fQuaternion rotate vector", iterations, n => { int acc = 0; for (int i = 0; i < n; i++) acc += (quats[i & M] * vectors[(i + 1) & M]).y.RawValue; Sink += acc; }));
            results.Add(Measure("fQuaternion slerp", iterations / 4, n => { int acc = 0; for (int i = 0; i < n; i++) acc += fQuaternion.Slerp(quats[i & M], quats[(i + 1) & M], ffloat.Half).w.RawValue; Sink += acc; }));
            return results;
        }

        static int Rand(ref ulong state)
        {
            ulong z = state += 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return (int)(uint)(z ^ (z >> 31));
        }

        static fMathBenchmarkResult Measure(string name, int iterations, Action<int> body)
        {
            // warm-up: several calls so tiered JITs promote the loop body before measuring
            for (int w = 0; w < 4; w++)
                body(Math.Min(iterations, 50000));
            var sw = new Stopwatch();
            long allocBefore = AllocationProbe();
            sw.Start();
            body(iterations);
            sw.Stop();
            long allocAfter = AllocationProbe();
            return new fMathBenchmarkResult
            {
                Name = name,
                Operations = iterations,
                NanosecondsPerOp = sw.Elapsed.TotalMilliseconds * 1e6 / iterations,
                AllocatedBytes = allocBefore < 0 || allocAfter < 0 ? -1 : allocAfter - allocBefore,
            };
        }
        #endregion

        #region Game-like
        /// <summary>
        /// 8-agent frames: normal (1 tick) and rollback 1+N (restore N ticks back, re-simulate N + 1).
        /// Also measures snapshot save+load.
        /// </summary>
        public static List<fMathBenchmarkResult> RunGameLike(int frames = 3000)
        {
            var results = new List<fMathBenchmarkResult>();
            results.Add(MeasureRollback("8 agents: normal x1", frames, 0));
            results.Add(MeasureRollback("8 agents: rollback 1+2", frames, 2));
            results.Add(MeasureRollback("8 agents: rollback 1+4", frames, 4));
            results.Add(MeasureRollback("8 agents: rollback 1+8", frames, 8));

            var sim = new fDeterministicAgentSimulation();
            var snapshot = new fDeterministicAgentSimulation.Snapshot();
            int copies = frames * 100;
            for (int i = 0; i < copies / 4; i++)
            {
                sim.SaveSnapshot(snapshot);
                sim.LoadSnapshot(snapshot);
            }
            var sw = new Stopwatch();
            long allocBefore = AllocationProbe();
            sw.Start();
            for (int i = 0; i < copies; i++)
            {
                sim.SaveSnapshot(snapshot);
                sim.LoadSnapshot(snapshot);
            }
            sw.Stop();
            long allocAfter = AllocationProbe();
            results.Add(new fMathBenchmarkResult
            {
                Name = "snapshot save+load (8 agents, " + SnapshotBytes + " B)",
                Operations = copies,
                NanosecondsPerOp = sw.Elapsed.TotalMilliseconds * 1e6 / copies,
                AllocatedBytes = allocBefore < 0 ? -1 : allocAfter - allocBefore,
            });
            return results;
        }

        /// <summary>Bytes of raw agent state per snapshot.</summary>
        public static int SnapshotBytes => fDeterministicAgentSimulation.AgentCount * System.Runtime.InteropServices.Marshal.SizeOf(typeof(fAgentState));

        static fMathBenchmarkResult MeasureRollback(string name, int frames, int rollbackTicks)
        {
            const ulong Seed = 0xBEEF;
            var sim = new fDeterministicAgentSimulation();
            int ring = rollbackTicks + 1;
            var snapshots = new fDeterministicAgentSimulation.Snapshot[ring];
            for (int i = 0; i < ring; i++) snapshots[i] = new fDeterministicAgentSimulation.Snapshot();
            var inputs = new fAgentInput[fDeterministicAgentSimulation.AgentCount];

            // fill the snapshot ring, then warm up with the measured pattern (JIT tiering, type init)
            for (int t = 0; t < ring; t++)
                Step(sim, snapshots, inputs, Seed, ring);
            RunFrames(sim, snapshots, inputs, Seed, ring, rollbackTicks, Math.Max(200, frames / 2));

            var sw = new Stopwatch();
            long allocBefore = AllocationProbe();
            sw.Start();
            RunFrames(sim, snapshots, inputs, Seed, ring, rollbackTicks, frames);
            sw.Stop();
            long allocAfter = AllocationProbe();
            Sink += (long)sim.ComputeWorldHash();
            return new fMathBenchmarkResult
            {
                Name = name,
                Operations = frames,
                NanosecondsPerOp = sw.Elapsed.TotalMilliseconds * 1e6 / frames,
                AllocatedBytes = allocBefore < 0 ? -1 : allocAfter - allocBefore,
            };
        }

        static void RunFrames(fDeterministicAgentSimulation sim, fDeterministicAgentSimulation.Snapshot[] snapshots, fAgentInput[] inputs, ulong seed, int ring, int rollbackTicks, int frames)
        {
            for (int f = 0; f < frames; f++)
            {
                if (rollbackTicks > 0)
                {
                    int target = sim.CurrentTick - rollbackTicks;
                    sim.LoadSnapshot(snapshots[target % ring]);
                    for (int t = 0; t < rollbackTicks; t++)
                        Step(sim, snapshots, inputs, seed, ring);
                }
                Step(sim, snapshots, inputs, seed, ring);
            }
        }

        static void Step(fDeterministicAgentSimulation sim, fDeterministicAgentSimulation.Snapshot[] snapshots, fAgentInput[] inputs, ulong seed, int ring)
        {
            sim.SaveSnapshot(snapshots[sim.CurrentTick % ring]);
            for (int a = 0; a < inputs.Length; a++)
                inputs[a] = fDeterministicAgentSimulation.ScriptedInput(seed, sim.CurrentTick, a);
            sim.Tick(inputs);
        }
        #endregion

        public static string ToMarkdown(string title, List<fMathBenchmarkResult> results, string unitLabel = "ns/op")
        {
            var sb = new StringBuilder();
            sb.Append("| ").Append(title).Append(" | ").Append(unitLabel).Append(" | allocated bytes |\n|---|---:|---:|\n");
            foreach (var r in results)
                sb.Append("| ").Append(r.Name).Append(" | ").Append(r.NanosecondsPerOp.ToString("0.0", CultureInfo.InvariantCulture)).Append(" | ").Append(r.AllocatedBytes).Append(" |\n");
            return sb.ToString();
        }
    }
}
