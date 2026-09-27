using System;
using System.IO;
using fMath.Diagnostics;

namespace fMath.Tools
{
    static class Program
    {
        static int Main(string[] args)
        {
            string repoRoot = FindRepoRoot();
            string coreDirectory = Path.Combine(repoRoot, "Runtime", "Core");
            string corpusPath = Path.Combine(repoRoot, "Tests", "Determinism", "fmath_v2_input_vectors.bin");
            string command = args.Length > 0 ? args[0] : "help";

            switch (command)
            {
                case "generate-lut":
                    GenerateTrigLut.WriteAll(coreDirectory);
                    Console.WriteLine("Trig LUTs written to " + coreDirectory);
                    return 0;

                case "verify-lut":
                {
                    bool ok = GenerateTrigLut.Verify(coreDirectory, out string message);
                    Console.WriteLine(message);
                    return ok ? 0 : 1;
                }

                case "write-corpus":
                    Directory.CreateDirectory(Path.GetDirectoryName(corpusPath));
                    using (var stream = File.Create(corpusPath))
                        fMathDeterminismCorpus.Generate().Write(stream);
                    Console.WriteLine("Corpus written to " + corpusPath);
                    return 0;

                case "hash":
                {
                    fMathRawHashResult result = fMathRawHashProbe.Run(fMathDeterminismCorpus.Generate());
                    Console.WriteLine(result);
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
                    Console.WriteLine("simulation 10000 ticks: final " + sim.ComputeWorldHash().ToString("X16") + ", chain " + chain.Value.ToString("X16"));
                    return 0;
                }

                case "benchmark":
                {
                    var micro = fMathBenchmark.RunMicro();
                    var game = fMathBenchmark.RunGameLike();
                    Console.WriteLine(fMathBenchmark.ToMarkdown("micro (" + Environment.Version + ")", micro));
                    Console.WriteLine(fMathBenchmark.ToMarkdown("game-like", game, "ns/frame"));
                    return 0;
                }

                case "precision-report":
                {
                    string docs = Path.Combine(repoRoot, "Documentation~");
                    Directory.CreateDirectory(docs);
                    PrecisionReport.Write(Path.Combine(docs, "PrecisionReport.md"), Path.Combine(docs, "PrecisionReport.csv"));
                    Console.WriteLine("Precision report written to " + docs);
                    return 0;
                }

                default:
                    Console.WriteLine("commands: generate-lut | verify-lut | write-corpus | hash | benchmark | precision-report");
                    return command == "help" ? 0 : 1;
            }
        }

        static string FindRepoRoot()
        {
            string dir = AppContext.BaseDirectory;
            while (dir != null && !Directory.Exists(Path.Combine(dir, "Runtime", "Core")))
                dir = Path.GetDirectoryName(dir);
            if (dir == null)
                throw new InvalidOperationException("repository root not found");
            return dir;
        }
    }
}
