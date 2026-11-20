# 診断ツール

`fMath.Diagnostics` は決定性probe・8 agent simulation・benchmarkを提供する。
NUnitやテストアセンブリには依存せず、実機サンプルからも利用できる。
`Editor/` の `fMath.Diagnostics.Editor` は診断メニューとbatch実行を提供するEditor専用アセンブリ。

診断だけをUnityで利用する場合は、このフォルダを `Assets/fMathDiagnostics` へコピーし、
fMath本体を導入した上で `Samples~/DeterminismProbe` をインポートする。
`Tests~` 全体を既にコピーしている場合、追加のコピーは不要。
テスト実行手順は、ひとつ上の `README.md` を参照。

`DotNet~/` はオフライン用のテスト・LUT生成・corpus生成・hash・benchmark・精度レポート生成と
.NET Standard 2.1互換性チェックを提供する。Unityの実行には不要だが、精度レポートの再生成処理を含む。
元のパッケージ内のソース配置を参照するため、コピー先ではなく元のパッケージ内で実行する。
