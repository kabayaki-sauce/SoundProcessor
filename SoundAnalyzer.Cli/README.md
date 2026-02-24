# SoundAnalyzer.Cli

## 役割
`SoundAnalyzer.Cli` はディレクトリ配下の音声ファイルを一括解析し、結果を SQLite へ保存する CLI です。  
`peak-analysis` は `PeakAnalyzer.Core`、`stft-analysis` は `STFTAnalyzer.Core` を利用します。

## 実行形式

```powershell
SoundAnalyzer.Cli.exe --window-size <len> --hop <len> --input-dir <path> --db-file <path> --mode <peak-analysis|stft-analysis> [options]
```

## オプション

| オプション | 必須 | 説明 |
|---|---|---|
| `--window-size <len>` | 必須 | 解析窓サイズ。`ms/s/m/sample/samples` を受理（整数値へ変換可能であること） |
| `--hop <len>` | 必須 | hop。`ms/s/m/sample/samples` を受理（整数値へ変換可能であること） |
| `--input-dir <path>` | 必須 | 入力ルート |
| `--db-file <path>` | 必須 | SQLite ファイル |
| `--mode <value>` | 必須 | `peak-analysis` または `stft-analysis` |
| `--target-sampling <n>hz` | 条件付き必須 | `stft-analysis` かつ `window/hop` のどちらかが `sample(s)` 指定の場合に必須 |
| `--table-name-override <name>` | 任意 | テーブル名上書き |
| `--upsert` | 任意 | 競合時更新 |
| `--skip-duplicate` | 任意 | 競合時スキップ |
| `--min-limit-db <dB>` | 任意 | 下限クランプ（既定 `-120.0`） |
| `--bin-count <n>` | 条件付き必須 | `stft-analysis` のみ必須（`n >= 1`） |
| `--delete-current` | 任意 | `stft-analysis` のみ。既存テーブルを削除して再作成 |
| `--recursive` | 任意 | `stft-analysis` のみ。`input-dir` 配下を再帰走査 |
| `--stems <csv>` | 任意 | `peak-analysis` のみ。解析対象 stem |
| `--ffmpeg-path <path>` | 任意 | 音声処理ツールのパス指定（現行実装では ffmpeg/ffprobe） |
| `--progress` | 任意 | 対話端末で2段プログレス表示を有効化（`stderr` 出力） |
| `--help`, `-h` | 任意 | ヘルプ表示 |

### 単位/サンプリング規則

- `sample` / `samples` は `stft-analysis` 専用です。`peak-analysis` ではエラーになります。
- `window` と `hop` の単位混在は許可します（例: `window=50ms`, `hop=512samples`）。
- `sample(s)` を1つでも使う場合は `--target-sampling <n>hz` が必須です（例: `44100hz`）。
- `peak-analysis` で `--target-sampling` を指定するとエラーになります。

### `stft-analysis` の追加制約

- `--bin-count` は `nextPow2(windowSamples)/2 + 1` を上限とします。
- `windowSamples` は最終的に解析時サンプルレート基準で評価されます。

既定テーブル名: `T_STFTAnalysis`  
既定モード文字列: `stft-analysis`

## 解析対象ファイルの解決

### `peak-analysis`

- 走査対象は `input-dir` 直下の1階層サブディレクトリ
- stem 一致は大文字小文字無視
- 同一 stem に複数拡張子がある場合の優先順は `wav -> flac -> m4a/caf -> others`

### `stft-analysis`

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

### `stft-analysis`

既定テーブル: `T_STFTAnalysis`

- `window` 列には入力指定値を保存します
- hop が `ms/s/m` 指定のとき:
  - 列: `idx`, `name`, `ch`, `window`, `ms`, `bin001..binNNN`, `create_at`, `modified_at`
  - 一意制約: `(name, ch, window, ms)`
  - インデックス: `(name)`, `(name, ch)`, `(name, ms)`, `(name, ch, ms)`
- hop が `sample/samples` 指定のとき:
  - 列: `idx`, `name`, `ch`, `window`, `sample`, `bin001..binNNN`, `create_at`, `modified_at`
  - 一意制約: `(name, ch, window, sample)`
  - インデックス: `(name)`, `(name, ch)`, `(name, sample)`, `(name, ch, sample)`

`--delete-current` 未指定時は、既存テーブルの次を検証し不一致なら失敗します。

- `binNNN` 列数と `--bin-count`
- hop 単位に対応するアンカー列（`ms` または `sample`）

## 音声処理の扱い

- 解析は `pcm_f32le` で実施します。
- `--target-sampling` 指定時は ffmpeg の `-ar <rate>` を解析パイプラインにだけ適用します。
- 入力音声ファイル自体の再エンコードやビット深度変更は行いません。

## SQLite ジャーナル運用

- 初期化時に `PRAGMA journal_mode=WAL` を試行します。
- 環境やファイルシステム制約で WAL へ切り替えできない場合は既存ジャーナルモードで継続します。

## 実行例

### peak-analysis

```powershell
SoundAnalyzer.Cli.exe --window-size 50ms --hop 10ms --input-dir /path/to/dir --db-file /path/to/file.db --mode peak-analysis --stems Piano,Drums,Vocals --table-name-override T_PEAK --upsert
```

### stft-analysis (ms基準)

```powershell
SoundAnalyzer.Cli.exe --window-size 50ms --hop 10ms --input-dir /path/to/dir --db-file /path/to/file.db --mode stft-analysis --bin-count 12 --table-name-override T_STFT --upsert --recursive --delete-current --progress
```

### stft-analysis (samples基準)

```powershell
SoundAnalyzer.Cli.exe --window-size 2048samples --hop 512samples --target-sampling 44100hz --input-dir /path/to/dir --db-file /path/to/file.db --mode stft-analysis --bin-count 24 --upsert
```
