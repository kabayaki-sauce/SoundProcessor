using System.Text;
using Cli.Shared.Application.Ports;
using Cli.Shared.Infrastructure.Console;

namespace Cli.Shared.Tests.Infrastructure.Console;

public sealed class TextBlockProgressDisplayFactoryTests
{
    private static readonly Encoding LegacySingleByteEncoding = Encoding.ASCII;

    [Fact]
    public void Create_ShouldReturnNoOp_WhenDisabled()
    {
        TextBlockProgressDisplayFactory factory = new(
            new FakeConsoleEnvironment(
                isErrorRedirected: false,
                isUserInteractive: true,
                initialOutputEncoding: LegacySingleByteEncoding));

        ITextBlockProgressDisplay display = factory.Create(enabled: false);

        Assert.Same(NoOpTextBlockProgressDisplay.Instance, display);
    }

    [Fact]
    public void Create_ShouldReturnNoOp_WhenErrorIsRedirected()
    {
        TextBlockProgressDisplayFactory factory = new(
            new FakeConsoleEnvironment(
                isErrorRedirected: true,
                isUserInteractive: true,
                initialOutputEncoding: LegacySingleByteEncoding));

        ITextBlockProgressDisplay display = factory.Create(enabled: true);

        Assert.Same(NoOpTextBlockProgressDisplay.Instance, display);
    }

    [Fact]
    public void Create_ShouldReturnNoOp_WhenEnvironmentIsNonInteractive()
    {
        TextBlockProgressDisplayFactory factory = new(
            new FakeConsoleEnvironment(
                isErrorRedirected: false,
                isUserInteractive: false,
                initialOutputEncoding: LegacySingleByteEncoding));

        ITextBlockProgressDisplay display = factory.Create(enabled: true);

        Assert.Same(NoOpTextBlockProgressDisplay.Instance, display);
    }

    [Fact]
    public void Create_ShouldReturnTextBlockDisplay_WhenEnabledAndInteractive()
    {
        FakeConsoleEnvironment environment = new(
            isErrorRedirected: false,
            isUserInteractive: true,
            initialOutputEncoding: LegacySingleByteEncoding);
        TextBlockProgressDisplayFactory factory = new(environment);

        ITextBlockProgressDisplay display = factory.Create(enabled: true);

        Assert.IsType<TextBlockProgressDisplay>(display);
        Assert.Equal(["EnsureUtf8OutputEncoding", "ErrorWriter"], environment.CallSequence);
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
    }
}
