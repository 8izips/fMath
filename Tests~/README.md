# テストと診断

このフォルダは末尾の `~` により、通常はUnityにインポートされない。

```text
Tests~/
  Runtime/              数値・Unity変換・決定性テスト（fMath.Tests）
  Determinism/          固定入力corpus
  Diagnostics/          実機でも動作する診断コード（fMath.Diagnostics）
    Editor/             診断メニュー（fMath.Diagnostics.Editor）
    DotNet~/            Unity外で実行する開発用ツール
```

Unityでテストを実行するには、フォルダ全体を `Assets/fMathTests` へコピーし、
Unity Test RunnerのPlayModeテストを実行する。コピー先のフォルダ名に `~` を付けない。
fMath本体とUnity Test Frameworkが必要。`Runtime/` と `Determinism/` の相対配置を維持する。
パッケージを `testables` に追加するだけでは、このフォルダはインポートされない。

テスト用asmdefは `Runtime/fMath.Tests.asmdef` の1つ。
Diagnosticsはテストアセンブリに依存しないため、通常のPlayerビルドでも利用できる。
診断メニューは `Tools/fMath/Run Determinism Probe` と `Tools/fMath/Run Benchmarks`。

実機サンプル用の診断だけが必要なら、`Diagnostics/` のみを `Assets/fMathDiagnostics` へコピーし、
`Samples~/DeterminismProbe` をインポートする。フォルダ全体と診断だけのコピーを併用しないこと。

`Diagnostics/DotNet~` は元のパッケージ内のソースを直接参照するため、元の場所で実行する。
Unity依存の変換テストはオフライン用プロジェクトの対象外。
