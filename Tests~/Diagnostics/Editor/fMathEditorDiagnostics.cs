#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using fMath.Diagnostics;
using UnityEditor;
using UnityEngine;

namespace fMath.Tools
{
    /// <summary>
    /// Editor entry points for the determinism probe and benchmarks.
    /// Batch mode: Unity -batchmode -projectPath ... -executeMethod fMath.Tools.fMathEditorDiagnostics.RunBatch -fmathOut path
    /// </summary>
    public static class fMathEditorDiagnostics
    {
        [MenuItem("Tools/fMath/Run Determinism Probe")]
        public static void RunProbeMenu() => Debug.Log(BuildReport(includeBenchmarks: false));

        [MenuItem("Tools/fMath/Run Benchmarks")]
        public static void RunBenchmarksMenu() => Debug.Log(BuildReport(includeBenchmarks: true));

        public static void RunBatch()
        {
            string outPath = "fMathDiagnostics.md";
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-fmathOut")
                    outPath = args[i + 1];
            int code = 0;
            try
            {
                File.WriteAllText(outPath, BuildReport(includeBenchmarks: true));
            }
            catch (Exception e)
            {
                File.WriteAllText(outPath, "failed: " + e);
                code = 1;
            }
            EditorApplication.Exit(code);
        }

        public static string BuildReport(bool includeBenchmarks)
        {
            var sb = new StringBuilder();
            sb.Append("# fMath diagnostics (").Append(Application.unityVersion).Append(", ").Append(Application.platform)
              .Append(", ").Append(Environment.Version).Append(", validate ").Append(fMathValidation.IsEnabled).Append(")\n\n");
            sb.Append("raw hash: ").Append(fMathRawHashProbe.Run(fMathDeterminismCorpus.Generate())).Append("\n\n");

            var sim = new fDeterministicAgentSimulation();
            var inputs = new fAgentInput[fDeterministicAgentSimulation.AgentCount];
            var chain = fRawHash.Create();
            for (int t = 0; t < 10000; t++)
            {
                for (int a = 0; a < inputs.Length; a++)
                    inputs[a] = fDeterministicAgentSimulation.ScriptedInput(0xC0FFEE, t, a);
                sim.Tick(inputs);
                chain.Add(sim.ComputeWorldHash());
            }
            sb.Append("simulation 10000 ticks: final ").Append(sim.ComputeWorldHash().ToString("X16"))
              .Append(", chain ").Append(chain.Value.ToString("X16")).Append("\n\n");

            if (includeBenchmarks)
            {
                sb.Append(fMathBenchmark.ToMarkdown("micro", fMathBenchmark.RunMicro())).Append('\n');
                sb.Append(fMathBenchmark.ToMarkdown("game-like", fMathBenchmark.RunGameLike(), "ns/frame")).Append('\n');
            }
            return sb.ToString();
        }
    }
}
#endif
