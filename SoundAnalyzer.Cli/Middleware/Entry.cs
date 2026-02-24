using System.Text.Json;
using AudioProcessor.Application.Errors;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PeakAnalyzer.Core.Application.Errors;
using PeakAnalyzer.Core.Extensions;
using STFTAnalyzer.Core.Application.Errors;
using STFTAnalyzer.Core.Extensions;
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

        if (parseResult.Warnings.Count > 0)
        {
            WriteWarnings(parseResult.Warnings);
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
            CommandLineArguments arguments = parseResult.Arguments;
            object summary = Execute(arguments, host.Services, cancellationTokenSource.Token);

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
        catch (StftAnalysisException exception)
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

    private static object Execute(
        CommandLineArguments arguments,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(services);

        return arguments.Mode switch
        {
            ConsoleTexts.PeakAnalysisMode => services.GetRequiredService<PeakAnalysisBatchExecutor>()
                .ExecuteAsync(arguments, cancellationToken)
                .GetAwaiter()
                .GetResult(),
            ConsoleTexts.StftAnalysisMode => services.GetRequiredService<StftAnalysisBatchExecutor>()
                .ExecuteAsync(arguments, cancellationToken)
                .GetAwaiter()
                .GetResult(),
            _ => throw new CliException(CliErrorCode.UnsupportedMode, arguments.Mode),
        };
    }

    private static IHost BuildHost()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();

        builder.Services.AddPeakAnalyzerCore();
        builder.Services.AddStftAnalyzerCore();
        builder.Services.AddSingleton<PeakAnalysisBatchExecutor>();
        builder.Services.AddSingleton<StftAnalysisBatchExecutor>();

        return builder.Build();
    }

    private static void WriteErrors(IEnumerable<string> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        string serialized = JsonSerializer.Serialize(new ErrorPayload(errors.ToArray()));
        System.Console.Error.WriteLine(serialized);
    }

    private static void WriteWarnings(IEnumerable<string> warnings)
    {
        ArgumentNullException.ThrowIfNull(warnings);
        string serialized = JsonSerializer.Serialize(new WarningPayload(warnings.ToArray()));
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

    private sealed class WarningPayload
    {
        public WarningPayload(IReadOnlyList<string> warnings)
        {
            ArgumentNullException.ThrowIfNull(warnings);
            Warnings = warnings;
        }

        public IReadOnlyList<string> Warnings { get; }
    }
}
