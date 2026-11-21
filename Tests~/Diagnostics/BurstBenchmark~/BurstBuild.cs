using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

// Place in an Editor-only assembly in the isolated measurement project.
public static class BurstBuild
{
    public static void Run()
    {
        bool validate = Array.IndexOf(Environment.GetCommandLineArgs(), "-fmathValidate") >= 0;
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
        PlayerSettings.runInBackground = true;
        // Burst 2 moved AOT settings into the Editor module. Persist the same settings as the
        // Project Settings UI, then clean the build cache below; their individual effects were not isolated.
        Type settingsType = Type.GetType("Unity.Burst.Editor.BurstPlatformAotSettings, UnityEditor.BurstModule", true);
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        var settings = (SerializedObject)settingsType.GetMethod("GetSerializedSettings", flags).Invoke(null, new object[] { BuildTarget.StandaloneWindows64 });
        settings.FindProperty("EnableBurstCompilation").boolValue = true;
        settings.FindProperty("EnableOptimisations").boolValue = true;
        settings.FindProperty("EnableSafetyChecks").boolValue = false;
        settings.ApplyModifiedPropertiesWithoutUndo();
        settingsType.GetMethod("Save", flags).Invoke(settings.targetObject, new object[] { (BuildTarget?)BuildTarget.StandaloneWindows64 });
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var camera = new GameObject("Camera").AddComponent<Camera>(); camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.08f, 0.13f, 0.2f);
        Type type = Type.GetType("BurstBench, fMath.Tests", true);
        Component bench = new GameObject("BurstBench").AddComponent(type); type.GetField("BenchmarkCamera").SetValue(bench, camera);
        EditorSceneManager.SaveScene(scene, "Assets/BurstBench.unity");
        string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../fMath-burst/" + (validate ? "ValidatePlayer" : "Player") + "/BurstBench.exe"));
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { "Assets/BurstBench.unity" }, locationPathName = output, target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development | BuildOptions.IncludeTestAssemblies | BuildOptions.CleanBuildCache, extraScriptingDefines = validate ? new[] { "FMATH_VALIDATE" } : Array.Empty<string>() });
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(output), "build-result.txt"), report.summary.result.ToString());
        EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
    }
}
