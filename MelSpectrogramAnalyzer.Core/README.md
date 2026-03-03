# MelSpectrogramAnalyzer.Core

## 役割
`MelSpectrogramAnalyzer.Core` は単一音源を hop/window 単位で解析し、  
各アンカー時刻に対するチャネル別 Mel Spectrogram（dB）を出力するコアライブラリです。

## 主要契約

- `MelSpectrogramAnalysisUseCase`
- `MelSpectrogramAnalysisRequest`
- `MelSpectrogramAnchorUnit`
- `MelSpectrogramScaleKind`
- `MelSpectrogramAnalysisPoint`
- `MelSpectrogramAnalysisSummary`
- `IMelSpectrogramAnalysisPointWriter`
- `MelSpectrogramAnalysisException`

## 入力モデル（sample基準）

`MelSpectrogramAnalysisRequest` は次を受け取ります。

- `windowSamples`
- `hopSamples`
- `analysisSampleRate`
- `anchorUnit` (`Millisecond` / `Sample`)
- `windowPersistedValue`
- `melBinCount`
- `melFminHz`
- `melFmaxHz`
- `melScaleKind` (`Slaney` / `Htk`)
- `melPower` (`1` / `2`)
- `minLimitDb`

`window/hop` の ms 指定は CLI 側で sample へ解決され、本コアには sample 値として渡されます。

## 解析ルール

- 解析アンカー進行は `frameIndex` と `hopSamples` で管理します（最初のアンカーは `hopSamples`）。
- 窓区間は `[anchor-window, anchor)`。
- 先頭不足区間は無音（0）として扱います。
- `anchorUnit` が `Millisecond` の場合、出力アンカーは `floor(sample * 1000 / sampleRate)` です。
- `anchorUnit` が `Sample` の場合、出力アンカーは sample 値をそのまま出力します。
- チャネルは分離して解析します（`ch=0..n-1`）。
- FFT 入力には Hann 窓を適用します。
- Mel filter bank は `slaney` / `htk` を切替可能です。
- `melPower=1` は magnitude 基準、`melPower=2` は power 基準です。
- dB 変換は `melPower=1 => 20*log10`, `melPower=2 => 10*log10` を適用します。
- `min-limit-db` で下限クランプします。

## バリデーション

- `windowSamples > 0`, `hopSamples > 0`, `analysisSampleRate > 0`, `melBinCount > 0`。
- `melFminHz >= 0`, `melFmaxHz > melFminHz`, `melFmaxHz <= Nyquist`。
- `melPower` は `1` または `2`。
- `melBinCount <= nextPow2(windowSamples)/2 + 1` を満たさない場合は `InvalidMelBinCount`。
- `minLimitDb` は `NaN`/`Infinity` を拒否します。

## 責務分離

`MelSpectrogramAnalysisUseCase` は解析実行のみを担当し、保存先には依存しません。  
永続化は `IMelSpectrogramAnalysisPointWriter` 実装（CLI 側の SQLite/PostgreSQL ストア）へ委譲します。
