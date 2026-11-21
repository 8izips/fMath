# fMath v2 benchmark

既存のmanagedベンチマークは `fMath.Diagnostics.fMathBenchmark` にあり、同じコードを
dotnet（`dotnet run -c Release --project Tests~/Diagnostics/DotNet~/fMath.Tools -- benchmark`）、Unity Editor
（`Tools/fMath/Run Benchmarks`、またはbatch実行 `fMath.Tools.fMathEditorDiagnostics.RunBatch`）、
実機（`Samples~/DeterminismProbe`）で実行できる。

Unity Editorおよび実機では、先に `Tests~/Diagnostics` フォルダ全体をプロジェクトの
`Assets/fMathDiagnostics` へコピーする（末尾の `~` を外す）。通常のインポートでは診断コードは含まれない。

## 2026-11-21: 現行版の全体ベンチマーク（Burst対応・最適化を反映）

sqrt / normalize / Slerpの最適化とBurst 2依存を含む現行ソースで、既存の全18項目を各6回再測定した。
Unity 6000.6.7f1、Burst 2.0.0、Windows x64 Mono Development Player、Core Ultra 9 285K、Windows 11 10.0.26300、RTX 5080 / D3D11。
FMATH_VALIDATE off、Profiler未接続、Deep Profilingなし、数値処理用Built-in pipeline、640x360。URP/GPU負荷は対象外。
全18項目は既存のmanaged APIを呼ぶ。Burstを導入しただけでこれらの呼び出しがnative化するわけではない。
対応する3演算のnative性能は、同一CoreソースとのSHA256一致を確認した直前のBurst比較を再利用し、次節に併記した。
両測定は別Player・異なる入力分布のため、全体表のmanaged値と次節のBurst値を直接割って改善率を出さない。

| micro（managed） | 平均 ns/op | 中央値 | 最小–最大 | 割り当てbyte/区間 |
|---|---:|---:|---:|---:|
| ffloat add | 6.89 | 6.88 | 6.87–6.92 | 0 |
| ffloat mul | 11.95 | 11.92 | 11.84–12.10 | 0 |
| ffloat div | 14.83 | 14.76 | 14.44–15.28 | 0 |
| ffloat sqrt | 29.15 | 29.03 | 28.75–29.72 | 0 |
| fTrig sin+cos | 19.31 | 19.27 | 19.04–19.71 | 0 |
| fTrig atan2 | 19.78 | 19.73 | 19.38–20.31 | 0 |
| fVector3 dot (wide) | 7.31 | 7.31 | 7.30–7.32 | 0 |
| fVector3 cross | 29.54 | 29.43 | 29.09–30.38 | 0 |
| fVector3 distance squared (wide) | 7.43 | 7.40 | 7.38–7.53 | 0 |
| fVector3 normalize | 62.20 | 62.16 | 60.46–64.13 | 0 |
| fQuaternion multiply | 31.93 | 31.90 | 31.28–32.46 | 0 |
| fQuaternion rotate vector | 47.69 | 47.42 | 46.70–49.65 | 0 |
| fQuaternion slerp | 160.43 | 160.52 | 155.44–165.02 | 0 |

| game-like（managed、8 agents） | 単位 | 平均 | 中央値 | 最小–最大 | 割り当てbyte/区間 |
|---|---|---:|---:|---:|---:|
| 8 agents: normal x1 | µs/frame | 6.025 | 6.017 | 6.001–6.077 | 0 |
| 8 agents: rollback 1+2 | µs/frame | 16.353 | 16.289 | 16.273–16.678 | 0 |
| 8 agents: rollback 1+4 | µs/frame | 26.559 | 26.567 | 26.473–26.621 | 0 |
| 8 agents: rollback 1+8 | µs/frame | 47.228 | 47.118 | 47.060–47.586 | 0 |
| snapshot save+load (8 agents, 480 B) | ns/save+load | 50.611 | 50.583 | 50.033–51.311 | 0 |

