# Determinism Probe

このサンプルは任意の診断アセンブリ `fMath.Diagnostics` に依存する。
インポート前に、パッケージの `Tests~/Diagnostics` フォルダ全体をプロジェクトの
`Assets/fMathDiagnostics` へコピーする（末尾の `~` を外す）。

サンプルをインポートし、空のシーンのGameObjectに `fMathDeterminismProbeBehaviour` を追加して対象端末向けにビルドする。
raw hashとsimulation hashを `Tests~/Runtime/fDeterminismTests.cs` のgolden値と比較する。

`Tests~` 全体を既にプロジェクトにコピーしている場合、診断フォルダの追加コピーは不要。
