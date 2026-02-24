# PeakAnalyzer.Core

## 役割
`PeakAnalyzer.Core` は、単一音源に対する窓ピーク dB 解析ロジックを提供します。  
書き込み先（DB、CSV、標準出力など）は `IPeakAnalysisPointWriter` で外部化されています。

## 主要契約

- `PeakAnalysisUseCase`
- `PeakAnalysisRequest`
- `PeakAnalysisPoint`
- `PeakAnalysisSummary`
- `IPeakAnalysisPointWriter`
- `PeakAnalysisException`

## 解析ルール

- 解析アンカーは `hop` ごと
- 最初のアンカーは `hop ms`（`0ms` は解析しない）
- 窓区間は `[t-window, t)`
- 先頭不足区間は無音（0）として扱う
- 末尾は `floor(total_ms / hop_ms) * hop_ms` までを対象とし端数は破棄
- マルチチャネル入力はチャネル横断ピーク
- `db = 20 * log10(peak)`、`min-limit-db` で下限クランプ

## 責務分離

`PeakAnalysisUseCase` は解析実行を担当し、解析結果の永続化・出力は `IPeakAnalysisPointWriter` 実装に委譲します。
