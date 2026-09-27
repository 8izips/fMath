# fMath v2 受け入れチェックリスト（実装状況）

凡例: [x] 実装・検証済み / [-] 対象外（理由記載） / [ ] 未検証（理由記載）

検証環境: dotnet 10（CoreCLR x64、`DotNet~/fMath.Tests`、FMATH_VALIDATE on/off両方）と
Unity 6000.6.0f1 Editor Mono（package testables、PlayMode 133テスト）。

## A. 基本仕様
- [x] ffloat 4byte / Q16.16
- [x] funit 4byte / Q1.30
- [x] fAngle 4byte / uint32 turn（符号付き差分 fAngleDelta も4byte）
- [x] fVector2 8byte
- [x] fVector3 12byte
- [x] fQuaternion 16byte
  - `fDeterminismTests.StructSizes`（Marshal.SizeOf と unsafe sizeof の両方）

## B. Core分離
- [x] CoreにUnityEngineなし（`Runtime/Core/fMath.asmdef` noEngineReferences、netstandard2.1ビルドガード）
- [x] Native Plugin依存なし（DllImport・FMATH_ENABLE_NATIVE_PLUGIN削除）
- [x] Runtime LUT生成なし（`Tools/GenerateTrigLut.cs` がdecimal級数でオフライン生成、`verify-lut` で一致確認）
- [x] float混合演算がSimulation API外（float/double operator・コンストラクタ削除。float→fixedは `fMath.Unity` のみ）

## C. Scalar
- [x] int64乗算
- [x] int64除算
- [x] ties-to-even（乗算、除算、Wide→32bit、Sqrt最終丸め、Round、Unit変換）
- [x] deterministic saturation
- [x] Validation overflow detection（`FMATH_VALIDATE` で `fMathValidation` がカウント・通知）
- [x] Sqrt単調（全非負範囲をstride走査、monotonicity failures 0）
- [x] FromFraction
- [x] zero divide契約明記（正→MaxValue、負→MinValue、0/0→0、`TryDivide` はfalse）

## D. Angle / Trig
- [x] Angle32
- [x] Quarter/Half turn
- [x] Q30 Sin/Cos（最大誤差 20.6 raw Q30 ≈ 1.9e-8）
- [x] LUT interpolation（4096区間 + 終端、64bit補間）
- [x] wrap test
- [x] quadrant boundary test（境界 ±2 raw）
- [x] tiny angle test（1 raw angle、2048 m先の1 cm）

## E. Vector
- [x] Raw exact equality
- [x] DistanceSquaredWide
- [x] LengthSquaredWide
- [x] DotWide
- [x] CrossWide
- [x] UnitVector（fUnitVector2 / fUnitVector3）
- [x] TryNormalize（成分誤差 ≤ 1 raw Q30）
- [x] 1raw test
- [x] 1cm test
- [x] 500m test
- [x] 2048m test

## F. Quaternion
- [x] Q30 components
- [x] identity
- [x] normalize
- [x] multiply wide
- [x] rotate vector（Q30回転行列、2048 mで誤差 2e-5 m、長さ変化 ≤ 3 raw）
- [x] AngleAxis修正
- [x] FromTo 180°修正（fAngle.HalfTurn、ほぼ反対方向でも誤差 < 4e-7 rad）
- [x] LookRotation orthonormal
- [x] Lerp normalize
- [x] Slerp negative-dot全成分反転
- [x] inverse（unit: conjugate、general: TryInverse）
- [x] qと-q同一回転（IsSameRotation / Angle）

## G. Regression（v1の不具合がv2で解消していることを確認）
- [x] Tan(Pi/4)
- [x] Sqrt monotonic legacy issue（4097/4098）
- [x] Acos(1)
- [x] Vector.Angle self
- [x] AngleAxis non-unit
- [x] vector length changed by rotation
- [x] FromTo degree/radian
- [x] Slerp conjugate
- [x] LookRotation
- [x] ToEuler unit mismatch
  - v1の不具合は最初のコミット（`test: capture legacy fMath numeric regressions`）で固定し、
    v2では `Tests/Runtime/*RegressionTests.cs` が修正後の挙動を検証する。

## H. Determinism
- [x] Editor Mono raw hash（golden一致）
- [ ] Windows IL2CPP raw hash（未検証: このマシンにIL2CPPモジュールなし）
- [-] Android ARM64 IL2CPP raw hash（今回は実機テスト対象外）
- [-] iOS ARM64 IL2CPP raw hash（今回は実機テスト対象外）
- [x] 全Hash一致（CoreCLR x64 と Unity Mono で combined `08BDC5891B892EA1`）
- [x] 10000tick simulation hash一致（final `13D4456E86187E21`、chain `AB4052045BE65F56`）
- [x] rollback restore/resim一致
  - 実機検証は `Samples~/DeterminismProbe` をビルドし、表示されたhashをgolden値と比較する。
    PlayMode testsはplayerでも実行できる（`Tests/Runtime` は全プラットフォーム対象）。

## I. Performance
- [x] hot path GC 0（テストで3 pass計測、CoreCLR / Mono とも0 byte）
- [-] Android benchmark（今回は実機テスト対象外）
- [x] 8 agent normal tick
- [x] 1+4 resim
- [x] 1+8 resim
- [x] snapshot copy
  - 結果は `Documentation~/Benchmark.md`。

## J. Migration
- [x] Numeric Format Version（`fMathFormatVersion`、`fDeterministicSessionHeader`）
- [-] old Q12 -> Q16 converter（v1データ未使用のため不要）
- [-] Quaternion converter（同上）
- [-] Asset migration tool（同上）
- [x] LUT再生成（v1 LUTは削除、Angle32 quarter-wave Q30 LUTを新規生成）
- [x] obsolete API整理（v1 APIは残さず削除）
- [-] replay compatibility（v1 replay/stateが存在しないため不要）
- [x] network handshake versioning（ProtocolVersion / fMathFormatVersion / GameSimulationVersion / ContentHash）

## K. 最終判定
- [x] 半径500m通常プレイで精度問題なし
- [x] ±2048m Validation Domainで破綻なし
- [x] 小角度判定で必要精度維持
- [x] Snapshotサイズが設計範囲内（8 agents 480 B）
- [x] Burst不要
- [x] Native Plugin不要
- [ ] モバイルRollback用途として採用可（Android/iOS実機のhash一致と性能確認後に判定）
