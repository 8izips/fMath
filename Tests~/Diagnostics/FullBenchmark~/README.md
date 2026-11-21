# Full benchmark harness

通常のUnityインポートから除外する検証コード。既存のfMath.DiagnosticsのRunMicro / RunGameLikeを変更せず実行する。
最小Unity 6000.6.7f1プロジェクトへCoreとDiagnosticsをコピーし、FullBenchmark.csを通常の検証用アセンブリ、
FullBenchmarkBuild.csをEditor専用アセンブリへコピーする。製品側のasmdef追加は不要。
今回の隔離環境ではfMath.FullBenchmarkがfMath / fMath.Diagnosticsを参照し、Editor側はfMath.FullBenchmarkを参照した。
imgui / jsonserialize / imageconversion / screencaptureモジュールとBurst 2を導入する。

Unity Editorで`-executeMethod FullBenchmarkBuild.Run`を指定してWindows Mono Development Playerをビルドする。
出力先は今回の隔離プロジェクト配置用なので、別配置では調整する。ビルド上限180秒。
Playerは`-batchmode -force-d3d11 -screen-width 640 -screen-height 360`で実行する。`-nographics`は使わない。
gate-ready.txtを待ち、最初の18行・2グループ・割り当て0・first-frame.pngを確認してからcontinue.txtを作る。
最初の反復を集計へ含め、残り5回を取得する。gate上限120秒、Player全体上限180秒。
complete.txtと108行CSVの全反復・group時刻・suite checksum・割り当てを照合する。

Profiler収集は行わず、既存計測器のStopwatch結果と外側グループ境界を保存する。
全18項目は既存managed APIの測定であり、Burst入口の比較は隣のBurstBenchmark~ハーネスと対応資料を使う。
