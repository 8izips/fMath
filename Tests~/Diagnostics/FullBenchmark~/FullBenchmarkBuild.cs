using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class FullBenchmarkBuild
{
    public static void Run()
    {
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
        PlayerSettings.runInBackground=true;
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var camera=new GameObject("Camera").AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(0.08f,0.13f,0.2f);
        new GameObject("FullBenchmark").AddComponent<FullBenchmark>().BenchmarkCamera=camera;
        EditorSceneManager.SaveScene(scene,"Assets/FullBenchmark/FullBenchmark.unity");
        string output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../fMath-full-final/Player/FullBenchmark.exe"));
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {scenes=new[]{"Assets/FullBenchmark/FullBenchmark.unity"},locationPathName=output,target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development | BuildOptions.CleanBuildCache});
        File.WriteAllText(Path.Combine(Application.dataPath,"../../../fMath-full-final/build-result.txt"),report.summary.result+"\n"+report.summary.totalTime);
        EditorApplication.Exit(report.summary.result==BuildResult.Succeeded?0:1);
    }
}

