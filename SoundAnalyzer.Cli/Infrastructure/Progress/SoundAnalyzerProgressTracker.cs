using System.Globalization;
using System.Text;

namespace SoundAnalyzer.Cli.Infrastructure.Progress;

internal sealed class SoundAnalyzerProgressTracker : IDisposable
{
    private const int QueueBarWidth = 24;
    private static readonly TimeSpan MinRenderInterval = TimeSpan.FromMilliseconds(80);

    private readonly object sync = new();
    private readonly bool enabled;
    private readonly bool ansiEnabled;
    private readonly TextWriter writer;
    private readonly Dictionary<string, SongState> songs = new(StringComparer.OrdinalIgnoreCase);

    private WorkerState[] workers = Array.Empty<WorkerState>();
    private DateTimeOffset lastRenderAt;
    private int originTop = -1;
    private int renderedLineCount;
    private long totalSongs;
    private long totalEnqueued;
    private long totalInserted;
    private int queueCapacity = 1;
    private bool disposed;

    private SoundAnalyzerProgressTracker(bool enabled, bool ansiEnabled, TextWriter writer)
    {
        this.enabled = enabled;
        this.ansiEnabled = ansiEnabled;
        this.writer = writer ?? throw new ArgumentNullException(nameof(writer));
    }

    public static SoundAnalyzerProgressTracker Create(bool showProgress)
    {
        if (!showProgress)
        {
            return new SoundAnalyzerProgressTracker(false, false, TextWriter.Null);
        }

        bool interactive = !System.Console.IsErrorRedirected && Environment.UserInteractive;
        if (!interactive)
        {
            return new SoundAnalyzerProgressTracker(false, false, TextWriter.Null);
        }

        System.Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        bool ansiEnabled = IsAnsiEnabled();
        return new SoundAnalyzerProgressTracker(true, ansiEnabled, System.Console.Error);
    }

    public void Configure(IReadOnlyList<string> songNames, int threadCount, int queueCapacity)
    {
        ArgumentNullException.ThrowIfNull(songNames);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(threadCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(queueCapacity);

        lock (sync)
        {
            ThrowIfDisposed();

            songs.Clear();
            for (int i = 0; i < songNames.Count; i++)
            {
                string songName = songNames[i];
                if (string.IsNullOrWhiteSpace(songName))
                {
                    continue;
                }

                songs[songName] = new SongState();
            }

            workers = new WorkerState[threadCount];
            for (int i = 0; i < workers.Length; i++)
            {
                workers[i] = new WorkerState();
            }

            totalSongs = songNames.Count;
            totalEnqueued = 0;
            totalInserted = 0;
            this.queueCapacity = queueCapacity;

            Render(force: true);
        }
    }

    public void SetWorkerSong(int workerId, string songName)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(workerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(songName);

        lock (sync)
        {
            ThrowIfDisposed();
            WorkerState worker = GetWorker(workerId);
            worker.IsActive = true;
            worker.SongName = songName;
            worker.AnalyzeCompleted = false;
            EnsureSong(songName);
            Render();
        }
    }

    public void MarkWorkerAnalyzeCompleted(int workerId)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(workerId);

        lock (sync)
        {
            ThrowIfDisposed();
            WorkerState worker = GetWorker(workerId);
            if (!string.IsNullOrWhiteSpace(worker.SongName)
                && songs.TryGetValue(worker.SongName, out SongState? songState))
            {
                songState.AnalyzeCompleted = true;
            }

            worker.AnalyzeCompleted = true;
            Render();
        }
    }

    public void SetWorkerIdle(int workerId)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(workerId);

