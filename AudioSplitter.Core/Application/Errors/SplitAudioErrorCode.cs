namespace AudioSplitter.Core.Application.Errors;

public enum SplitAudioErrorCode
{
    None = 0,
    InputFileNotFound,
    OutputDirectoryCreationFailed,
    OverwriteConflictInNonInteractive,
}