`RunMicro()` / `RunGameLike()`の公開計測器は変更していない。micro各200万回（Slerp50万回）、各項目5万回×4warm-up。
game-like各3000 simulation frame、1500frame warm-up。snapshot30万組、7.5万組warm-up。
micro/game-likeの順序を反復ごとに交替し、最初の18行・割り当て・描画画像を確認後に残り5回を取得した。
全108行・12グループが完了し、各6反復、時刻境界・Player frame番号、suiteチェックサムの再現性、全区間0 byteを確認した。
平均は各反復のns/演算を算術平均。初期化、warm-up、描画、ログ・画像・ファイル保存は各項目の計測外。
固定演算数のthroughput測定なのでFPS上限なし。simulation frameは画面の1frameとは異なり、snapshotはsave+load一組。
CSVの外側group時刻は準備・warm-upを含み、チェックサムはwarm-upとsuite全項目のSink差分を表す。
同じグループの13行または5行はその外側境界を共有する。Stopwatch時間は実占有CPU時間とは区別する。

生データ: [FullBenchmark-2026-11-21-final.csv](FullBenchmark-2026-11-21-final.csv)。
環境・全反復・source/binary SHA256・Burst証拠の対応: [FullBenchmark-2026-11-21-final.json](FullBenchmark-2026-11-21-final.json)。
再現用コード: `Tests~/Diagnostics/FullBenchmark~`。Burst比較は下記の8条件・96行を使用する。
今回の既存API値と以前の別プロセス値の差は、単独の最適化効果とは扱わない。
IL2CPP・Release・ARM・Android/iOS・独立プロセスでの反復は未検証。

## 2026-11-21: Burst 2比較

Advisorの実行環境・書き込みツールを接続しない隔離セッションを修復し、事前レビュー後に導入。
Unity 6000.6.7f1、組込みBurst 2.0.0、Windows 11 10.0.26300、Core Ultra 9 285K、RTX 5080 / D3D11。
同一Windows x64 Mono Development PlayerでmanagedとBurst AOTを6組交互比較。
Profiler未接続、Deep Profilingなし、FMATH_VALIDATE off。Built-in pipeline、640x360、描画は計測外。
固定回数の演算スループットを測るためFPS上限なし。初期化・ウォームアップ・画像保存は計測外。
各ブロック307200演算、入力1024個、seed617。単発と1024要素バッチを分け、同じ方式内で比較した。
単発は全結果のチェックサムを計測内で加算し、バッチは出力書き込みを計測、チェックサムは計測外。
したがって単発とバッチの値だけから境界コストを厳密に差し引くことはできない。

| 演算 | 方式 | managed平均 ns/演算 | Burst平均 ns/演算 | 時間短縮 |
|---|---|---:|---:|---:|
| sqrt | 単発 | 24.64 | 18.01 | 26.9% |
| normalize（±2048m程度） | 単発 | 69.05 | 43.38 | 37.2% |
| Slerp（通常） | 単発 | 163.31 | 94.16 | 42.3% |
| Slerp（0.1°近接） | 単発 | 76.32 | 48.65 | 36.3% |
| sqrt | バッチ1024 | 25.59 | 10.37 | 59.5% |
| normalize（±2048m程度） | バッチ1024 | 66.57 | 23.52 | 64.7% |
| Slerp（通常） | バッチ1024 | 163.63 | 85.63 | 47.7% |
| Slerp（0.1°近接） | バッチ1024 | 77.02 | 29.89 | 61.2% |

全96行、全組のチェックサム一致、計測区間managed allocation 0 byte。
各組の順序・時刻・Player frame番号をCSVに残し、最初の16行・描画・raw検証を確認してから残りを取得した。
平均は各ブロックのns/演算を6回で算術平均。中央値・全反復・最小最大はJSONに記録。
normalize単発のBurstは38.91–47.08nsと変動があるが、この6回すべてでmanagedより短かった。

