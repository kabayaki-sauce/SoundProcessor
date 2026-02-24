# SoundAnalyzer.Cli

## 役割
`SoundAnalyzer.Cli` はディレクトリ配下の音声ファイルを一括解析し、結果を SQLite / PostgreSQL へ保存する CLI です。  
`peak-analysis` は `PeakAnalyzer.Core`、`stft-analysis` は `STFTAnalyzer.Core` を利用します。

Windows / Linux は必須サポート対象です（SQLite モード・Postgres モードの双方で同一方針を適用します）。
macOS は任意検証対象です。

## 実行形式

```powershell
SoundAnalyzer.Cli.exe --window-size <len> --hop <len> --input-dir <path> --mode <peak-analysis|stft-analysis> [--db-file <path> | --postgres ...] [options]
```

```bash
dotnet SoundAnalyzer.Cli.dll --window-size <len> --hop <len> --input-dir <path> --mode <peak-analysis|stft-analysis> [--db-file <path> | --postgres ...] [options]
```

## クロスプラットフォーム方針

- パス解釈は OS 依存の区切り文字を前提にせず、`.NET` の `Path` API を基準とします。
- 証明書/鍵/known_hosts を含むファイル読み込みは `.NET` 標準 I/O で統一します。
- エラーメッセージは OS 固有コマンド名に依存しない共通文言を採用します。

## オプション

| オプション | 必須 | 説明 |
|---|---|---|
| `--window-size <len>` | 必須 | 解析窓サイズ。`ms/s/m/sample/samples` を受理（整数値へ変換可能であること） |
| `--hop <len>` | 必須 | hop。`ms/s/m/sample/samples` を受理（整数値へ変換可能であること） |
| `--input-dir <path>` | 必須 | 入力ルート |
| `--db-file <path>` | 条件付き必須 | SQLite モード時に必須（`--postgres` 指定時は指定不可） |
| `--postgres` | 任意 | PostgreSQL モードを有効化 |
| `--postgres-host <host>` | 条件付き必須 | `--postgres` 指定時に必須 |
| `--postgres-port <port>` | 条件付き必須 | `--postgres` 指定時に必須 |
| `--postgres-db <name>` | 条件付き必須 | `--postgres` 指定時に必須 |
| `--postgres-user <user>` | 条件付き必須 | `--postgres` 指定時に必須 |
| `--postgres-password <pw>` | 任意 | パスワード認証。`sslcert+sslkey` と排他 |
| `--postgres-sslcert-path <path>` | 任意 | クライアント証明書。`sslkey` と同時指定必須 |
| `--postgres-sslkey-path <path>` | 任意 | クライアント秘密鍵。`sslcert` と同時指定必須 |
| `--postgres-sslrootcert-path <path>` | 任意 | ルート証明書（CA） |
| `--postgres-ssh-host <host>` | 任意 | 指定時に SSH トンネルを自動有効化 |
| `--postgres-ssh-port <port>` | 任意 | SSH ポート（既定 `22`） |
| `--postgres-ssh-user <user>` | 条件付き必須 | `--postgres-ssh-host` 指定時に必須 |
| `--postgres-ssh-private-key-path <path>` | 条件付き必須 | `--postgres-ssh-host` 指定時に必須 |
| `--postgres-ssh-known-hosts-path <path>` | 条件付き必須 | `--postgres-ssh-host` 指定時に必須 |
| `--postgres-batch-row-count <n>` | 任意 | PostgreSQL モード専用。複数行 INSERT バッチ行数（既定 `1`、PostgreSQLパラメータ上限で自動クランプ） |
| `--mode <value>` | 必須 | `peak-analysis` または `stft-analysis` |
| `--target-sampling <n>hz` | 条件付き必須 | `stft-analysis` かつ `window/hop` のどちらかが `sample(s)` 指定の場合に必須 |
| `--table-name-override <name>` | 任意 | テーブル名上書き |
| `--upsert` | 任意 | 競合時更新 |
| `--skip-duplicate` | 任意 | 競合時スキップ |
| `--min-limit-db <dB>` | 任意 | 下限クランプ（既定 `-120.0`） |
| `--bin-count <n>` | 条件付き必須 | `stft-analysis` のみ必須（`n >= 1`） |
| `--delete-current` | 任意 | `stft-analysis` のみ。既存テーブルを削除して再作成 |
| `--recursive` | 任意 | `stft-analysis` のみ。`input-dir` 配下を再帰走査 |
| `--stft-proc-threads <n>` | 任意 | `stft-analysis` のみ。1ファイル内の解析処理スレッド数（既定 `1`） |
| `--peak-proc-threads <n>` | 任意 | `peak-analysis` のみ。1 Song 内の解析処理スレッド数（既定 `1`） |
| `--stft-file-threads <n>` | 任意 | `stft-analysis` のみ。同時解析ファイル数（既定 `1`） |
| `--peak-file-threads <n>` | 任意 | `peak-analysis` のみ。同時解析 Song 数（既定 `1`） |
| `--insert-queue-size <n>` | 任意 | 解析と DB Insert の間に置く bounded queue の容量（既定 `1024`） |
| `--sqlite-batch-row-count <n>` | 任意 | SQLite モード専用。複数行 INSERT バッチ行数（既定 `512`、SQLite変数上限で自動クランプ） |
| `--sqlite-fast-mode` | 任意 | SQLite モード専用。書込PRAGMAを速度優先へ切替（`synchronous=OFF` 等、耐障害性トレードオフあり） |
| `--stems <csv>` | 任意 | `peak-analysis` のみ。解析対象 stem |
| `--ffmpeg-path <path>` | 任意 | 音声処理ツールのパス指定（現行実装では ffmpeg/ffprobe） |
| `--show-progress` | 任意 | 対話端末で詳細進捗表示（Songs/Threads/Queue）を有効化（`stderr` 出力）。Thread行は単一ゲージ（Insert=緑、Analyze-only=白、未処理=斑点） |
| `--help`, `-h` | 任意 | ヘルプ表示 |

