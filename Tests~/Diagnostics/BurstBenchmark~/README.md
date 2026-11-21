# Burst comparison harness

検証用であり通常のUnityインポートに含めない。既存のTests~構成とasmdef数を維持する。
隔離したUnity 6000.6.7f1プロジェクトにCore、Unity変換、Tests~/Runtime、Diagnosticsとdeterminism corpusをコピーする。
Tests~/Runtime/fMathBurstTests.csも既存のfMath.Testsへ含める。

BurstBench.csを同じfMath.Testsに、BurstBuild.csをEditor専用アセンブリにコピーする。
後者はreflectionでテストComponentとBurst 2のAOT設定へアクセスするため、製品のasmdefを増やす必要はない。
Test Framework1.8と組込みBurst2を導入し、Unity Editorの`-executeMethod BurstBuild.Run`でビルドする。
ハーネスの出力先は今回の隔離プロジェクト配置を前提としているため、別配置では出力先を調整する。
Development / Mono / IncludeTestAssemblies、明示AOT設定、CleanBuildCacheを使用する。

Playerを`-batchmode -force-d3d11 -screen-width 640 -screen-height 360`で実行する。`-nographics`は使用しない。
最初の16行を保存するとgate-ready.txtを書いて待機する。parity.txt、CSVのchecksum/割り当て、first-frame.pngを確認後、
出力フォルダにcontinue.txtを作る。残りの6組を実行してcomplete.txtと96行CSVを保存する。
初回gateの待機上限120秒。Player全体の上限180秒、ビルド上限180秒を外側の起動スクリプトで設定する。

`-fmathValidate`をEditorのビルド引数に加えるとFMATH_VALIDATE有効の別Playerを作る。
そのPlayerは`-parityOnly`で起動し、rawと診断差だけを検証する（上限90秒）。性能表へ混ぜない。
独自のProfiler収集は行わず、Stopwatchのブロック境界・Player frame番号をCSVへ記録する。
単発のchecksumは計測内、バッチのchecksumは計測外。結果は方式内で比較する。
