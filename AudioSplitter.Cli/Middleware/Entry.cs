using System.Text.Json;
using AudioProcessor.Application.Errors;
using AudioSplitter.Core.Application.Errors;
using AudioSplitter.Core.Application.Models;
using AudioSplitter.Core.Application.UseCases;
using AudioSplitter.Core.Extensions;
using AudioSplitter.Core.Application.Ports;
using AudioSplitter.Cli.Infrastructure.Console;
using AudioSplitter.Cli.Presentation.Cli.Arguments;
using AudioSplitter.Cli.Presentation.Cli.Errors;
using AudioSplitter.Cli.Presentation.Cli.Texts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AudioSplitter.Cli.Middleware;

internal static class Entry
{
    public static int Main(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        using IHost host = BuildHost();

        CommandLineParseResult parseResult = CommandLineParser.Parse(args);
        if (parseResult.IsHelpRequested)
        {
            System.Console.Out.WriteLine(ConsoleTexts.HelpText);
            return 0;
        }

        if (!parseResult.IsSuccess || parseResult.Arguments is null)
        {
            WriteErrors(parseResult.Errors);
            return 1;
        }

        using CancellationTokenSource cancellationTokenSource = new();
        ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellationTokenSource.Cancel();
        };

        System.Console.CancelKeyPress += cancelHandler;
        try
        {
            SplitAudioUseCase useCase = host.Services.GetRequiredService<SplitAudioUseCase>();
            SplitAudioRequest request = ToRequest(parseResult.Arguments);
            var executionResult = useCase.ExecuteAsync(
                    request,
                    cancellationTokenSource.Token)
                .GetAwaiter()
                .GetResult();
            string serialized = JsonSerializer.Serialize(executionResult.Summary);
            System.Console.Out.WriteLine(serialized);
            return 0;
        }
        catch (OperationCanceledException)
        {
            WriteErrors(new[] { ConsoleTexts.OperationCanceledText });
            return 1;
        }
        catch (SplitAudioException exception)
        {
            WriteErrors(new[] { CliErrorMapper.ToMessage(exception) });
            return 1;
        }
        catch (AudioProcessorException exception)
        {
            WriteErrors(new[] { CliErrorMapper.ToMessage(exception) });
            return 1;
        }
        finally
        {
            System.Console.CancelKeyPress -= cancelHandler;
        }
    }

    private static IHost BuildHost()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();

        builder.Services.AddAudioSplitterCore();
        builder.Services.AddSingleton<IOverwriteConfirmationService, OverwriteConfirmationService>();

        return builder.Build();
    }

    private static SplitAudioRequest ToRequest(CommandLineArguments arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        return new SplitAudioRequest(
            arguments.InputFilePath,
            arguments.OutputDirectoryPath,
            arguments.LevelDb,
            arguments.Duration,
            arguments.AfterOffset,
            arguments.ResumeOffset,
            arguments.ResolutionType,
            arguments.FfmpegPath,
            arguments.OverwriteWithoutPrompt);
    }

    private static void WriteErrors(IEnumerable<string> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        string serialized = JsonSerializer.Serialize(new ErrorPayload(errors.ToArray()));
        System.Console.Error.WriteLine(serialized);
    }

    private sealed class ErrorPayload
    {
        public ErrorPayload(IReadOnlyList<string> errors)
        {
            ArgumentNullException.ThrowIfNull(errors);
            Errors = errors;
        }

        public IReadOnlyList<string> Errors { get; }
    }
}