Editorの関連PlayModeテスト66件（corpus / 10000tick simulationのgolden維持を含む）が合格。
Playerでもsqrt・normalize・Slerpの各10万入力について全要素raw値と成功フラグが一致した。
6つの実際のscalar/batch入口それぞれのBurstDiscard markerでnative実行を確認。
両LUT、近接分岐境界のQ1.30隣接値、負dot、t端点・範囲外、極値、ゼロvector、負sqrtを含む。
バッファ前後のsentinel、zero-count/null、負count拒否も確認。
別のFMATH_VALIDATE on Playerでも同じ各10万入力が一致し、managedの診断callback/counterとnativeの省略を確認した。

最初のPlayerは生成nativeライブラリがなくmanaged fallbackを検出したため不採用。
AOT設定の明示保存とCleanBuildCacheで再ビルドし、`lib_burst_generated.dll`と入口markerの両方を確認した。
失敗したPlayerの測定値は採用していない。キャッシュと設定保存を同時に変更したため個別の寄与は未分離。

生データ: `BurstBenchmark-2026-11-21.csv`。環境・反復・ソース/native artifact SHA256: `BurstBenchmark-2026-11-21.json`。
再現ハーネスは`Tests~/Diagnostics/BurstBenchmark~`。導入条件とバッファ契約は`Burst.md`。
1プロセス内の6組であり独立プロセス反復ではない。IL2CPP・Release・ARM・Android/iOS実機は未検証。
以前の全体18項目は非Burst基準であり、この表を全APIのBurst性能と解釈しない。

## 2026-11-21: Slerpのraw互換最適化

以下はBurst依存化前に行った通常C#の局所最適化の記録。現在はAdvisor環境を修復してBurst依存化を再開している。
この節の計測時点ではpackage.json・asmdefの依存関係、数値形式、演算結果を変更していない。

Slerp / RotateTowardsの近接分岐は、短い弧を既に選択しているため、正規化線形補間内の重複DotWideを省いた。
LerpUnclampedと同じ積和・丸め順序を共有し、インライン化を指定した。
半角の成分は非負Q1.30と分かっているため、同じatan LUTと除算丸めを直接使い、汎用atan2の符号判定・bit長計算を省いた。
汎用Atan2Wideの処理順は元のまま。角度しきい値や、近接以外でのtrigベースSlerpは変更していない。

Unity 6000.6.7f1、Windows Mono Development Player、Core Ultra 9 285K、Windows 11 10.0.26300、RTX 5080 / Direct3D11。
Burst不使用、FMATH_VALIDATE off、Profiler未接続、Deep Profiling off。一時プロジェクトのBuilt-in pipelineで計測し、URPの描画負荷は対象外。
seed 617の1024入力、3万回×3回warm-up後に各30万回を6組、旧→新／新→旧を交互に実行した。
最初の6ペアで出力・checksum一致・割り当て0 byte・非黒の描画画像を確認してから残りを取得し、最初の組も集計に含む。

| 処理 | 旧: 平均ns/op（最小–最大） | 新: 平均ns/op（最小–最大） | 平均処理時間の短縮 |
|---|---:|---:|---:|
| slerp random | 168.98（165.46–171.80） | 163.91（159.26–167.37） | 3.0% |
| slerp close | 86.41（84.11–88.58） | 81.24（78.06–84.28） | 6.0% |
| slerp close negative dot | 91.44（87.77–95.71） | 84.97（83.51–86.14） | 7.1% |
| atan2 control | 27.64（23.91–30.79） | 27.02（25.54–28.76） | 2.2% |
| lerp control | 78.74（76.58–80.69） | 79.68（78.49–80.63） | -1.2% |
| rotate-towards close | 183.90（181.17–185.28） | 164.00（162.40–166.66） | 10.8% |

