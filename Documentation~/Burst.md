# Burst integration

Unity 6000.6 / built-in Burst 2.0.0を最低構成とする試験実装。
従来のUnity 2021.3対応から導入条件を変更した。raw型・保存形式・LUT・整数演算の丸め規則は変更していない。
Coreのasmdefは追加せず、Unity.Burstを参照し、unsafeを有効化した。
Burst 2の属性はUnityEngine.BurstModule / ScriptingModuleにあるため`noEngineReferences`はfalse。
通常の数学処理にGameObject等のEngine APIは使わない。

## 呼び出し

```csharp
fMathBurst.Sqrt(in value, out ffloat root);
bool success = fMathBurst.TryNormalize(in vector, out fUnitVector3 direction);
fMathBurst.Slerp(in a, in b, in t, out fQuaternion rotation);
```

既存の`ffloat.Sqrt`、`fVector3.TryNormalize`、`fQuaternion.Slerp`をそのまま共用する。
既存APIを通常C#から呼ぶだけではBurst処理にならない。大きなBurst Job内では既存APIを直接呼び、
要素ごとのmanaged/native境界を避ける。Burstを無効にした場合や利用できない場合、入口はmanagedへ戻る。
`CompileSynchronously`は非対応環境でネイティブ実行を強制する指定ではない。

## 同期バッチ

`SqrtBatch(input, output, count)`、`NormalizeBatch(input, output, success, count)`、
`SlerpBatch(a, b, t, output, count)`はunsafeポインターを借用する。確保・スケジュール・所有権移転・保持は行わない。

- countは0以上。0ではnullを許可し、要素にアクセスしない。負数はArgumentOutOfRangeException。
- 正数では各バッファがcount要素以上あり、型の自然なアラインメントを満たす必要がある（数値型は4byte、successは1byte）。
- 全バッファは互いに重複不可。同じ開始位置のin-place処理も不可。
- managed配列は呼び出し終了までfixed等でpinする。呼び出し中の競合する読み書きは禁止。
- normalizeは全outputとsuccessを書き、successは失敗0、成功1。ゼロベクトルは失敗し、数値結果は既存APIと同じ。

不正ポインター・長さ不足・重複は呼び出し側の契約違反であり、入口でメモリー検査は行わない。
unsafeのコンパイラー許可はCoreアセンブリ全体に適用されるが、追加したunsafe実装は入口ファイルに限定した。

## 診断と検証

`FMATH_VALIDATE`の数値上の失敗分岐は維持する。`fMathValidation.Report`だけをBurstDiscardにしたため、
native実行ではmanagedカウンター更新・Handler呼び出し・Handlerが投げる例外を省略する。
`IsEnabled`はスクリプト定義の有無であり、nativeで報告できることを意味しない。

既存のfMath.Testsにネイティブ入口の検証を追加した。各入口の内部markerがmanaged fallbackを検出すると失敗する。
単独probeの成功を他の入口のnative実行証拠として扱わない。
Unityを使わないオフライン診断ではUnity専用の入口・属性・テストをコンパイル対象から除外する（スタブは作らない）。
このオフライン構成の実行は今回検証していない。

実測条件・結果は[Benchmark.md](Benchmark.md)を参照。Windowsでの結果からAndroid/iOSの一致・速度は保証しない。
