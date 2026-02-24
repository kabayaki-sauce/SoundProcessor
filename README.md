# AudioProcessor

## 概要
AudioProcessor は、オーディオ解析・変換ツール群を提供する .NET 10 ソリューションです。  
現時点では次の実行可能な機能を提供しています。

- 無音区間ベース分割: `AudioSplitter.Cli`
- 窓ピーク dB 解析 + SQLite/PostgreSQL 保存: `SoundAnalyzer.Cli --mode peak-analysis`
- 窓STFT解析（チャネル別band dB）+ SQLite/PostgreSQL 保存: `SoundAnalyzer.Cli --mode stft-analysis`
  - `--window-size` / `--hop` は `ms/s/m/sample/samples` を受理
  - `sample(s)` を1つでも使う場合は `--target-sampling <n>hz` が必須
  - STFT bin は `bin_no` / `db` の縦持ち行として保存（列数上限依存を回避）
  - `--stft-proc-threads` / `--stft-file-threads` / `--insert-queue-size` で並列・キュー制御
  - 大量投入向けに `--sqlite-batch-row-count`（既定 `512`）で複数行 INSERT バッチサイズを調整可能
  - PostgreSQLモードでは `--postgres-batch-row-count`（既定 `1`）で複数行 INSERT バッチサイズを調整可能（上限は自動クランプ）
  - `--sqlite-fast-mode` 指定時のみ SQLite 書込PRAGMAを速度優先へ切替（耐障害性トレードオフあり）
- `--show-progress` は interactive な `pwsh/cmd` で、Songs/Threads/Queue の詳細進捗を `stderr` に表示（Thread行は単一ゲージで Insert=緑 / Analyze=白 / 未処理=斑点）

## プロジェクト構成

| パス | 種別 | 役割 |
|---|---|---|
| `AudioProcessor` | Library | 外部オーディオ処理エンジン連携、プローブ、PCM ストリーム読取、セグメント出力などの共通基盤 |
| `Cli.Shared` | Library | CLI 表示共通化（2段プログレス表示、テキストブロック再描画、TTY判定、stderr描画） |
| `AudioSplitter.Core` | Library | 無音分割ドメイン、境界計算、分割ユースケース |
| `AudioSplitter.Cli` | CLI | 無音分割のコマンドライン実行層 |
| `PeakAnalyzer.Core` | Library | hop/window ベースのピーク dB 窓解析コア |
| `STFTAnalyzer.Core` | Library | hop/window ベースの短時間FFT band解析コア |
| `SoundAnalyzer.Cli` | CLI | ディレクトリ一括解析と SQLite/PostgreSQL 永続化 |
| `AudioProcessor.Tests` | Test | `AudioProcessor` の単体テスト |
| `Cli.Shared.Tests` | Test | `Cli.Shared` の単体テスト |
| `AudioSplitter.Core.Tests` | Test | `AudioSplitter.Core` の単体テスト |
| `AudioSplitter.Cli.Tests` | Test | `AudioSplitter.Cli` の単体テスト |
| `PeakAnalyzer.Core.Tests` | Test | `PeakAnalyzer.Core` の単体テスト |
| `STFTAnalyzer.Core.Tests` | Test | `STFTAnalyzer.Core` の単体テスト |
| `SoundAnalyzer.Cli.Tests` | Test | `SoundAnalyzer.Cli` の単体テスト |

## 依存方向

- `AudioSplitter.Cli -> AudioSplitter.Core -> AudioProcessor`
- `SoundAnalyzer.Cli -> PeakAnalyzer.Core -> AudioProcessor`
- `SoundAnalyzer.Cli -> STFTAnalyzer.Core -> AudioProcessor`

`SoundAnalyzer.Cli` の SQLite 保存は、初期化時に `journal_mode=WAL` を試行します。  
WAL 非対応環境では既存ジャーナルモードへ自動フォールバックします。

`--sqlite-fast-mode` 指定時は `synchronous=OFF` など速度優先PRAGMAを追加で適用します。  
異常終了時の破損/欠損リスクが上がるため、投入ジョブ専用DB・バックアップ運用を推奨します。

`--postgres` 指定時は PostgreSQL 保存モードへ切り替わります。  
`--postgres-host/--postgres-port/--postgres-db/--postgres-user` が必須で、`--db-file` は指定できません。

## 前提環境

- `.NET SDK 10.x`
- 音声処理に必要な外部ツールが利用可能であること（現行実装では `ffmpeg` / `ffprobe`。CLI オプション `--ffmpeg-path` でパス指定可能）
- Windows / Linux は必須サポート対象
- macOS は任意検証対象

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

### SoundAnalyzer.Cli (stft-analysis)

```powershell
dotnet run --project SoundAnalyzer.Cli -- \
  --window-size 50ms \
  --hop 10ms \
  --input-dir /path/to/dir \
  --db-file /path/to/file.db \
  --mode stft-analysis \
  --bin-count 12 \
  --table-name-override T_STFT \
  --upsert \
  --recursive
```

### SoundAnalyzer.Cli (stft-analysis, samples基準)

```powershell
dotnet run --project SoundAnalyzer.Cli -- \
  --window-size 2048samples \
  --hop 512samples \
  --target-sampling 44100hz \
  --input-dir /path/to/dir \
  --db-file /path/to/file.db \
  --mode stft-analysis \
  --bin-count 24 \
  --upsert
```

### SoundAnalyzer.Cli (Windows 実行例)

```powershell
SoundAnalyzer.Cli.exe --window-size 50ms --hop 10ms --input-dir C:\audio\split --db-file C:\data\analyze.db --mode stft-analysis --bin-count 12 --sqlite-batch-row-count 512 --sqlite-fast-mode
```

### SoundAnalyzer.Cli (Linux 実行例)

```bash
dotnet SoundAnalyzer.Cli.dll --window-size 50ms --hop 10ms --input-dir /data/audio/split --db-file /data/analyze.db --mode stft-analysis --bin-count 12 --sqlite-batch-row-count 512 --sqlite-fast-mode
```

### SoundAnalyzer.Cli (PostgreSQL 実行例)

```powershell
SoundAnalyzer.Cli.exe --window-size 50ms --hop 10ms --input-dir C:\audio\split --mode stft-analysis --bin-count 12 --postgres --postgres-host 127.0.0.1 --postgres-port 5432 --postgres-db audio --postgres-user analyzer --postgres-password secret --postgres-batch-row-count 512 --show-progress
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
- [`Cli.Shared/README.md`](Cli.Shared/README.md)
- [`AudioSplitter.Core/README.md`](AudioSplitter.Core/README.md)
- [`AudioSplitter.Cli/README.md`](AudioSplitter.Cli/README.md)
- [`PeakAnalyzer.Core/README.md`](PeakAnalyzer.Core/README.md)
- [`STFTAnalyzer.Core/README.md`](STFTAnalyzer.Core/README.md)
- [`SoundAnalyzer.Cli/README.md`](SoundAnalyzer.Cli/README.md)