randomはランダムな正規化Quaternion同士、closeは約0.1°差、negative dotはcloseの相手Quaternionの全成分を反転。
tは0.5、RotateTowardsの最大ステップはAngle32 raw 4096。全成分の結果をchecksumへ加算し、計測には入力参照・呼び出し・checksum加算を含む。
初期化・warm-up・描画・出力・フレーム間待機は区間外。固定回数のthroughput測定なのでFPSは制限しない。
ns/opはStopwatch経過時間で、実占有CPU時間やゲームのフレーム時間ではない。

全72区間でchecksumが新旧一致し、割り当ては0 byte。フレーム番号は区間ごとに一意で、開始終了timestampの欠落・重複・重なりがない。
関連PlayModeテスト47件を通過。Slerp・LerpUnclamped・RotateTowards・汎用atan2をそれぞれ10万ケースで変更前と比較し、raw値が一致した。
既存corpusと10000tick simulationのgolden hashも維持。

Lerp controlは平均約1.2%増加したが、反復範囲が重なるため、この一度のプロセス内比較では性能差を確定しない。
atan2 controlも範囲が重なり、改善とは主張しない。通常Slerpの改善は小幅であり、上表の変動と併せて解釈する。
初回候補はLerpの遅延が出たため不採用とし、元データを作業用ディレクトリに保持した。
本表は修正後の追加1回のPlayer実行。別プロセス反復、IL2CPP、ARM、モバイル、Release Player、Burstでの結果は未検証。

生データ: [SlerpBenchmark-2026-11-21.csv](SlerpBenchmark-2026-11-21.csv)。
環境・source / binary SHA-256・全反復・集計: [SlerpBenchmark-2026-11-21.json](SlerpBenchmark-2026-11-21.json)。
以下の全体ベンチマークとは入力分布・checksum対象・反復数が異なるため、絶対値の差を追加の速度変化とみなさない。

## 2026-11-21: 全体ベンチマーク（Slerp最適化前の基準）

乗算・除算のchecksum修正とsqrt / normalize最適化を含む現行版について、公開の
`fMathBenchmark.RunMicro()` と `RunGameLike()` を変更せずに実行した。
Unity 6000.6.7f1、Windows x64 **Mono Development Player**、Intel Core Ultra 9 285K、Windows 11 10.0.26300、RTX 5080 / Direct3D11。
FMATH_VALIDATE off、Burst / Jobsなし、Profiler未接続、Deep Profiling off。
前回の整数カーネル比較と同じ一時プロジェクトを使用し、数値処理用のBuilt-in pipelineで計測。製品のURP描画は対象外。

同一プロセス内で全18項目を6回測定し、micro→game-like／game-like→microの順序を交互にした。
microは各200万回、Slerpのみ50万回。各項目の直前に5万回×4回warm-up。
game-likeは各3000 simulationフレーム、各項目でring初期化後に1500フレームwarm-up。
snapshotは30万回のsave+load、その前に7.5万回warm-up。
最初の1回で全項目・反復数・割り当て0 byte・非黒の描画画像を確認してから、残り5回へ進んだ。最初の回も集計に含む。

| micro | 平均ns/op | 中央値 | 最小–最大 | 割り当てbyte/区間 |
|---|---:|---:|---:|---:|
| ffloat add | 6.89 | 6.88 | 6.87–6.91 | 0 |
| ffloat mul | 11.95 | 11.97 | 11.79–12.09 | 0 |
| ffloat div | 13.48 | 13.50 | 12.95–13.90 | 0 |
| ffloat sqrt | 29.10 | 29.01 | 28.87–29.63 | 0 |
| fTrig sin+cos | 19.42 | 19.42 | 18.98–19.87 | 0 |
| fTrig atan2 | 19.89 | 19.92 | 19.05–20.59 | 0 |
| fVector3 dot (wide) | 7.56 | 7.55 | 7.55–7.57 | 0 |
| fVector3 cross | 29.16 | 28.95 | 28.06–30.54 | 0 |
| fVector3 distance squared (wide) | 7.44 | 7.41 | 7.39–7.58 | 0 |
| fVector3 normalize | 60.80 | 60.63 | 59.04–62.56 | 0 |
| fQuaternion multiply | 30.16 | 30.01 | 29.50–31.29 | 0 |
| fQuaternion rotate vector | 47.68 | 47.19 | 46.79–50.27 | 0 |
| fQuaternion slerp | 162.55 | 160.35 | 159.81–173.21 | 0 |

