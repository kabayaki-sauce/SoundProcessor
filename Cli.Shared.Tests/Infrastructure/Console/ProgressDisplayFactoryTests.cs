using System.Text;
using Cli.Shared.Application.Ports;
using Cli.Shared.Infrastructure.Console;

namespace Cli.Shared.Tests.Infrastructure.Console;

public sealed class ProgressDisplayFactoryTests
{
    private static readonly Encoding LegacySingleByteEncoding = Encoding.ASCII;

    [Fact]
    public void Create_ShouldReturnNoOp_WhenDisabled()
    {
        ProgressDisplayFactory factory = new(
            new FakeConsoleEnvironment(
                isErrorRedirected: false,
                isUserInteractive: true,
                initialOutputEncoding: LegacySingleByteEncoding));

        IProgressDisplay display = factory.Create(enabled: false);

        Assert.Same(NoOpProgressDisplay.Instance, display);
    }

    [Fact]
    public void Create_ShouldReturnNoOp_WhenErrorIsRedirected()
    {
        ProgressDisplayFactory factory = new(
            new FakeConsoleEnvironment(
                isErrorRedirected: true,
                isUserInteractive: true,
                initialOutputEncoding: LegacySingleByteEncoding));

        IProgressDisplay display = factory.Create(enabled: true);

        Assert.Same(NoOpProgressDisplay.Instance, display);
    }

    [Fact]
    public void Create_ShouldReturnNoOp_WhenEnvironmentIsNonInteractive()
    {
        ProgressDisplayFactory factory = new(
            new FakeConsoleEnvironment(
                isErrorRedirected: false,
                isUserInteractive: false,
                initialOutputEncoding: LegacySingleByteEncoding));

        IProgressDisplay display = factory.Create(enabled: true);

        Assert.Same(NoOpProgressDisplay.Instance, display);
    }

    [Fact]
    public void Create_ShouldReturnDualLineDisplay_WhenEnabledAndInteractive()
    {
        FakeConsoleEnvironment environment = new(
            isErrorRedirected: false,
            isUserInteractive: true,
            initialOutputEncoding: LegacySingleByteEncoding);
        ProgressDisplayFactory factory = new(environment);

        IProgressDisplay display = factory.Create(enabled: true);

        Assert.IsType<DualLineProgressDisplay>(display);
        Assert.Equal(["EnsureUtf8OutputEncoding", "ErrorWriter"], environment.CallSequence);
    }

    [Fact]
    public void Create_ShouldPreserveProgressGlyphs_WhenEnvironmentStartsInSingleByteEncoding()
    {
        FakeConsoleEnvironment environment = new(
            isErrorRedirected: false,
            isUserInteractive: true,
            initialOutputEncoding: LegacySingleByteEncoding);
        ProgressDisplayFactory factory = new(environment);

        IProgressDisplay display = factory.Create(enabled: true);
        display.Report(new("Top", 0.5, "Bottom", 0.25));

        string rendered = environment.RenderedText;
        Assert.Contains('█', rendered);
        Assert.Contains('░', rendered);
        Assert.DoesNotContain("??", rendered, StringComparison.Ordinal);
    }

    [Fact]
    public void Regression_WhenWriterCapturedBeforeUtf8_ShouldCorruptProgressGlyphs()
    {
        FakeConsoleEnvironment environment = new(
            isErrorRedirected: false,
            isUserInteractive: true,
            initialOutputEncoding: LegacySingleByteEncoding);

        TextWriter writerCapturedBeforeEnsure = environment.ErrorWriter;
        environment.EnsureUtf8OutputEncoding();
        DualLineProgressDisplay display = new(writerCapturedBeforeEnsure);

        display.Report(new("Top", 0.5, "Bottom", 0.25));

        string rendered = environment.RenderedText;
        Assert.Contains('?', rendered);
        Assert.DoesNotContain('█', rendered);
        Assert.DoesNotContain('░', rendered);
    }

    private sealed class FakeConsoleEnvironment : IConsoleEnvironment
    {
        private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

        private EncodingAwareStringWriter? errorWriter;
        private Encoding outputEncoding;

        public FakeConsoleEnvironment(
            bool isErrorRedirected,
            bool isUserInteractive,
            Encoding initialOutputEncoding)
        {
            IsErrorRedirected = isErrorRedirected;
            IsUserInteractive = isUserInteractive;
            outputEncoding = initialOutputEncoding;
        }

        public bool IsErrorRedirected { get; }

        public bool IsUserInteractive { get; }

        public List<string> CallSequence { get; } = new();

        public string RenderedText => errorWriter?.ToString() ?? string.Empty;

        public void EnsureUtf8OutputEncoding()
        {
            CallSequence.Add("EnsureUtf8OutputEncoding");
            outputEncoding = Utf8NoBom;
        }

        public TextWriter ErrorWriter
        {
            get
            {
                CallSequence.Add("ErrorWriter");
                errorWriter ??= new EncodingAwareStringWriter(outputEncoding);
                return errorWriter;
            }
        }
    }

    private sealed class EncodingAwareStringWriter : StringWriter
    {
        private readonly Encoding encoding;

        public EncodingAwareStringWriter(Encoding encoding)
        {
            this.encoding = encoding ?? throw new ArgumentNullException(nameof(encoding));
        }

        public override Encoding Encoding => encoding;

        public override void Write(char value)
        {
            base.Write(RoundTripEncoding(value.ToString()));
        }

        public override void Write(string? value)
        {
            base.Write(RoundTripEncoding(value));
        }

        public override void Write(char[] buffer, int index, int count)
        {
            ArgumentNullException.ThrowIfNull(buffer);
            base.Write(RoundTripEncoding(new string(buffer, index, count)));
        }

        private string RoundTripEncoding(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value ?? string.Empty;
            }

            byte[] bytes = encoding.GetBytes(value);
            return encoding.GetString(bytes);
        }
    }
}