        lock (sync)
        {
            ThrowIfDisposed();
            WorkerState worker = GetWorker(workerId);
            worker.IsActive = false;
            worker.SongName = string.Empty;
            worker.AnalyzeCompleted = false;
            Render();
        }
    }

    public void IncrementEnqueued(string songName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(songName);

        lock (sync)
        {
            ThrowIfDisposed();
            SongState songState = EnsureSong(songName);
            songState.Enqueued++;
            totalEnqueued++;
            Render();
        }
    }

    public void IncrementInserted(string songName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(songName);

        lock (sync)
        {
            ThrowIfDisposed();
            SongState songState = EnsureSong(songName);
            songState.Inserted++;
            totalInserted++;
            Render();
        }
    }

    public void Complete()
    {
        lock (sync)
        {
            if (disposed)
            {
                return;
            }

            Render(force: true);
            if (enabled && renderedLineCount > 0 && originTop >= 0)
            {
                TrySetCursorPosition(0, originTop + renderedLineCount);
            }
        }
    }

    public void Dispose()
    {
        lock (sync)
        {
            if (disposed)
            {
                return;
            }

            Complete();
            disposed = true;
        }
    }

    private void Render(bool force = false)
    {
        if (!enabled)
        {
            return;
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        if (!force && now - lastRenderAt < MinRenderInterval)
        {
            return;
        }

        RenderCore();
        lastRenderAt = now;
    }

    private void RenderCore()
    {
        List<string> lines = BuildLines();
        int width = ResolveWidth();

        if (originTop < 0)
        {
            originTop = GetCurrentCursorTop();
        }
        else
        {
            TrySetCursorPosition(0, originTop);
        }

        for (int i = 0; i < lines.Count; i++)
        {
            WriteLineFixed(lines[i], width);
        }

        if (renderedLineCount > lines.Count)
        {
            for (int i = lines.Count; i < renderedLineCount; i++)
            {
                WriteLineFixed(string.Empty, width);
            }
        }

        renderedLineCount = lines.Count;
    }

    private List<string> BuildLines()
    {
        long completedSongs = songs.Values.LongCount(static state => state.AnalyzeCompleted && state.Inserted >= state.Enqueued);
        long safeTotalSongs = totalSongs > 0 ? totalSongs : 1;

        string songsLine = string.Create(
            CultureInfo.InvariantCulture,
            $"Songs {Math.Min(completedSongs, safeTotalSongs)}/{safeTotalSongs}");

        string workerCircles = string.Join(
            " ",
            workers.Select(worker => worker.IsActive ? PaintGreen("●") : PaintGray("●")));
        string threadsLine = string.Create(CultureInfo.InvariantCulture, $"Threads {workerCircles}");

        long queueDepth = Math.Max(0, totalEnqueued - totalInserted);
        double queueRatio = ClampRatio(queueCapacity > 0 ? (double)queueDepth / queueCapacity : 0);
        int filled = checked((int)Math.Round(queueRatio * QueueBarWidth, MidpointRounding.AwayFromZero));
        if (filled > QueueBarWidth)
        {
            filled = QueueBarWidth;
        }

        char[] queueChars = new char[QueueBarWidth];
        for (int i = 0; i < queueChars.Length; i++)
        {
            queueChars[i] = i < filled ? '█' : '░';
        }

        string queueBar = new(queueChars);
        string queuePercent = (queueRatio * 100.0).ToString("0.0", CultureInfo.InvariantCulture);
        string queueLine = string.Create(
            CultureInfo.InvariantCulture,
            $"Queue {queuePercent,6}% |{queueBar}| {queueDepth}/{queueCapacity}");

        List<string> lines = new(3 + workers.Length)
        {
            songsLine,
            threadsLine,
            queueLine,
        };

        for (int i = 0; i < workers.Length; i++)
        {
            WorkerState worker = workers[i];
            string active = worker.IsActive ? PaintGreen("●") : PaintGray("●");
            string songLabel = string.IsNullOrWhiteSpace(worker.SongName) ? "(idle)" : worker.SongName;
            string analyzeState = ResolveAnalyzeState(worker);
            string insertState = ResolveInsertState(worker);

            string line = string.Create(
                CultureInfo.InvariantCulture,
                $"T{i + 1:00} {active} {songLabel,-32} Analyze:{analyzeState} Insert:{insertState}");
            lines.Add(line);
        }

        return lines;
    }

    private string ResolveAnalyzeState(WorkerState worker)
    {
        if (string.IsNullOrWhiteSpace(worker.SongName))
        {
            return "-";
        }

        if (worker.AnalyzeCompleted)
        {
            return PaintWhite("●");
        }

        return PaintGray("○");
    }

    private string ResolveInsertState(WorkerState worker)
    {
        if (string.IsNullOrWhiteSpace(worker.SongName))
        {
            return "-";
        }

        if (!songs.TryGetValue(worker.SongName, out SongState? songState))
        {
            return PaintGray("○");
        }

        bool insertCompleted = songState.AnalyzeCompleted && songState.Inserted >= songState.Enqueued;
        return insertCompleted ? PaintGreen("●") : PaintGray("○");
    }

    private SongState EnsureSong(string songName)
    {
        if (!songs.TryGetValue(songName, out SongState? songState))
        {
            songState = new SongState();
            songs[songName] = songState;
        }

        return songState;
    }

    private WorkerState GetWorker(int workerId)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(workerId, workers.Length);

        return workers[workerId];
    }

    private string PaintGreen(string text) => Paint(text, "32");

    private string PaintGray(string text) => Paint(text, "90");

    private string PaintWhite(string text) => Paint(text, "97");

    private string Paint(string text, string code)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(code);

        if (!ansiEnabled)
        {
            return text;
        }

        return string.Create(CultureInfo.InvariantCulture, $"\u001b[{code}m{text}\u001b[0m");
    }

    private void WriteLineFixed(string text, int width)
    {
        string safeText = text.Length <= width ? text : text[..width];
        writer.Write(safeText);
        if (safeText.Length < width)
        {
            writer.Write(new string(' ', width - safeText.Length));
        }

        writer.WriteLine();
        writer.Flush();
    }

    private static bool IsAnsiEnabled()
    {
        string? term = Environment.GetEnvironmentVariable("TERM");
        if (term is not null && term.Equals("dumb", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private static int ResolveWidth()
    {
        try
        {
            return Math.Max(60, System.Console.BufferWidth - 1);
        }
        catch (Exception ex) when (ex is IOException or ArgumentOutOfRangeException)
        {
            return 160;
        }
    }

    private static int GetCurrentCursorTop()
    {
        try
        {
            return System.Console.CursorTop;
        }
        catch (Exception ex) when (ex is IOException or ArgumentOutOfRangeException)
        {
            return 0;
        }
    }

    private void TrySetCursorPosition(int left, int top)
    {
        try
        {
            System.Console.SetCursorPosition(left, top);
        }
        catch (Exception ex) when (ex is IOException or ArgumentOutOfRangeException)
        {
            originTop = GetCurrentCursorTop();
        }
    }

    private static double ClampRatio(double ratio)
    {
        if (double.IsNaN(ratio) || double.IsInfinity(ratio))
        {
            return 0;
        }

        if (ratio <= 0)
        {
            return 0;
        }

        if (ratio >= 1)
        {
            return 1;
        }

        return ratio;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }

    private sealed class SongState
    {
        public long Enqueued { get; set; }

        public long Inserted { get; set; }

        public bool AnalyzeCompleted { get; set; }
    }

    private sealed class WorkerState
    {
        public bool IsActive { get; set; }

        public string SongName { get; set; } = string.Empty;

        public bool AnalyzeCompleted { get; set; }
    }
}
