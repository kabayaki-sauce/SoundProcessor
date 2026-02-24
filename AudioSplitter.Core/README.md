# AudioSplitter.Core

## 役割
`AudioSplitter.Core` は、無音区間を境界とした分割ドメインとユースケースを提供します。  
CLI 入力の表現から切り離し、分割仕様の決定ロジックを集約しています。

## 主要ユースケース

- `SplitAudioUseCase`

`SplitAudioRequest` を受け取り、次の順で実行します。

1. 入力検証と出力先準備
2. `AudioProcessor` 経由でストリーム情報取得
3. 無音解析 (`ISilenceAnalyzer`)
4. 境界算出 (`SegmentPlanner`)
5. セグメント出力 (`IAudioSegmentExporter`)

`SplitAudioUseCase.ExecuteAsync` は任意の進捗コールバックを受け取り、
`Resolve/Probe/Analyze/Export` の phase 進捗を通知できます。

## 仕様ポイント

- 無音判定はフレーム単位の peak dBFS
- `duration` を満たす連続無音のみ分割候補
- `after-offset` / `resume-offset` は境界補正として適用
- offset 条件で重複が発生しても重複区間を許容
- 全体無音時は 0 セグメント

## 依存

`AudioSplitter.Core` は `AudioProcessor` へ依存し、CLI には依存しません。