| game-like（8 agents） | 単位 | 平均 | 中央値 | 最小–最大 | 割り当てbyte/区間 |
|---|---|---:|---:|---:|---:|
| 8 agents: normal x1 | µs/frame | 6.027 | 6.021 | 6.003–6.088 | 0 |
| 8 agents: rollback 1+2 | µs/frame | 16.375 | 16.379 | 16.346–16.398 | 0 |
| 8 agents: rollback 1+4 | µs/frame | 26.745 | 26.765 | 26.626–26.810 | 0 |
| 8 agents: rollback 1+8 | µs/frame | 47.450 | 47.414 | 47.334–47.717 | 0 |
| snapshot save+load (8 agents, 480 B) | ns/save+load | 64.584 | 64.624 | 63.852–65.315 | 0 |

値は既存計測器内のStopwatch経過時間を処理回数で割ったもの。入力参照、関数呼び出し、checksumへの反映を含む。
初期化、warm-up、描画、フレーム間の待機、ログ・ファイル出力は各項目の計測区間外。
固定回数のthroughput測定なのでFPS制限は使わない。これは実占有CPU時間やゲーム全体のフレーム時間ではない。
game-likeのframeはsimulationの1反復で、rollbackはrestoreとN+1 tick等を含む。snapshotの単位はsave+load一組である。

全108行・12実行グループが揃い、各項目の反復数・group境界・Playerフレーム番号に欠落や重複がないことを確認した。
CSVのグループ開始終了timestampは準備・warm-upも含む外側の境界であり、各項目の計測時間そのものではない。
micro 13行またはgame-like 5行は同じ実行グループの境界とPlayerフレーム番号を共有する。
CSVのchecksumはsuite全体のSink差分（warm-up込み）で、各項目単独のchecksumではない。同じsuiteの6回で再現した。
全項目・全6回の計測区間でmanaged allocationは0 byte。

生データ: [FullBenchmark-2026-11-21.csv](FullBenchmark-2026-11-21.csv)。
条件・source / binary SHA-256・6回分の値・集計: [FullBenchmark-2026-11-21.json](FullBenchmark-2026-11-21.json)。
前回のsqrt / normalize個別比較とは入力分布・反復数・checksum対象成分が異なるため、数値の差を追加の最適化効果とは扱わない。
以前のEditor / CoreCLRの履歴値との直接比較もしない。現在版のEditor・CoreCLR・IL2CPP・ARM・モバイル・Release Playerは今回計測していない。
Burstの有無による比較も未実施であり、この結果からBurstが不要とは判断できない。

## 2026-11-21: sqrt / normalizeの整数カーネル最適化

同一Windows x64 Mono Development Player内に旧実装と新実装を含めて比較した。
Unity 6000.6.7f1、Intel Core Ultra 9 285K、Windows 11 10.0.26300、Direct3D11 / RTX 5080。
FMATH_VALIDATE off、Profiler未接続、Deep Profiling off。数値処理を分離するため一時プロジェクトはBuilt-in pipelineを使用し、製品のURP描画処理は測定対象にしていない。

各処理は固定seed 12345の1024入力を循環し、3万回×3回ずつwarm-upした後、30万回×6組を計測。
旧→新／新→旧を交互にし、すべての反復を集計した。ns/opは各区間のStopwatch経過時間を反復数で割った値で、スレッドの実占有CPU時間やゲーム全体のフレーム時間ではない。
入力参照・関数呼び出し・checksum加算を含み、初期化、描画、ログ、ファイル出力、フレーム間の待機は含まない。
固定回数のthroughput比較なのでFPSは制限しない。計測区間外で明示的なオフスクリーン描画を行い、非黒画像の取得も確認した。

