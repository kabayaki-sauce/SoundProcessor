namespace SoundAnalyzer.Cli.Infrastructure.Sqlite;

internal enum SqliteConflictMode
{
    Error = 0,
    Upsert,
    SkipDuplicate,
}
