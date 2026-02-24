# SoundAnalyzer.Cli

## 役割
`SoundAnalyzer.Cli` は、ディレクトリ配下の音声ファイルを一括解析し、結果を SQLite へ保存する CLI です。  
`peak-analysis` は `PeakAnalyzer.Core`、`sfft-analysis` は `SFFTAnalyzer.Core` を利用します。

## 実行形式

```powershell
SoundAnalyzer.Cli.exe --window-size <time> --hop <time> --input-dir <path> --db-file <path> --mode <peak-analysis|sfft-analysis> [options]
```

## オプション

| オプション | 必須 | 説明 |
|---|---|---|
| `--window-size <time>` | 必須 | 窓サイズ（`ms/s/m`、整数ms化可能であること） |
| `--hop <time>` | 必須 | hop（`ms/s/m`、整数ms化可能であること） |
| `--input-dir <path>` | 必須 | 入力ルート |
| `--db-file <path>` | 必須 | SQLite ファイル |
| `--mode <value>` | 必須 | `peak-analysis` または `sfft-analysis` |
| `--table-name-override <name>` | 任意 | テーブル名上書き |
| `--upsert` | 任意 | 競合時更新 |
| `--skip-duplicate` | 任意 | 競合時スキップ |
| `--min-limit-db <dB>` | 任意 | 下限クランプ（既定 `-120.0`） |
| `--ffmpeg-path <path>` | 任意 | 音声処理ツールのパス指定（現行実装では ffmpeg/ffprobe） |
| `--help`, `-h` | 任意 | ヘルプ表示 |

`--upsert` と `--skip-duplicate` は同時指定不可です。

### `peak-analysis` 専用

| オプション | 説明 |
|---|---|
| `--stems <csv>` | 解析対象 stem 名（大小無視）。未指定時は全stem解析 |

既定テーブル名: `T_PeakAnalysis`

### `sfft-analysis` 専用

| オプション | 説明 |
|---|---|
| `--bin-count <n>` | 出力band数（必須、`n >= 1`） |
| `--delete-current` | 既存テーブルがあれば削除して再作成 |
| `--recursive` | `input-dir` 配下を再帰走査 |

既定テーブル名: `T_SFFTAnalysis`

## 解析対象ファイルの解決

### `peak-analysis`

- 走査対象は `input-dir` 直下の1階層サブディレクトリ
- stem 一致は大文字小文字無視
- 同一 stem に複数拡張子がある場合の優先順は `wav -> flac -> m4a/caf -> others`

### `sfft-analysis`

- `--recursive` 未指定: `input-dir` 直下ファイルのみ
- `--recursive` 指定: `input-dir` 配下を再帰走査
- 対象拡張子は `wav/flac/m4a/caf`
- 同一ディレクトリで同名別拡張子がある場合は `wav -> flac -> m4a/caf -> others` 優先で1件採用
- 解析対象集合で `name`（拡張子なし）が大小無視で重複する場合は実行前エラー

## SQLite スキーマ

### `peak-analysis`

既定テーブル: `T_PeakAnalysis`

- 列: `idx`, `name`, `stem`, `window`, `ms`, `db`, `create_at`, `modified_at`
- 一意制約: `(name, stem, window, ms)`
- インデックス: `(name)`, `(name, stem)`, `(name, ms)`, `(name, stem, ms)`

### `sfft-analysis`

既定テーブル: `T_SFFTAnalysis`

- 列: `idx`, `name`, `ch`, `window`, `ms`, `bin001..binNNN`, `create_at`, `modified_at`
- 一意制約: `(name, ch, window, ms)`
- インデックス: `(name)`, `(name, ch)`, `(name, ms)`, `(name, ch, ms)`

`--delete-current` 未指定時は既存テーブルの `binNNN` 列数と `--bin-count` が一致する場合のみ続行します。

## 実行例

### peak-analysis

```powershell
SoundAnalyzer.Cli.exe --window-size 50ms --hop 10ms --input-dir /path/to/dir --db-file /path/to/file.db --mode peak-analysis --stems Piano,Drums,Vocals --table-name-override T_PEAK --upsert
```

### sfft-analysis

```powershell
SoundAnalyzer.Cli.exe --window-size 50ms --hop 10ms --input-dir /path/to/dir --db-file /path/to/file.db --mode sfft-analysis --bin-count 12 --table-name-override T_SFFT --upsert --recursive --delete-current
```