| 処理 | 旧: 平均ns/op（最小–最大） | 新: 平均ns/op（最小–最大） | 平均処理時間の短縮 |
|---|---:|---:|---:|
| ffloat.Sqrt | 65.10（63.18–67.08） | 30.47（29.19–32.33） | 53.2% |
| fVector3.TryNormalize（極小） | 151.33（146.03–154.17） | 67.95（66.83–68.99） | 55.1% |
| fVector3.TryNormalize（各成分±2048 m） | 151.19（149.57–154.50） | 67.17（65.55–68.08） | 55.6% |
| fVector3.TryNormalize（int32全域） | 151.11（149.51–154.43） | 67.48（66.20–68.68） | 55.3% |

極小ベクトルはraw成分を22bit右シフトした入力、ゲーム範囲は4bit右シフトした入力、全域は元のint32入力。
Sqrtは従来のmicrobenchmarkと同様に、10bit右シフトした符号付きrawスカラーの絶対値を使った。
全48区間でmanaged allocationは0 byte。各組の旧・新のchecksumが一致し、区間ID・Playerフレーム番号・開始終了timestampに欠落・重複・重なりがないことを確認した。
生データ: [KernelBenchmark-2026-11-21.csv](KernelBenchmark-2026-11-21.csv)。各反復の値とフレーム番号を保持している。

整数平方根はdigit-by-digit法を整数Newton法へ変更した。normalizeはQ62の共通逆数を一度求め、各成分の推定商を剰余で補正してからties-to-evenへ丸める。
逆数は近似のまま返さず、従来と同じ商・剰余へ戻すため、raw結果と保存形式は変わらない。
Unity PlayModeの関連97テストを通過。10万組の新旧sqrt・3/4成分normalize比較、整数上限を含む追加1000組、既存corpus hash・10000tick simulation hashも一致した。

旧実装の基準コミットは `ad96e35477aeba51f356df4e41ed4d8cd78bc0a9`。
候補fWideMath.csのSHA-256: `2701c267b95599bb9b1b12e19305d86d86a852cf26a1fbe1069cd6201b8d8734`。
最初の非表示キャプチャは黒画像だったため暫定記録として別保存し、上表には描画確認後の追加1回の実行のみを使用した。
同一プロセス内の6組の比較であり、別プロセス反復・IL2CPP・ARM・モバイル・Release Playerの性能は未検証。以前のEditor値やCoreCLR値との直接比較には使わない。

## 以前の計測記録

計測環境: Windows 11 x64（Unityプロジェクトと同じマシン）、2026-09-27。

以下は修正前の履歴値であり、現在版の性能保証ではない。2026-11-20に乗算・除算の
ベンチマークを、全反復の結果をlongのchecksumへ加算する方式へ修正した。
この2項目の旧値は比較対象から除外した。上記の最新Player記録で再計測済みだが、以下の旧Editor / CoreCLR条件では再計測していない。新しい計測値にはchecksum加算の負荷も含まれる。
また `WithinAngle` は丸め誤差を避けるatan2ベースの判定へ変更したため、game-likeの旧値も現在版とは分けて扱う。
2026-11-20時点では4096反復の動作確認のみだった。現在版の全項目は上記の最新Player記録を参照。

## Unity 6000.6.0f1 Editor（Mono、Code Optimization = Debug、FMATH_VALIDATE off）

| micro | ns/op | allocated bytes |
|---|---:|---:|
| ffloat add | 11.5 | 0 |
| ffloat mul（checksum修正後） | 未再計測 | 未再計測 |
| ffloat div（checksum修正後） | 未再計測 | 未再計測 |
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
| ffloat mul（checksum修正後） | 未再計測 | 未再計測 |
| ffloat div（checksum修正後） | 未再計測 | 未再計測 |
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
