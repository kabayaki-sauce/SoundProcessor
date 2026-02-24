# AudioProcessor

## 役割
`AudioProcessor` は、CLI やドメイン層から再利用される共通オーディオ処理基盤です。  
FFmpeg/FFprobe の実行、入力ストリーム情報の取得、PCM フレームの逐次読取、セグメント出力を担当します。

## 主な公開契約

### ポート
- `IFfmpegLocator`
- `IAudioProbeService`
- `IAudioPcmFrameReader`
- `IAudioPcmFrameSink`
- `IAudioSegmentExporter`

### モデル
- `FfmpegToolPaths`
- `AudioStreamInfo`
- `AudioSegment`
- `OutputAudioFormat`
- `SegmentExportRequest`

### 例外
- `AudioProcessorException`
- `AudioProcessorErrorCode`

## 利用側への期待

- 呼び出し側は、業務ロジック（例: 無音判定やピーク窓判定）を `AudioProcessor` の外に保持する。
- `IAudioPcmFrameReader` はストリーム処理前提なので、全データのメモリ展開を前提にしない。
- `AudioProcessorException` の `ErrorCode` を用いて CLI 層でメッセージ変換する。

## DI登録

`AudioProcessor.Extensions.AudioProcessorServiceCollectionExtensions` により、主要ポート実装を DI へ登録できます。
