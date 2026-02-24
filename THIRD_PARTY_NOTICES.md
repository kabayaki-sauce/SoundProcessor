# THIRD_PARTY_NOTICES

## 注意事項

本リポジトリは第三者ライブラリを利用しています。  
本リポジトリ本体のライセンスとは別に、各ライブラリのライセンス条件が適用されます。

以下は、現時点の主要な直接依存の一覧です。

## 1. 実行時依存（Runtime Dependencies）

| Package | Version | License | Used In | Notes |
|---|---:|---|---|---|
| CommunityToolkit.Mvvm | 8.4.0 | MIT | `AudioSplitter.Cli` | MVVM ユーティリティ |
| Microsoft.Extensions.DependencyInjection | 10.0.3 | MIT | `AudioProcessor`, `AudioSplitter.Core`, `PeakAnalyzer.Core`, `AudioSplitter.Cli`, `SoundAnalyzer.Cli` | DI コンテナ |
| Microsoft.Extensions.Hosting | 10.0.3 | MIT | `AudioSplitter.Cli`, `SoundAnalyzer.Cli` | Host 構成 |
| Microsoft.Data.Sqlite | 10.0.0 | MIT | `SoundAnalyzer.Cli` | SQLite アクセス |
| NAudio | 2.2.1 | MIT（license.txt） | `AudioSplitter.Cli` | 音声関連ユーティリティ |
| OpenTK.Audio.OpenAL | 4.9.4 | MIT | `AudioSplitter.Cli` | OpenAL バインディング |

## 2. 開発時依存（Development / Test / Analysis）

| Package | Version | License | Used In | Notes |
|---|---:|---|---|---|
| Microsoft.CodeAnalysis.NetAnalyzers | 10.0.100 | MIT | 全プロジェクト（`Directory.Build.props`） | 静的解析 |
| SonarAnalyzer.CSharp | 10.15.0.120848 | Sonar Source-Available License（LICENSE.txt） | 全プロジェクト（`Directory.Build.props`） | 静的解析（Source-Available） |
| Microsoft.NET.Test.Sdk | 17.12.0 | MIT | `*.Tests` | テスト実行基盤 |
| xunit | 2.9.2 | Apache-2.0 | `*.Tests` | テストフレームワーク |
| xunit.runner.visualstudio | 3.0.0 | Apache-2.0 | `*.Tests` | テストランナー |
| coverlet.collector | 6.0.2 | MIT | `*.Tests` | カバレッジ収集 |

## 更新時の確認コマンド例

```powershell
# 主要参照ファイル確認
Get-Content Directory.Packages.props
Get-ChildItem -Recurse -Filter *.csproj | ForEach-Object { Get-Content $_.FullName }

# 依存パッケージ復元と検証
dotnet restore AudioProcessor.slnx
dotnet build AudioProcessor.slnx -warnaserror
```

最終的な遵守義務は各依存ライブラリの公式ライセンス本文に従ってください。
