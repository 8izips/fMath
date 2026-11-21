using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using fMath.Diagnostics;

public sealed class FullBenchmark : MonoBehaviour
{
    public Camera BenchmarkCamera;
    struct Row { public string Suite, Group; public int Repetition, Frame; public long Start, End, Checksum; public fMathBenchmarkResult Result; }
    readonly List<Row> _rows = new List<Row>(108);
    RenderTexture _frame;
    string _output;

    IEnumerator Start()
    {
        Application.runInBackground=true;QualitySettings.vSyncCount=0;Application.targetFrameRate=-1;
        _output=Path.GetDirectoryName(Application.dataPath);
        _frame=new RenderTexture(640,360,24);_frame.Create();BenchmarkCamera.targetTexture=_frame;
        File.WriteAllText(Path.Combine(_output,"environment.txt"),
            "Unity: "+Application.unityVersion+"\nCPU: "+SystemInfo.processorType+"\nOS: "+SystemInfo.operatingSystem+
            "\nGPU: "+SystemInfo.graphicsDeviceName+"\nGraphics API: "+SystemInfo.graphicsDeviceType+
            "\nDevelopment: "+UnityEngine.Debug.isDebugBuild+"\nBackend: Mono\nPackage: built-in Burst 2.0.0; this suite calls managed scalar APIs\nFMATH_VALIDATE: "+fMathValidation.IsEnabled+
            "\nPipeline: Built-in, isolated numeric harness (URP not exercised)\nProfiler: disconnected; Deep Profiling off"+
            "\nFPS: uncapped; fixed-operation throughput blocks\nMicro iterations: 2000000 (Slerp: 500000)"+
            "\nGame frames: 3000 (snapshot copies: 300000)\nRepetitions: 6\nStopwatch frequency: "+Stopwatch.Frequency+"\n");
        for(int rep=0;rep<6;rep++)
        {
            for(int order=0;order<2;order++)
            {
                bool micro=((rep+order)&1)==0;
                BenchmarkCamera.Render();yield return null;
                long before=fMathBenchmark.Sink;long start=Stopwatch.GetTimestamp();
                List<fMathBenchmarkResult> results=micro?fMathBenchmark.RunMicro():fMathBenchmark.RunGameLike();
                long end=Stopwatch.GetTimestamp();long checksum=unchecked(fMathBenchmark.Sink-before);
                if(results.Count!=(micro?13:5))throw new InvalidOperationException("Missing benchmark results");
                string suite=micro?"micro":"game-like";
                foreach(var result in results)
                {
                    if(double.IsNaN(result.NanosecondsPerOp)||double.IsInfinity(result.NanosecondsPerOp)||result.NanosecondsPerOp<0)
                        throw new InvalidOperationException("Invalid timing");
                    _rows.Add(new Row { Suite=suite,Group=suite+"-"+rep,Repetition=rep,Frame=Time.frameCount,Start=start,End=end,Checksum=checksum,Result=result });
                }
            }
            if(rep==0)
            {
                Save();Capture("first-frame.png");
                File.WriteAllText(Path.Combine(_output,"gate-ready.txt"),"18 rows saved; waiting for first-repetition review");
                double deadline=Time.realtimeSinceStartupAsDouble+120;
                while(!File.Exists(Path.Combine(_output,"continue.txt")))
                {
                    if(Time.realtimeSinceStartupAsDouble>deadline)throw new TimeoutException("First-repetition review timed out");
                    BenchmarkCamera.Render();yield return null;
                }
            }
        }
        Save();Capture("final-frame.png");
        File.WriteAllText(Path.Combine(_output,"complete.txt"),"108 rows / 12 groups completed; all output synchronously saved");
        BenchmarkCamera.targetTexture=null;_frame.Release();Destroy(_frame);Application.Quit(0);
    }

    void Save()
    {
        var csv=new StringBuilder("suite,repetition,name,ns_per_op,operations,allocated_bytes,group_id,player_frame,group_start_ticks,group_end_ticks,group_checksum\n");
        foreach(Row row in _rows)
        {
            csv.Append(row.Suite).Append(',').Append(row.Repetition).Append(',').Append('"').Append(row.Result.Name.Replace("\"","\"\"")).Append("\",")
                .Append(row.Result.NanosecondsPerOp.ToString("G17",CultureInfo.InvariantCulture)).Append(',')
                .Append(row.Result.Operations).Append(',').Append(row.Result.AllocatedBytes).Append(',').Append(row.Group).Append(',')
                .Append(row.Frame).Append(',').Append(row.Start).Append(',').Append(row.End).Append(',').Append(row.Checksum).Append('\n');
        }
        File.WriteAllText(Path.Combine(_output,"results.csv"),csv.ToString());
    }

    void Capture(string name)
    {
        BenchmarkCamera.Render();RenderTexture previous=RenderTexture.active;RenderTexture.active=_frame;
        var image=new Texture2D(640,360,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,640,360),0,0);image.Apply();RenderTexture.active=previous;
        Color pixel=image.GetPixel(320,180);if(pixel.r==0&&pixel.g==0&&pixel.b==0)throw new InvalidOperationException("Black rendered frame");
        File.WriteAllBytes(Path.Combine(_output,name),image.EncodeToPNG());Destroy(image);
    }
}

