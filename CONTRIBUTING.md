# CONTRIBUTING

## 目的
このドキュメントは、本リポジトリへの貢献手順と品質基準を定義します。  
対象はコード、ドキュメント、テスト、ビルド設定のすべてです。

## 開発前提

- `.NET SDK 10.x`
- 音声処理に必要な外部ツールが実行可能であること（現行実装では `ffmpeg` / `ffprobe`。`PATH` もしくは CLI 引数で指定）
- PowerShell もしくは互換シェルでコマンド実行できること

## 基本フロー

1. 変更対象と影響範囲を明確化する。
2. 既存実装・既存ドキュメントを確認する。
3. 必要な変更を実装する。
4. ローカル検証を完了してから PR を作成する。

## ブランチ運用

- 本リポジトリのブランチ戦略は `git-flow` とする。
- 機能開発は、最新の `develop` ブランチを起点として `feature` ブランチを作成して開始する。
- `feature` ブランチ名は `feature/{issueNo}-{概要}` とする（`issueNo` は対応 Issue 番号）。

## 必須ローカル検証

```powershell
dotnet build AudioProcessor.slnx -warnaserror
dotnet test AudioProcessor.slnx
dotnet run --project AudioSplitter.Cli -- --help
dotnet run --project SoundAnalyzer.Cli -- --help
```

## Pull Request に必ず含める情報

- 背景と目的
- 変更内容の要約
- 互換性影響（破壊的変更の有無）
- 実施した検証コマンドと結果
- 既知の制約、未対応事項

## ドキュメント変更時の確認項目

- リンク切れがないこと
- コマンド例が現行実装と一致すること
- 用語・表記（英数字、単位、パス表現）が統一されていること
- ライセンス記述と矛盾がないこと

## ライセンス同意

本リポジトリへ貢献されたコード・ドキュメントは、リポジトリで定義されたライセンス条件の下で公開されます。  
詳細は `LICENSE` および `LICENSE_en.md` を参照してください。