### 単位/サンプリング規則

- `sample` / `samples` は `stft-analysis` 専用です。`peak-analysis` ではエラーになります。
- `window` と `hop` の単位混在は許可します（例: `window=50ms`, `hop=512samples`）。
- `sample(s)` を1つでも使う場合は `--target-sampling <n>hz` が必須です（例: `44100hz`）。
- モード不一致オプションはエラーではなく warning として無視されます（`stderr` に JSON の `warnings` を出力）。
- 互換性注意: `--progress` は廃止され、`--show-progress` のみ受理します。

### Storage バックエンド切替規則

- 既定は SQLite モードです。
- `--postgres` 指定時のみ PostgreSQL モードに切り替わります。
- PostgreSQL モード時は `--db-file` を指定できません。
- PostgreSQL モード時は `--sqlite-fast-mode` / `--sqlite-batch-row-count` を指定できません。
- `--postgres-batch-row-count` は PostgreSQL モード専用です（SQLite モードでは指定できません）。
- SQLite モード時に PostgreSQL 専用オプションを指定するとエラーになります。
- PostgreSQL 認証は `--postgres-password` と `--postgres-sslcert-path + --postgres-sslkey-path` が排他です。
- `sslcert` と `sslkey` は同時指定必須です（片側のみはエラー）。
- 認証情報が未指定の場合は warning を出して接続試行を継続します。
- `--postgres-ssh-host` 指定時は SSH トンネルを自動有効化します。
- SSH 有効時は `ssh-user` / `ssh-private-key-path` / `ssh-known-hosts-path` が必須です。

### `--show-progress` 表示仕様

- Songs: 完了Song数/全体 + 進捗バー
- Threads: file worker の稼働状態（緑丸/灰丸）
- Queue: Insert queue 占有率（`(enqueued-inserted)/capacity`）
- Thread行ゲージ:
  - 単一ゲージ上で `Insert済=緑`、`Analyze済未Insert=白`、`未処理=斑点` を表示
  - 通常は Analyze が Insert より先行して伸び、Insert が追従して緑化します
  - `EstimatedTotalFrames` が取得できないSongは Analyze完了まで不確定表示（斑点中心）とし、Analyze完了後は `inserted/enqueued` でInsertドレインを表示します
  - `name` 列幅は固定12で表示します
  - `Songs` / `Queue` / `Thread` はゲージ開始列を共通化し、横位置を揃えて表示します

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

- STFT bin は縦持ちで保存されます（1行 = 1bin）
- `window` 列には入力指定値を保存します
- hop が `ms/s/m` 指定のとき:
  - 列: `idx`, `name`, `ch`, `window`, `ms`, `bin_no`, `db`, `create_at`, `modified_at`
  - 一意制約: `(name, ch, window, ms, bin_no)`
  - インデックス: `(name)`, `(name, ch)`, `(name, ms)`, `(name, ch, ms)`, `(name, ch, window, ms)`
- hop が `sample/samples` 指定のとき:
  - 列: `idx`, `name`, `ch`, `window`, `sample`, `bin_no`, `db`, `create_at`, `modified_at`
  - 一意制約: `(name, ch, window, sample, bin_no)`
  - インデックス: `(name)`, `(name, ch)`, `(name, sample)`, `(name, ch, sample)`, `(name, ch, window, sample)`

`--delete-current` 未指定時は、既存テーブルの次を検証し不一致なら失敗します。

- 既存データの `bin_no` 分布（`MAX(bin_no)` / `COUNT(DISTINCT bin_no)`）と `--bin-count`
- hop 単位に対応するアンカー列（`ms` または `sample`）

旧 wide 形式（`bin001..binNNN` 列）の STFT テーブルを検知した場合は互換実行せず失敗します。  
その場合は `--delete-current` で再作成するか、`--table-name-override` で新規テーブルを指定してください。

