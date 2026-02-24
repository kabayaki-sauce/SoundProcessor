# STFTAnalyzer.Core

## 役割
`STFTAnalyzer.Core` は単一音源を hop/window 単位で短時間 FFT 解析し、  
各アンカー時刻に対するチャネル別スペクトルバンド（dB）を出力するコアライブラリです。

## 主要契約

- `StftAnalysisUseCase`
- `StftAnalysisRequest`
- `StftAnchorUnit`
- `StftAnalysisPoint`
- `StftAnalysisSummary`
- `IStftAnalysisPointWriter`
- `StftAnalysisException`

## 入力モデル（sample基準）

`StftAnalysisRequest` は実行時に次を受け取ります。

- `windowSamples`
- `hopSamples`
- `analysisSampleRate`
- `anchorUnit` (`Millisecond` / `Sample`)
- `windowPersistedValue`
- `binCount`
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
- 正周波数 `0..Nyquist` を `binCount` 等分し、帯域平均 magnitude を dB 化します。
- `min-limit-db` で下限クランプします。

## バリデーション

- `windowSamples > 0`, `hopSamples > 0`, `analysisSampleRate > 0`, `binCount > 0`。
- `binCount <= nextPow2(windowSamples)/2 + 1` を満たさない場合は `InvalidBinCount`。
- `minLimitDb` は `NaN`/`Infinity` を拒否します。

## 責務分離

`StftAnalysisUseCase` は解析実行のみを担当し、保存先には依存しません。  
永続化は `IStftAnalysisPointWriter` 実装（CLI 側の SQLite ストアなど）へ委譲します。
