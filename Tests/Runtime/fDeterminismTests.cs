using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using fMath.Diagnostics;
using NUnit.Framework;

namespace fMath.Tests
{
    /// <summary>
    /// Golden raw hashes. They were produced on Windows x64 (CoreCLR) and must be reproduced bit for bit
    /// by Unity Editor Mono and by Windows / Android ARM64 / iOS ARM64 IL2CPP players. A mismatch means
    /// a platform-dependent result (or an intentional numeric change, in which case bump the golden
    /// values together with fMathFormatVersion or the GameSimulationVersion).
    /// </summary>
    [TestFixture]
    public class fDeterminismTests
    {
        const ulong GoldenCombined = 0x08BDC5891B892EA1UL;
        const ulong GoldenScalar = 0xDD6E904678C46A40UL;
        const ulong GoldenTrig = 0xC04222DA05BB6C05UL;
        const ulong GoldenVector = 0x227101F14B82F86AUL;
        const ulong GoldenUnitVector = 0xB0E66DDD8A83D110UL;
        const ulong GoldenQuaternion = 0xE20D6ECD7862A3CBUL;
        const ulong GoldenSimulationFinal = 0x13D4456E86187E21UL;
        const ulong GoldenSimulationChain = 0xAB4052045BE65F56UL;
        const ulong SimulationSeed = 0xC0FFEE;
        const int SimulationTicks = 10000;

        [Test]
        public void RawHash_MatchesGolden()
        {
            fMathRawHashResult result = fMathRawHashProbe.Run(fMathDeterminismCorpus.Generate());
            TestContext.WriteLine(result.ToString());
            Assert.That(result.Scalar, Is.EqualTo(GoldenScalar), "scalar");
            Assert.That(result.Trig, Is.EqualTo(GoldenTrig), "trig");
            Assert.That(result.Vector, Is.EqualTo(GoldenVector), "vector");
            Assert.That(result.UnitVector, Is.EqualTo(GoldenUnitVector), "unit vector");
            Assert.That(result.Quaternion, Is.EqualTo(GoldenQuaternion), "quaternion");
            Assert.That(result.Combined, Is.EqualTo(GoldenCombined), "combined");
        }

        [Test]
        public void Corpus_FileMatchesGenerator()
        {
            string path = CorpusPath();
            if (path == null)
                Assert.Ignore("corpus file not reachable from this runtime (player build)");
            fMathDeterminismCorpus fromFile;
            using (var stream = File.OpenRead(path))
                fromFile = fMathDeterminismCorpus.Read(stream);
            Assert.That(fromFile.RecordCount, Is.EqualTo(fMathDeterminismCorpus.DefaultRecordCount));
            Assert.That(fromFile.ContentEquals(fMathDeterminismCorpus.Generate()), Is.True);
            Assert.That(fMathRawHashProbe.Run(fromFile).Combined, Is.EqualTo(GoldenCombined));
        }

