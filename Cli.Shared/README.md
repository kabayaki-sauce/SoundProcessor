# Cli.Shared

## 役割
`Cli.Shared` は、このソリューション内の CLI プロジェクトで再利用するための
コンソール表示ユーティリティを提供します。

現在の実装には、次の機能が含まれます。

- `--progress` 指定時のみ有効化される 2 段プログレス表示
- TTY 判定に基づく no-op 表示への自動フォールバック
- `stdout` の機械可読出力を維持するための `stderr` への進捗出力

## 主な契約

- `IProgressDisplay`
- `IProgressDisplayFactory`
- `DualProgressState`

## DI 登録

次の拡張メソッドで登録します。

```csharp
services.AddCliShared();
```

`IProgressDisplayFactory.Create(enabled)` は、進捗表示が無効の場合、または
対話的な TTY が利用できない場合に no-op 実装を返します。
