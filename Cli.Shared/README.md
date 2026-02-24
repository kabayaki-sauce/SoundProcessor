# Cli.Shared

## Role
`Cli.Shared` provides reusable console presentation utilities for CLI projects in this solution.

Current implementation includes:

- opt-in dual-line progress rendering
- TTY-aware fallback to no-op renderer
- stderr-targeted output to preserve stdout machine-readable payloads

## Main contracts

- `IProgressDisplay`
- `IProgressDisplayFactory`
- `DualProgressState`

## DI registration

Register with:

```csharp
services.AddCliShared();
```

`IProgressDisplayFactory.Create(enabled)` returns a no-op display when progress is disabled or when no interactive TTY is available.