        static string CorpusPath([CallerFilePath] string sourcePath = "")
        {
            try
            {
                string candidate = Path.Combine(Path.GetDirectoryName(sourcePath), "..", "Determinism", "fmath_v2_input_vectors.bin");
                return File.Exists(candidate) ? candidate : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        [Test]
        public void Simulation_10000Ticks_MatchesGolden()
        {
            var sim = new fDeterministicAgentSimulation();
            var inputs = new fAgentInput[fDeterministicAgentSimulation.AgentCount];
            var chain = fRawHash.Create();
            for (int t = 0; t < SimulationTicks; t++)
            {
                FillInputs(inputs, t);
                sim.Tick(inputs);
                chain.Add(sim.ComputeWorldHash());
            }
            TestContext.WriteLine($"final {sim.ComputeWorldHash():X16}, chain {chain.Value:X16}");

            // the scenario must exercise the interesting paths, not idle agents
            int hit = 0;
            for (int i = 0; i < fDeterministicAgentSimulation.AgentCount; i++)
            {
                fAgentState a = sim.GetAgent(i);
                if (a.Health < 1000) hit++;
                Assert.That(a.Rotation.IsNormalized, Is.True);
                Assert.That(ffloat.Abs(a.Position.x) <= ffloat.FromInt(61) && ffloat.Abs(a.Position.z) <= ffloat.FromInt(61), Is.True, "agent left the arena");
            }
            Assert.That(hit, Is.GreaterThan(0), "no attack ever connected");

            Assert.That(sim.ComputeWorldHash(), Is.EqualTo(GoldenSimulationFinal));
            Assert.That(chain.Value, Is.EqualTo(GoldenSimulationChain));
        }

        [Test]
        public void Rollback_RestoreAndResimulate_MatchesBaseline()
        {
            const int Ticks = 2000;
            var baseline = new ulong[Ticks + 1];
            var sim = new fDeterministicAgentSimulation();
            var inputs = new fAgentInput[fDeterministicAgentSimulation.AgentCount];
            baseline[0] = sim.ComputeWorldHash();
            for (int t = 0; t < Ticks; t++)
            {
                FillInputs(inputs, t);
                sim.Tick(inputs);
                baseline[t + 1] = sim.ComputeWorldHash();
            }

            // rollback netcode pattern: keep a ring of snapshots, periodically restore N ticks back and re-run
            const int Ring = 9;
            var snapshots = new fDeterministicAgentSimulation.Snapshot[Ring];
            for (int i = 0; i < Ring; i++) snapshots[i] = new fDeterministicAgentSimulation.Snapshot();
            var replay = new fDeterministicAgentSimulation();
            int rollbacks = 0;
            int highest = 0;
            while (replay.CurrentTick < Ticks)
            {
                int tick = replay.CurrentTick;
                replay.SaveSnapshot(snapshots[tick % Ring]);
                FillInputs(inputs, tick);
                replay.Tick(inputs);
                Assert.That(replay.ComputeWorldHash(), Is.EqualTo(baseline[tick + 1]), $"tick {tick + 1}");

                int depth = 1 + (tick % 8);
                bool newTick = replay.CurrentTick > highest;
                if (newTick) highest = replay.CurrentTick;
                if (newTick && tick % 5 == 0 && replay.CurrentTick - depth >= 0)
                {
                    int target = replay.CurrentTick - depth;
                    replay.LoadSnapshot(snapshots[target % Ring]);
                    Assert.That(replay.CurrentTick, Is.EqualTo(target));
                    Assert.That(replay.ComputeWorldHash(), Is.EqualTo(baseline[target]), $"restore {target}");
                    rollbacks++;
                }
            }
            Assert.That(rollbacks, Is.GreaterThan(100));
        }

        static void FillInputs(fAgentInput[] inputs, int tick)
        {
            for (int a = 0; a < inputs.Length; a++)
                inputs[a] = fDeterministicAgentSimulation.ScriptedInput(SimulationSeed, tick, a);
        }

        [Test]
        public void StructSizes()
        {
            Assert.That(Marshal.SizeOf(typeof(ffloat)), Is.EqualTo(4));
            Assert.That(Marshal.SizeOf(typeof(funit)), Is.EqualTo(4));
            Assert.That(Marshal.SizeOf(typeof(fAngle)), Is.EqualTo(4));
            Assert.That(Marshal.SizeOf(typeof(fAngleDelta)), Is.EqualTo(4));
            Assert.That(Marshal.SizeOf(typeof(fVector2)), Is.EqualTo(8));
            Assert.That(Marshal.SizeOf(typeof(fVector3)), Is.EqualTo(12));
            Assert.That(Marshal.SizeOf(typeof(fUnitVector2)), Is.EqualTo(8));
            Assert.That(Marshal.SizeOf(typeof(fUnitVector3)), Is.EqualTo(12));
            Assert.That(Marshal.SizeOf(typeof(fQuaternion)), Is.EqualTo(16));
            Assert.That(ManagedSize<ffloat>(), Is.EqualTo(4));
            Assert.That(ManagedSize<fVector3>(), Is.EqualTo(12));
            Assert.That(ManagedSize<fQuaternion>(), Is.EqualTo(16));
        }

        /// <summary>Managed (unsafe sizeof) size, independent of marshalling rules.</summary>
        static unsafe int ManagedSize<T>() where T : unmanaged
        {
            return sizeof(T);
        }

        [Test]
        public void HotPath_AllocatesNothing()
        {
            long probe = fMathBenchmark.AllocationProbe();
            if (probe < 0)
                Assert.Ignore("allocation probe unavailable on this runtime");

            var sim = new fDeterministicAgentSimulation();
            var snapshot = new fDeterministicAgentSimulation.Snapshot();
            var inputs = new fAgentInput[fDeterministicAgentSimulation.AgentCount];
            FillInputs(inputs, 0);
            sim.Tick(inputs); // warm up (static constructors, JIT)
            sim.SaveSnapshot(snapshot);
            sim.LoadSnapshot(snapshot);
            fQuaternion q = fQuaternion.AngleAxis(fAngle.FromDegrees(33), fUnitVector3.up);
            fVector3 v = fVector3.FromInt(3, 4, 5);
            long sink = HotLoop(q, v, 16);

            // first pass over the exact same tick sequence: one-time type initialization (boxed statics)
            // and JIT happen here, the measured second pass is the steady state
            sim.Reset();
            sink += SimulationLoop(sim, snapshot, inputs, q, v);
            // Measure three identical passes: a steady-state allocation shows up in every pass, while a
            // one-off runtime allocation (JIT tier-up, first-touch type init) cannot repeat.
            long minimum = long.MaxValue;
            for (int pass = 0; pass < 3; pass++)
            {
                sim.Reset();
                long before = fMathBenchmark.AllocationProbe();
                sink += SimulationLoop(sim, snapshot, inputs, q, v);
                long after = fMathBenchmark.AllocationProbe();
                TestContext.WriteLine($"pass {pass}: {after - before} bytes");
                minimum = Math.Min(minimum, after - before);
            }
            Assert.That(minimum, Is.EqualTo(0L), "managed allocation in the hot path");
            Assert.That(sink, Is.Not.EqualTo(long.MinValue));
        }

        static long SimulationLoop(fDeterministicAgentSimulation sim, fDeterministicAgentSimulation.Snapshot snapshot, fAgentInput[] inputs, fQuaternion q, fVector3 v)
        {
            long sink = 0;
            for (int t = 0; t < 200; t++)
            {
                FillInputs(inputs, t);
                sim.Tick(inputs);
                sim.SaveSnapshot(snapshot);
                sim.LoadSnapshot(snapshot);
                sink += (long)sim.ComputeWorldHash();
            }
            return sink + HotLoop(q, v, 1000);
        }

        static long HotLoop(fQuaternion q, fVector3 v, int n)
        {
            long acc = 0;
            for (int i = 0; i < n; i++)
            {
                fVector3 r = q * v;
                fVector3.TryNormalize(r, out fUnitVector3 u);
                q = fQuaternion.Slerp(q, fQuaternion.FromToRotation(u, fUnitVector3.forward), ffloat.Half);
                acc += fTrig.Sin(fAngle.FromRaw((uint)i * 7919u)).RawValue + fTrig.Atan2(r.x, r.z).RawValue;
                acc += (ffloat.Sqrt(r.x * r.x) / (r.y + ffloat.One)).RawValue + fVector3.DotWide(r, v);
            }
            return acc;
        }
    }
}
