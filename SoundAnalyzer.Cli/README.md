# SoundAnalyzer.Cli

## 役割
`SoundAnalyzer.Cli` は、ディレクトリ配下の STEM 音源を一括解析し、ピーク窓結果を SQLite へ保存する CLI です。  
解析ロジックは `PeakAnalyzer.Core`、音声I/Oは `AudioProcessor` を利用します。

## 実行形式

```powershell
SoundAnalyzer.Cli.exe --window-size <time> --hop <time> --input-dir <path> --db-file <path> --mode peak-analysis [options]
```

## オプション

| オプション | 必須 | 説明 |
|---|---|---|
| `--window-size <time>` | 必須 | 窓サイズ（`ms/s/m`、整数ms化可能であること） |
| `--hop <time>` | 必須 | hop（`ms/s/m`、整数ms化可能であること） |
| `--input-dir <path>` | 必須 | 入力ルート（1階層サブディレクトリのみ走査） |
| `--db-file <path>` | 必須 | SQLite ファイル |
| `--mode peak-analysis` | 必須 | モード（現状 `peak-analysis` 固定） |
| `--stems <csv>` | 任意 | 解析対象 stem 名（大小無視） |
| `--table-name-override <name>` | 任意 | 既定 `T_PeakAnalysis` |
| `--upsert` | 任意 | 競合時更新 |
| `--skip-duplicate` | 任意 | 競合時スキップ |
| `--min-limit-db <dB>` | 任意 | 下限クランプ（既定 `-120.0`） |
| `--ffmpeg-path <path>` | 任意 | ffmpeg/ffprobe パス指定 |
| `--help`, `-h` | 任意 | ヘルプ表示 |

`--upsert` と `--skip-duplicate` は同時指定不可です。

## 対象ファイル解決

- 走査対象は `input-dir` 直下の1階層サブディレクトリ
- stem 一致は大文字小文字無視
- 同一 stem に複数拡張子がある場合の優先順は `wav -> flac -> m4a/caf -> others`

## SQLite スキーマ

既定テーブル: `T_PeakAnalysis`

- 列: `idx`, `name`, `stem`, `window`, `ms`, `db`, `create_at`, `modified_at`
- 一意制約: `(name, stem, window, ms)`
- インデックス: `(name)`, `(name, stem)`, `(name, ms)`, `(name, stem, ms)`

競合時の挙動:

- `--upsert`: `db`, `modified_at` を更新（`create_at` 維持）
- `--skip-duplicate`: 何もしない
- 未指定: エラー

## 実行例

```powershell
SoundAnalyzer.Cli.exe --window-size 50ms --hop 10ms --input-dir /path/to/dir --db-file /path/to/file.db --stems Piano,Drums,Vocals --mode peak-analysis --table-name-override T_PEAK --upsert
```
