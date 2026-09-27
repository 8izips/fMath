using System;
using System.IO;

namespace fMath.Tools
{
    static class Program
    {
        static int Main(string[] args)
        {
            string repoRoot = FindRepoRoot();
            string coreDirectory = Path.Combine(repoRoot, "Runtime", "Core");
            string command = args.Length > 0 ? args[0] : "help";

            switch (command)
            {
                case "generate-lut":
                    GenerateTrigLut.WriteAll(coreDirectory);
                    Console.WriteLine("Trig LUTs written to " + coreDirectory);
                    return 0;
                case "verify-lut":
                    bool ok = GenerateTrigLut.Verify(coreDirectory, out string message);
                    Console.WriteLine(message);
                    return ok ? 0 : 1;
                default:
                    Console.WriteLine("commands: generate-lut | verify-lut");
                    return command == "help" ? 0 : 1;
            }
        }

        static string FindRepoRoot()
        {
            string dir = AppContext.BaseDirectory;
            while (dir != null && !File.Exists(Path.Combine(dir, "package.json")) && !Directory.Exists(Path.Combine(dir, "Runtime", "Core")))
                dir = Path.GetDirectoryName(dir);
            if (dir == null)
                throw new InvalidOperationException("repository root not found");
            return dir;
        }
    }
}
