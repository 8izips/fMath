# fMath

Unity向けのRollback / Lockstep Netcode用、決定論的固定小数点数学ライブラリ（v2）。

```text
保存状態: 32bit
一般スカラー: ffloat  signed Q16.16
Unit / Normal / Quaternion成分: funit  signed Q1.30
角度: fAngle  uint32 turn（fAngleDelta: int32）
中間演算: int64 / uint64、round-to-nearest ties-to-even、最終値は決定論的に飽和
```

目的は、半径500m程度の3Dアクションゲームを最大8プレイヤーのRollback環境で、小さな状態サイズを保ったまま
Windows / Android ARM64 / iOS ARM64（Mono / IL2CPP）でbit一致に再実行できる数学基盤を提供すること。
Burst、Jobs、Native Pluginは不要。

## 導入

Unity Package Managerで `https://github.com/8izips/fMath.git` を追加する（`package.json`: `com.8izips.fmath`）。

| アセンブリ | 内容 |
|---|---|
| `fMath`（Runtime/Core） | 数値型と演算。`noEngineReferences`、UnityEngine非依存 |
| `fMath.Unity`（Runtime/Unity） | float / Vector3 / Quaternion との変換（Authoring・Presentation境界専用） |
| `fMath.Diagnostics`（Runtime/Diagnostics） | 決定性probe、8 agent simulation、benchmark |
| `fMath.Tools.Editor`（Tools） | LUT再生成、診断メニュー |

## 型

| 型 | サイズ | 用途 |
|---|---:|---|
| `ffloat` | 4 B | 位置・距離・速度・パラメータ。1.0 = 65536 raw、範囲 約±32768 |
| `funit` | 4 B | 方向成分・法線・Quaternion成分・sin/cos。1.0 = 2^30 raw |
| `fAngle` / `fAngleDelta` | 4 B | 角度（1周 = 2^32）/ 符号付き差分 |
| `fVector2` / `fVector3` | 8 / 12 B | ffloat成分のベクトル |
| `fUnitVector2` / `fUnitVector3` | 8 / 12 B | funit成分の単位ベクトル |
| `fQuaternion` | 16 B | funit成分の回転 |
| `fWideVector3` | 24 B | cross積などの64bit中間結果（状態には保存しない） |

## 使い方

```csharp
ffloat speed = ffloat.FromFraction(35, 10);            // 3.5
ffloat dt = ffloat.FromFraction(1, 60);
fVector3 pos = fVector3.FromInt(10, 0, -4);
fVector3 enemyPos = fVector3.FromInt(12, 0, -3);
fAngle yaw = fAngle.FromDegrees(90);

// 移動: 方向(Q30) × 速度 × dt
fUnitVector2 dir2 = fUnitVector2.FromAngle(yaw);
pos += new fVector3(dir2.x * speed, ffloat.Zero, dir2.y * speed) * dt;

// 範囲判定: Sqrtなし（64bit二乗距離の比較）
bool hit = fVector3.IsWithinDistance(pos, enemyPos, ffloat.FromInt(3));

// 角度判定: acosなし（dot >= cos(limit)）
fQuaternion q = fQuaternion.AngleAxis(yaw, fUnitVector3.up);
fUnitVector3 forward = q * fUnitVector3.forward;
if (hit && fVector3.TryNormalize(enemyPos - pos, out fUnitVector3 toEnemy))
{
    hit = fUnitVector3.WithinAngle(forward, toEnemy, fAngle.FromDegrees(45));

    // 回転: 敵を向く回転へ1/4ずつ近づける
    fQuaternion look = fQuaternion.LookRotation(toEnemy, fUnitVector3.up);
    q = fQuaternion.Slerp(q, look, ffloat.FromFraction(1, 4));
}
fVector3 rotated = q * fVector3.FromInt(1, 0, 0);
```

Presentation / Authoring境界（`using fMath.Unity;`）:

