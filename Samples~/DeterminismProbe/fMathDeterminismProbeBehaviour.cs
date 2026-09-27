using System.Text;
using fMath.Diagnostics;
using UnityEngine;

/// <summary>
/// Drop on a GameObject in an empty scene and build for the target (Windows / Android ARM64 / iOS ARM64
/// IL2CPP, Release). It shows the raw hash, the 10000-tick simulation hash and benchmark results; the
/// hashes must equal the golden values in Tests/Runtime/fDeterminismTests.cs.
/// </summary>
public class fMathDeterminismProbeBehaviour : MonoBehaviour
{
    public bool runBenchmarks = true;

    string _report = "running...";
    Vector2 _scroll;

    void Start()
    {
        var sb = new StringBuilder();
        sb.AppendLine("fMath determinism probe - " + Application.platform + " " + SystemInfo.processorType);
        sb.AppendLine("raw hash: " + fMathRawHashProbe.Run(fMathDeterminismCorpus.Generate()));

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
        sb.AppendLine("simulation: final " + sim.ComputeWorldHash().ToString("X16") + ", chain " + chain.Value.ToString("X16"));
        sb.AppendLine("expected:   combined 08BDC5891B892EA1, final 13D4456E86187E21, chain AB4052045BE65F56");

        if (runBenchmarks)
        {
            sb.AppendLine(fMathBenchmark.ToMarkdown("micro", fMathBenchmark.RunMicro(500000)));
            sb.AppendLine(fMathBenchmark.ToMarkdown("game-like", fMathBenchmark.RunGameLike(1000), "ns/frame"));
        }
        _report = sb.ToString();
        Debug.Log(_report);
    }

    void OnGUI()
    {
        _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Width(Screen.width), GUILayout.Height(Screen.height));
        GUILayout.Label(_report);
        GUILayout.EndScrollView();
    }
}
