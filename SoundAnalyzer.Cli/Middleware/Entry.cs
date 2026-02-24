using System.Text.Json;
using AudioProcessor.Application.Errors;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PeakAnalyzer.Core.Application.Errors;
using PeakAnalyzer.Core.Extensions;
using SoundAnalyzer.Cli.Infrastructure.Execution;
using SoundAnalyzer.Cli.Presentation.Cli.Arguments;
using SoundAnalyzer.Cli.Presentation.Cli.Errors;
using SoundAnalyzer.Cli.Presentation.Cli.Texts;

namespace SoundAnalyzer.Cli.Middleware;

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
            PeakAnalysisBatchExecutor executor = host.Services.GetRequiredService<PeakAnalysisBatchExecutor>();
            PeakAnalysisBatchSummary summary = executor
                .ExecuteAsync(parseResult.Arguments, cancellationTokenSource.Token)
                .GetAwaiter()
                .GetResult();

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
        catch (PeakAnalysisException exception)
        {
            WriteErrors(new[] { CliErrorMapper.ToMessage(exception) });
            return 1;
        }
        catch (AudioProcessorException exception)
        {
            WriteErrors(new[] { CliErrorMapper.ToMessage(exception) });
            return 1;
        }
        catch (SqliteException exception)
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

        builder.Services.AddPeakAnalyzerCore();
        builder.Services.AddSingleton<PeakAnalysisBatchExecutor>();

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
