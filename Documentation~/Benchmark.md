# fMath v2 benchmark

Burst、Jobs、Native Pluginなし。ベンチマークは `fMath.Diagnostics.fMathBenchmark` にあり、同じコードを
dotnet（`dotnet run -c Release --project DotNet~/fMath.Tools -- benchmark`）、Unity Editor
（`Tools/fMath/Run Benchmarks`、またはbatch実行 `fMath.Tools.fMathEditorDiagnostics.RunBatch`）、
実機（`Samples~/DeterminismProbe`）で実行できる。

計測環境: Windows 11 x64（Unityプロジェクトと同じマシン）、2026-09-27。

## Unity 6000.6.0f1 Editor（Mono、Code Optimization = Debug、FMATH_VALIDATE off）

| micro | ns/op | allocated bytes |
|---|---:|---:|
| ffloat add | 11.5 | 0 |
| ffloat mul | 18.4 | 0 |
| ffloat div | 31.1 | 0 |
| ffloat sqrt | 140.4 | 0 |
| fTrig sin+cos | 50.4 | 0 |
| fTrig atan2 | 56.9 | 0 |
| fVector3 dot (wide) | 17.1 | 0 |
| fVector3 cross | 85.9 | 0 |
| fVector3 distance squared (wide) | 17.8 | 0 |
| fVector3 normalize | 320.1 | 0 |
| fQuaternion multiply | 78.1 | 0 |
| fQuaternion rotate vector | 132.1 | 0 |
| fQuaternion slerp | 776.8 | 0 |

| game-like（8 agents, 1 frame） | ns/frame | allocated bytes |
|---|---:|---:|
| normal x1 | 19589 | 0 |
| rollback 1+2 | 57297 | 0 |
| rollback 1+4 | 94810 | 0 |
| rollback 1+8 | 171678 | 0 |
| snapshot save+load（8 agents、480 B） | 80.0 | 0 |

Editor Mono（Debugコード最適化）でも、1+8 rollbackは1フレーム0.17 msで60 Hz予算（16.7 ms）の約1%。

## .NET 10 CoreCLR（Release、参考値）

| micro | ns/op | allocated bytes |
|---|---:|---:|
| ffloat add | 0.4 | 0 |
| ffloat mul | 1.5 | 0 |
| ffloat div | 5.1 | 0 |
| ffloat sqrt | 76.8 | 0 |
| fTrig sin+cos | 11.9 | 0 |
| fTrig atan2 | 14.2 | 0 |
| fVector3 dot (wide) | 1.4 | 0 |
| fVector3 cross | 2.3 | 0 |
| fVector3 distance squared (wide) | 1.3 | 0 |
| fVector3 normalize | 91.8 | 0 |
| fQuaternion multiply | 1.6 | 0 |
| fQuaternion rotate vector | 4.8 | 0 |
| fQuaternion slerp | 310.5 | 0 |

| game-like（8 agents, 1 frame） | ns/frame | allocated bytes |
|---|---:|---:|
| normal x1 | 5974 | 0 |
| rollback 1+2 | 15580 | 0 |
| rollback 1+4 | 23654 | 0 |
| rollback 1+8 | 19718 | 0 |
| snapshot save+load | 9.1 | 0 |

CoreCLRでは同じtick列を繰り返す1+8で分岐予測とキャッシュが効き、1+4より速く出ることがある
（再シミュレーション結果はbaselineとbit一致することをテストで確認済み）。

## 未計測

- Windows IL2CPP: このマシンにIL2CPPモジュールが未インストールのため未計測。
- Android ARM64 / iOS ARM64 IL2CPP実機: 今回は対象外。`Samples~/DeterminismProbe` をReleaseでビルドすると、
  raw hash、10000 tick simulation hash、上記ベンチマークを画面とログに出力する。