```csharp
fVector3 spawn = transform.position.ToFixed();       // authoring時のみ
transform.position = simPosition.ToVector3();        // 描画のみ。floatをsimulationへ戻さない
fAngle facing = fMathUnityConversions.DegreesToAngle(90f);
```

## 決まりごと

- `==` はraw完全一致。近似比較は `Approximately` / `IsWithinDistance` / `WithinAngle` / `fQuaternion.IsSameRotation`。
- 乗算・除算・dot・cross・長さ・正規化・Quaternion積和はすべて64bitで計算し、最終値だけ32bitへ丸める。
- 表現できない最終値は飽和する。0除算は 正→MaxValue、負→MinValue、0/0→0。分岐が必要なら `TryDivide` / `TrySqrt` / `TryNormalize` / `TryInverse`。
- 負のSqrt、ゼロベクトル正規化、不正Quaternion、overflowは Scripting Define Symbol `FMATH_VALIDATE` で検出できる（`fMathValidation.Handler` / `GetCount`）。defineなしではコードごと除去される。
- 角度はすべて `fAngle`。degree/radianの混在はない（`FromDegrees(int)` / `FromTurnsFraction` で生成）。
- Euler（`fQuaternion.Euler` / `ToEuler`）はUnityのZXY順で、Authoring・Debug用途。
- Session / Replay / Snapshotには `fMathFormatVersion.Current`（= 2）を含め、P2P handshakeは `fDeterministicSessionHeader` の4値が一致したときのみ開始する。

## 精度・性能・決定性

- 精度: `Documentation~/PrecisionReport.md`（Sqrt ±0.5 raw、Sin/Cos 1.9e-8、Atan2 6.4e-9 rad、Normalize < 1 raw Q30、2048 mでの回転誤差 2e-5 m、単調性違反 0）
- 性能: `Documentation~/Benchmark.md`（Unity Editor Monoで8 agent rollback 1+8が 0.17 ms/frame、hot path GC 0）
- 決定性: 固定入力corpus（`Tests/Determinism/fmath_v2_input_vectors.bin`）に全APIを適用したraw hashと、10000 tickの8 agent simulation hashをgolden値として `Tests/Runtime/fDeterminismTests.cs` で検証。
  CoreCLR x64とUnity Monoで一致。実機は `Samples~/DeterminismProbe` をビルドして比較する。
- 受け入れ状況: `Documentation~/AcceptanceChecklist.md`

## テスト・ツール

```bash
dotnet test DotNet~/fMath.Tests
```

```bash
dotnet test DotNet~/fMath.Tests -c Release -p:FMathValidate=false
```

```bash
dotnet run -c Release --project DotNet~/fMath.Tools -- verify-lut
```

`DotNet~/fMath.Tools` のコマンド: `generate-lut` / `verify-lut` / `write-corpus` / `hash` / `benchmark` / `precision-report`。
Unityでは package を `testables` に追加すると `Tests/Runtime`（PlayMode）と `Tests/Unity` が実行できる。
`DotNet~/fMath.Core.NetStandard` は Runtime/Core を .NET Standard 2.1 / C# 9 でビルドし、Unityで使えないAPIの混入を防ぐ。

## v1からの変更

v1（Q20.12、radian ffloat、ffloat成分Quaternion、Native Plugin）はv2で置き換えた。互換APIとv1データ変換は提供しない。
主な置き換え: `new ffloat(float)` → `FromFraction` / `fMath.Unity.ToFixed`、`CreateFromRawValue` → `FromRaw`、
`ffloat.Sin(radian)` → `fTrig.Sin(fAngle)`、`normalized` → `TryNormalize(out fUnitVector3)`、
`Angle` の戻り値 degree ffloat → `fAngle`、`fQuaternion(Quaternion)` / `ToUnityQuaternion` → `fMath.Unity` 拡張メソッド。

## License

MIT
