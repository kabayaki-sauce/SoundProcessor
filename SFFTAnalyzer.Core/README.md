# SFFTAnalyzer.Core

## 役割
`SFFTAnalyzer.Core` は、単一音源を hop/window 単位で短時間FFT解析し、
各時間窓に対するチャネル別スペクトルバンド（dB）を出力するコアライブラリです。

## 主要契約

- `SfftAnalysisUseCase`
- `SfftAnalysisRequest`
- `SfftAnalysisPoint`
- `SfftAnalysisSummary`
- `ISfftAnalysisPointWriter`
- `SfftAnalysisException`

## 解析ルール

- 解析アンカーは `hop` ごと
- 最初のアンカーは `hop ms`（`0ms` は解析しない）
- 窓区間は `[t-window, t)`
- 先頭不足区間は無音（0）として扱う
- 末尾は `floor(total_ms / hop_ms) * hop_ms` までを対象とし端数は破棄
- チャネルは分離して解析する（`ch=0..n-1`）
- FFT入力には Hann 窓を適用する
- 正周波数 `0..Nyquist` を `binCount` 等分し、帯域平均 magnitude を dB 化する
- `min-limit-db` で下限クランプする

## 責務分離

`SfftAnalysisUseCase` は解析実行を担当し、解析結果の永続化・出力は
`ISfftAnalysisPointWriter` 実装に委譲します。
