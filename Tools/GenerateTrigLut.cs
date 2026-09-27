using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace fMath.Tools
{
    /// <summary>
    /// Offline generator for the trigonometry look-up tables compiled into Runtime/Core.
    ///
    /// The tables are evaluated with System.Decimal Taylor series (28 significant digits), not with
    /// Math.Sin/Math.Atan, so regenerating them yields bit-identical output on every machine.
    /// The runtime never generates tables; it only reads the committed source files.
    ///
    ///   fTrigSinQuarterLut.cs : sin(i/4096 * 90°) in Q1.30, i = 0..4096
    ///   fTrigAtanLut.cs       : atan(i/4096) in Angle32 raw units, i = 0..4096
    ///
    /// Run from Unity (Tools/fMath/Regenerate Trig LUT) or offline:
    ///   dotnet run --project DotNet~/fMath.Tools -- generate-lut
    /// </summary>
    public static class GenerateTrigLut
    {
        public const int SinIntervals = 4096;
        public const int AtanIntervals = 4096;
        public const string SinFileName = "fTrigSinQuarterLut.cs";
        public const string AtanFileName = "fTrigAtanLut.cs";

        const decimal Pi = 3.1415926535897932384626433833m;
        const decimal Q30 = 1073741824m;
        const decimal Turn32 = 4294967296m;

        public static int[] ComputeSinQuarterTable()
        {
            var table = new int[SinIntervals + 1];
            for (int i = 0; i <= SinIntervals; i++)
            {
                decimal x = Pi / 2m * i / SinIntervals;
                table[i] = (int)decimal.Round(SinDecimal(x) * Q30, MidpointRounding.ToEven);
            }
            table[0] = 0;
            table[SinIntervals] = 1 << 30; // exact endpoints
            return table;
        }

        public static uint[] ComputeAtanOctantTable()
        {
            var table = new uint[AtanIntervals + 1];
            for (int i = 0; i <= AtanIntervals; i++)
            {
                decimal t = (decimal)i / AtanIntervals;
                table[i] = (uint)decimal.Round(AtanDecimal(t) / (2m * Pi) * Turn32, MidpointRounding.ToEven);
            }
            table[0] = 0;
            table[AtanIntervals] = 1u << 29; // atan(1) = 1/8 turn exactly
            return table;
        }

        static decimal SinDecimal(decimal x)
        {
            decimal term = x, sum = x, x2 = x * x;
            for (int n = 1; n < 40; n++)
            {
                term = -term * x2 / ((2 * n) * (2 * n + 1));
                if (term == 0m) break;
                sum += term;
            }
            return sum;
        }

        static decimal AtanDecimal(decimal t)
        {
            // atan(t) = pi/4 + atan((t - 1) / (t + 1)) keeps the series argument below 0.4143
            if (t > 0.4142135623730950488m)
                return Pi / 4m + AtanSeries((t - 1m) / (t + 1m));
            return AtanSeries(t);
        }

        static decimal AtanSeries(decimal u)
        {
            decimal u2 = u * u, power = u, sum = u;
            for (int n = 1; n < 200; n++)
            {
                power = -power * u2;
                decimal term = power / (2 * n + 1);
                if (term == 0m) break;
                sum += term;
            }
            return sum;
        }

        public static string BuildSinLutSource()
        {
            int[] table = ComputeSinQuarterTable();
            var sb = new StringBuilder();
            AppendHeader(sb, "sin(i / " + SinIntervals + " * 90 degrees) in Q1.30, i = 0.." + SinIntervals + ".");
            sb.Append("internal static class fTrigSinQuarterLut\n{\n");
            sb.Append("    internal const int Intervals = ").Append(SinIntervals).Append(";\n\n");
            sb.Append("    internal static readonly int[] Table = new int[]\n    {\n");
            for (int i = 0; i < table.Length; i++)
            {
                if (i % 8 == 0) sb.Append("        ");
                sb.Append(table[i].ToString(CultureInfo.InvariantCulture)).Append(',');
                sb.Append(i % 8 == 7 || i == table.Length - 1 ? "\n" : " ");
            }
            sb.Append("    };\n}\n");
            return sb.ToString();
        }

        public static string BuildAtanLutSource()
        {
            uint[] table = ComputeAtanOctantTable();
            var sb = new StringBuilder();
            AppendHeader(sb, "atan(i / " + AtanIntervals + ") in Angle32 raw units (2^32 per turn), i = 0.." + AtanIntervals + ".");
            sb.Append("internal static class fTrigAtanLut\n{\n");
            sb.Append("    internal const int Intervals = ").Append(AtanIntervals).Append(";\n\n");
            sb.Append("    internal static readonly uint[] Table = new uint[]\n    {\n");
            for (int i = 0; i < table.Length; i++)
            {
                if (i % 8 == 0) sb.Append("        ");
                sb.Append(table[i].ToString(CultureInfo.InvariantCulture)).Append("u,");
                sb.Append(i % 8 == 7 || i == table.Length - 1 ? "\n" : " ");
            }
            sb.Append("    };\n}\n");
            return sb.ToString();
        }

        static void AppendHeader(StringBuilder sb, string description)
        {
            sb.Append("// <auto-generated>\n");
            sb.Append("// Generated by Tools/GenerateTrigLut.cs. Do not edit by hand.\n");
            sb.Append("// ").Append(description).Append('\n');
            sb.Append("// Evaluated with System.Decimal series and rounded to nearest (ties to even).\n");
            sb.Append("// </auto-generated>\n\n");
        }

        /// <summary>Writes both tables into the Runtime/Core directory.</summary>
        public static void WriteAll(string coreDirectory)
        {
            File.WriteAllText(Path.Combine(coreDirectory, SinFileName), BuildSinLutSource(), new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(coreDirectory, AtanFileName), BuildAtanLutSource(), new UTF8Encoding(false));
        }

        /// <summary>True when the committed tables match a fresh generation.</summary>
        public static bool Verify(string coreDirectory, out string message)
        {
            string sinPath = Path.Combine(coreDirectory, SinFileName);
            string atanPath = Path.Combine(coreDirectory, AtanFileName);
            bool sinOk = File.Exists(sinPath) && Normalize(File.ReadAllText(sinPath)) == BuildSinLutSource();
            bool atanOk = File.Exists(atanPath) && Normalize(File.ReadAllText(atanPath)) == BuildAtanLutSource();
            message = (sinOk ? "sin LUT ok" : "sin LUT differs") + ", " + (atanOk ? "atan LUT ok" : "atan LUT differs");
            return sinOk && atanOk;
        }

        static string Normalize(string text) => text.Replace("\r\n", "\n");

#if UNITY_EDITOR
        [UnityEditor.MenuItem("Tools/fMath/Regenerate Trig LUT")]
        static void RegenerateFromMenu()
        {
            string[] guids = UnityEditor.AssetDatabase.FindAssets("fTrigSinQuarterLut t:MonoScript");
            if (guids.Length == 0)
            {
                UnityEngine.Debug.LogError("fMath: fTrigSinQuarterLut.cs not found.");
                return;
            }
            string assetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
            string directory = Path.GetDirectoryName(Path.GetFullPath(assetPath));
            WriteAll(directory);
            UnityEditor.AssetDatabase.Refresh();
            UnityEngine.Debug.Log("fMath: trig LUTs regenerated in " + directory);
        }

        [UnityEditor.MenuItem("Tools/fMath/Verify Trig LUT")]
        static void VerifyFromMenu()
        {
            string[] guids = UnityEditor.AssetDatabase.FindAssets("fTrigSinQuarterLut t:MonoScript");
            if (guids.Length == 0)
            {
                UnityEngine.Debug.LogError("fMath: fTrigSinQuarterLut.cs not found.");
                return;
            }
            string directory = Path.GetDirectoryName(Path.GetFullPath(UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0])));
            bool ok = Verify(directory, out string message);
            if (ok) UnityEngine.Debug.Log("fMath: " + message);
            else UnityEngine.Debug.LogError("fMath: " + message);
        }
#endif
    }
}
