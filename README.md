# AudioProcessor

## 概要
AudioProcessor は、オーディオ解析・変換ツール群を提供する .NET 10 ソリューションです。  
現時点では次の実行可能な機能を提供しています。

- 無音区間ベース分割: `AudioSplitter.Cli`
- 窓ピーク dB 解析 + SQLite 保存: `SoundAnalyzer.Cli --mode peak-analysis`
- 窓SFFT解析（チャネル別band dB）+ SQLite 保存: `SoundAnalyzer.Cli --mode sfft-analysis`

## プロジェクト構成

| パス | 種別 | 役割 |
|---|---|---|
| `AudioProcessor` | Library | 外部オーディオ処理エンジン連携、プローブ、PCM ストリーム読取、セグメント出力などの共通基盤 |
| `AudioSplitter.Core` | Library | 無音分割ドメイン、境界計算、分割ユースケース |
| `AudioSplitter.Cli` | CLI | 無音分割のコマンドライン実行層 |
| `PeakAnalyzer.Core` | Library | hop/window ベースのピーク dB 窓解析コア |
| `SFFTAnalyzer.Core` | Library | hop/window ベースの短時間FFT band解析コア |
| `SoundAnalyzer.Cli` | CLI | ディレクトリ一括解析と SQLite 永続化 |
| `AudioProcessor.Tests` | Test | `AudioProcessor` の単体テスト |
| `AudioSplitter.Core.Tests` | Test | `AudioSplitter.Core` の単体テスト |
| `AudioSplitter.Cli.Tests` | Test | `AudioSplitter.Cli` の単体テスト |
| `PeakAnalyzer.Core.Tests` | Test | `PeakAnalyzer.Core` の単体テスト |
| `SFFTAnalyzer.Core.Tests` | Test | `SFFTAnalyzer.Core` の単体テスト |
| `SoundAnalyzer.Cli.Tests` | Test | `SoundAnalyzer.Cli` の単体テスト |

## 依存方向

- `AudioSplitter.Cli -> AudioSplitter.Core -> AudioProcessor`
- `SoundAnalyzer.Cli -> PeakAnalyzer.Core -> AudioProcessor`
- `SoundAnalyzer.Cli -> SFFTAnalyzer.Core -> AudioProcessor`

## 前提環境

- `.NET SDK 10.x`
- 音声処理に必要な外部ツールが利用可能であること（現行実装では `ffmpeg` / `ffprobe`。CLI オプション `--ffmpeg-path` でパス指定可能）
- Windows / Linux / macOS で動作可能（実行確認は環境依存）

## 最小コマンド

```powershell
dotnet build AudioProcessor.slnx -warnaserror
dotnet test AudioProcessor.slnx
```

### AudioSplitter.Cli

```powershell
dotnet run --project AudioSplitter.Cli -- \
  --input-dir /path/to/inputdir \
  --recursive \
  --output-dir /path/to/out \
  --level -48.0 \
  --duration 2000ms \
  --after-offset 500ms \
  --resume-offset -200ms
```

### SoundAnalyzer.Cli (peak-analysis)

```powershell
dotnet run --project SoundAnalyzer.Cli -- \
  --window-size 50ms \
  --hop 10ms \
  --input-dir /path/to/dir \
  --db-file /path/to/file.db \
  --mode peak-analysis \
  --stems Piano,Drums,Vocals \
  --table-name-override T_PEAK \
  --upsert
```

### SoundAnalyzer.Cli (sfft-analysis)

```powershell
dotnet run --project SoundAnalyzer.Cli -- \
  --window-size 50ms \
  --hop 10ms \
  --input-dir /path/to/dir \
  --db-file /path/to/file.db \
  --mode sfft-analysis \
  --bin-count 12 \
  --table-name-override T_SFFT \
  --upsert \
  --recursive
```

## ライセンス

本リポジトリのソースコードは `Kabayaki-Kappikapi License v2.0` を適用しています。

- 正文（日本語）: [`LICENSE`](LICENSE)
- 参考英訳: [`LICENSE_en.md`](LICENSE_en.md)

商業利用時には、ライセンス本文に定義された追加条件（公開要件・表示要件・駄菓子投稿要件など）が適用されます。詳細は必ずライセンス本文を確認してください。

## 第三者ライブラリのライセンス

本リポジトリは複数の第三者ライブラリを使用しており、それらには各ライブラリ固有のライセンスが適用されます。  
代表例として MIT、Apache-2.0、Source-Available ライセンス系が含まれます。配布・利用時は各依存パッケージのライセンス条項も確認してください。

- 一覧: [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md)

## 公開運用ドキュメント

- 参加ガイド: [`CONTRIBUTING.md`](CONTRIBUTING.md)
- 行動規範: [`CODE_OF_CONDUCT.md`](CODE_OF_CONDUCT.md)
- セキュリティ: [`SECURITY.md`](SECURITY.md)
- サポート窓口: [`SUPPORT.md`](SUPPORT.md)
- 第三者依存ライセンス: [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md)

## 本体プロジェクトREADME

- [`AudioProcessor/README.md`](AudioProcessor/README.md)
- [`AudioSplitter.Core/README.md`](AudioSplitter.Core/README.md)
- [`AudioSplitter.Cli/README.md`](AudioSplitter.Cli/README.md)
- [`PeakAnalyzer.Core/README.md`](PeakAnalyzer.Core/README.md)
- [`SFFTAnalyzer.Core/README.md`](SFFTAnalyzer.Core/README.md)
- [`SoundAnalyzer.Cli/README.md`](SoundAnalyzer.Cli/README.md)