## PostgreSQL スキーマ

- SQLite と同じ論理列を維持します（Peak: `idx,name,stem,window,ms,db,create_at,modified_at` / STFT: `idx,name,ch,window,ms|sample,bin_no,db,create_at,modified_at`）。
- 競合モードは SQLite と同等です（Error / Upsert / SkipDuplicate）。
- 既存テーブル時の STFT スキーマ検証 / bin-count 検証 / `--delete-current` 挙動は SQLite と同等です。
- インデックスは SQLite 相当を基本とし、STFT では PostgreSQL 向けに `(name, ch, window, anchor)` 補助インデックスを明示作成します。
- PostgreSQL 側も複数行 `INSERT ... VALUES` バッチで挿入します（`--postgres-batch-row-count` 既定 `1`）。
  - PostgreSQL のパラメータ上限（`65535`）を超えないように自動クランプされます。

## 音声処理の扱い

- 解析は `pcm_f32le` で実施します。
- `--target-sampling` 指定時は ffmpeg の `-ar <rate>` を解析パイプラインにだけ適用します。
- 入力音声ファイル自体の再エンコードやビット深度変更は行いません。

## SQLite ジャーナル運用

- 初期化時に `PRAGMA journal_mode=WAL` を試行します。
- 環境やファイルシステム制約で WAL へ切り替えできない場合は既存ジャーナルモードで継続します。

## SQLite 大量投入チューニング

- STFT/Peak ともに内部で複数行 `INSERT ... VALUES (...), (...)` を使用します。
- `--sqlite-batch-row-count` でバッチ行数を指定できます（既定 `512`）。
  - SQLite の変数上限（`MAX_VARIABLE_NUMBER`）を超えないように自動クランプされます。
- `--sqlite-fast-mode` 指定時は次の PRAGMA を追加適用します。
  - `synchronous=OFF`
  - `locking_mode=EXCLUSIVE`
  - `temp_store=MEMORY`
  - `cache_size=-262144`
- `--sqlite-fast-mode` は速度優先モードです。異常終了時の耐障害性は低下するため、投入ジョブ専用DBでの運用を推奨します。
- 新規テーブルかつ競合モード `Error` の場合は、投入完了時 (`Complete`) にユニーク/二次インデックスを後建てして挿入速度を優先します。

## 実行例

### peak-analysis

```powershell
SoundAnalyzer.Cli.exe --window-size 50ms --hop 10ms --input-dir /path/to/dir --db-file /path/to/file.db --mode peak-analysis --stems Piano,Drums,Vocals --peak-file-threads 2 --peak-proc-threads 4 --insert-queue-size 2048 --table-name-override T_PEAK --upsert --show-progress
```

### stft-analysis (ms基準)

```powershell
SoundAnalyzer.Cli.exe --window-size 50ms --hop 10ms --input-dir /path/to/dir --db-file /path/to/file.db --mode stft-analysis --bin-count 12 --stft-file-threads 2 --stft-proc-threads 6 --insert-queue-size 4096 --sqlite-batch-row-count 512 --sqlite-fast-mode --table-name-override T_STFT --upsert --recursive --delete-current --show-progress
```

### stft-analysis (samples基準)

```powershell
SoundAnalyzer.Cli.exe --window-size 2048samples --hop 512samples --target-sampling 44100hz --input-dir /path/to/dir --db-file /path/to/file.db --mode stft-analysis --bin-count 24 --upsert
```

### Linux 実行例（stft-analysis）

```bash
dotnet SoundAnalyzer.Cli.dll --window-size 2048samples --hop 512samples --target-sampling 44100hz --input-dir /path/to/dir --db-file /path/to/file.db --mode stft-analysis --bin-count 24 --upsert
```

### PostgreSQL 実行例（Windows, password 認証）

```powershell
SoundAnalyzer.Cli.exe --window-size 50ms --hop 10ms --input-dir C:\audio\split --mode stft-analysis --bin-count 24 --postgres --postgres-host 127.0.0.1 --postgres-port 5432 --postgres-db audio --postgres-user analyzer --postgres-password secret --postgres-batch-row-count 512 --table-name-override T_STFT --upsert --show-progress
```

### PostgreSQL 実行例（Linux, SSH トンネル）

```bash
dotnet SoundAnalyzer.Cli.dll --window-size 50ms --hop 10ms --input-dir /data/audio/split --mode peak-analysis --postgres --postgres-host 10.0.0.12 --postgres-port 5432 --postgres-db audio --postgres-user analyzer --postgres-password secret --postgres-batch-row-count 256 --postgres-ssh-host bastion.example.com --postgres-ssh-user ubuntu --postgres-ssh-private-key-path /home/ubuntu/.ssh/id_ed25519 --postgres-ssh-known-hosts-path /home/ubuntu/.ssh/known_hosts --show-progress
```
