using System.Text.Json;
using Cli.Shared.Application.Ports;
using Cli.Shared.Extensions;
using AudioProcessor.Application.Errors;
using AudioSplitter.Cli.Infrastructure.Execution;
using AudioSplitter.Core.Application.Errors;
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
            SplitAudioBatchExecutor executor = host.Services.GetRequiredService<SplitAudioBatchExecutor>();
            IProgressDisplayFactory progressDisplayFactory = host.Services.GetRequiredService<IProgressDisplayFactory>();
            IProgressDisplay progressDisplay = progressDisplayFactory.Create(parseResult.Arguments.Progress);
            SplitAudioBatchSummary summary = executor.ExecuteAsync(
                    parseResult.Arguments,
                    progressDisplay,
                    cancellationTokenSource.Token)
                .GetAwaiter()
                .GetResult();
            progressDisplay.Complete();

            string serialized = JsonSerializer.Serialize(summary);
            System.Console.Out.WriteLine(serialized);
            return 0;
        }
        catch (OperationCanceledException)
        {
            WriteErrors(new[] { ConsoleTexts.OperationCanceledText });
            return 1;
        }
        catch (CliException exception)
        {
            WriteErrors(new[] { CliErrorMapper.ToMessage(exception) });
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

        builder.Services.AddCliShared();
        builder.Services.AddAudioSplitterCore();
        builder.Services.AddSingleton<IOverwriteConfirmationService, OverwriteConfirmationService>();
        builder.Services.AddSingleton<SplitAudioBatchExecutor>();

        return builder.Build();
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
