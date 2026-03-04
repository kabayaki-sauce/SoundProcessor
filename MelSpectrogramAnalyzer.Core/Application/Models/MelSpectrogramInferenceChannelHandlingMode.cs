namespace MelSpectrogramAnalyzer.Core.Application.Models;

public enum MelSpectrogramInferenceChannelHandlingMode
{
    DuplicateMonoAndTakeFirstTwo = 0,
    StrictTwoChannels = 1,
}
