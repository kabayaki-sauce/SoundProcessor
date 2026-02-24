# AudioSplitter.Cli

## 役割
`AudioSplitter.Cli` は、無音区間ベースで音声を分割するコマンドラインツールです。  
`AudioSplitter.Core` と `AudioProcessor` を利用して、入力音源を WAV セグメントとして書き出します。

## 実行形式

```powershell
AudioSplitter.Cli.exe --input-file <path> --output-dir <path> --level <dBFS> --duration <time> [options]
```

## オプション

| オプション | 必須 | 説明 |
|---|---|---|
| `--input-file <path>` | 必須 | 入力音声ファイル |
| `--output-dir <path>` | 必須 | 出力先ディレクトリ |
| `--level <dBFS>` | 必須 | 無音判定閾値（負値） |
| `--duration <time>` | 必須 | 無音継続時間閾値（`ms/s/m`） |
| `--after-offset <time>` | 任意 | 無音開始後に前ファイルへ残す長さ |
| `--resume-offset <time>` | 任意 | 次有音開始に対する次ファイル開始補正 |
| `--resolution-type <spec>` | 任意 | `16bit|24bit|32float,<rate>hz` |
| `--ffmpeg-path <path>` | 任意 | 音声処理ツールのパスまたは格納ディレクトリ（現行実装では ffmpeg/ffprobe） |
| `-y` | 任意 | 上書き確認を省略 |
| `--help`, `-h` | 任意 | ヘルプ表示 |

## 出力仕様

- 出力形式: WAV
- 命名規則: `{入力ファイル名}_{index:000}.wav`
- 成功時標準出力: サマリー JSON
- エラー時標準エラー: `{ "errors": [...] }`
- 終了コード: 成功 `0` / 失敗 `1`

## 実行例

```powershell
AudioSplitter.Cli.exe --input-file /path/to/file.wav --output-dir /path/to/out --level -48.0 --duration 2000ms --after-offset 500ms --resume-offset -200ms
```
