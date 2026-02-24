using System.Globalization;
using System.Text;

namespace SoundAnalyzer.Cli.Infrastructure.Progress;

internal sealed class SoundAnalyzerProgressTracker : IDisposable
{
    private const int SongsBarWidth = 24;
    private const int QueueBarWidth = 24;
    private const int WorkerGaugeWidth = 24;
    private const int SongLabelWidth = 24;
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

    internal static SoundAnalyzerProgressTracker CreateForTest(bool ansiEnabled, TextWriter writer)
    {
        return new SoundAnalyzerProgressTracker(true, ansiEnabled, writer);
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

    public void SetSongExpectedPoints(string songName, long expectedPoints)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(songName);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(expectedPoints);

        lock (sync)
        {
            ThrowIfDisposed();
            SongState songState = EnsureSong(songName);
            songState.ExpectedPoints = expectedPoints;
            songState.ExpectedPointsKnown = true;
            Render();
        }
    }

    public void MarkSongExpectedPointsUnknown(string songName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(songName);

        lock (sync)
        {
            ThrowIfDisposed();
            SongState songState = EnsureSong(songName);
            songState.ExpectedPointsKnown = false;
            songState.ExpectedPoints = null;
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
        double songsRatio = ClampRatio((double)completedSongs / safeTotalSongs);
        string songsBar = BuildProgressBar(songsRatio, SongsBarWidth, "97");
        string songsPercent = (songsRatio * 100.0).ToString("0.0", CultureInfo.InvariantCulture);

        string songsLine = string.Create(
            CultureInfo.InvariantCulture,
            $"Songs {Math.Min(completedSongs, safeTotalSongs)}/{safeTotalSongs} |{songsBar}| {songsPercent,6}%");

        string workerCircles = string.Join(
            " ",
            workers.Select(worker => FormatThreadCircle(worker.IsActive)));
        string threadsLine = string.Create(CultureInfo.InvariantCulture, $"Threads {workerCircles}");

        long queueDepth = Math.Max(0, totalEnqueued - totalInserted);
        double queueRatio = ClampRatio(queueCapacity > 0 ? (double)queueDepth / queueCapacity : 0);
        string queueBar = BuildProgressBar(queueRatio, QueueBarWidth, "97");
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
            string active = FormatThreadCircle(worker.IsActive);
            string songLabel = string.IsNullOrWhiteSpace(worker.SongName) ? "(idle)" : worker.SongName;
            if (songLabel.Length > SongLabelWidth)
            {
                songLabel = songLabel[..SongLabelWidth];
            }

            WorkerProgress progress = ResolveWorkerProgress(worker);
            string mergedGauge = BuildMergedGauge(progress.AnalyzeRatio, progress.InsertRatio, WorkerGaugeWidth);
            string analyzePercent = (progress.AnalyzeRatio * 100.0).ToString("0.0", CultureInfo.InvariantCulture);
            string insertPercent = (progress.InsertRatio * 100.0).ToString("0.0", CultureInfo.InvariantCulture);

            string line = string.Create(
                CultureInfo.InvariantCulture,
                $"T{i + 1:00} {active} {songLabel,-24} |{mergedGauge}| A {analyzePercent,5}% I {insertPercent,5}%");
            lines.Add(line);
        }

        return lines;
    }

    private string BuildProgressBar(double ratio, int width, string filledColorCode)
    {
        int filled = checked((int)Math.Round(ClampRatio(ratio) * width, MidpointRounding.AwayFromZero));
        if (filled > width)
        {
            filled = width;
        }

        if (!ansiEnabled)
        {
            char[] chars = new char[width];
            for (int i = 0; i < width; i++)
            {
                chars[i] = i < filled ? '█' : '░';
            }

            return new(chars);
        }

        StringBuilder builder = new(capacity: width + 24);
        if (filled > 0)
        {
            builder.Append(Paint(new string('█', filled), filledColorCode));
        }

        if (filled < width)
        {
            builder.Append(Paint(new string('░', width - filled), "90"));
        }

        return builder.ToString();
    }

    private string BuildMergedGauge(double analyzeRatio, double insertRatio, int width)
    {
        double clampedAnalyze = ClampRatio(analyzeRatio);
        double clampedInsert = ClampRatio(insertRatio);
        if (clampedInsert > clampedAnalyze)
        {
            clampedInsert = clampedAnalyze;
        }

        int analyzeCells = checked((int)Math.Round(clampedAnalyze * width, MidpointRounding.AwayFromZero));
        int insertCells = checked((int)Math.Round(clampedInsert * width, MidpointRounding.AwayFromZero));
        if (analyzeCells > width)
        {
            analyzeCells = width;
        }

        if (insertCells > analyzeCells)
        {
            insertCells = analyzeCells;
        }

        int analyzeOnlyCells = analyzeCells - insertCells;
        int remainingCells = width - analyzeCells;

        if (!ansiEnabled)
        {
            StringBuilder plainBuilder = new(capacity: width);
            if (insertCells > 0)
            {
                plainBuilder.Append(new string('█', insertCells));
            }

            if (analyzeOnlyCells > 0)
            {
                plainBuilder.Append(new string('▓', analyzeOnlyCells));
            }

            if (remainingCells > 0)
            {
                plainBuilder.Append(new string('░', remainingCells));
            }

            return plainBuilder.ToString();
        }

        StringBuilder builder = new(capacity: width + 24);
        if (insertCells > 0)
        {
            builder.Append(Paint(new string('█', insertCells), "32"));
        }

        if (analyzeOnlyCells > 0)
        {
            builder.Append(Paint(new string('█', analyzeOnlyCells), "97"));
        }

        if (remainingCells > 0)
        {
            builder.Append(Paint(new string('░', remainingCells), "90"));
        }

        return builder.ToString();
    }

    private string FormatThreadCircle(bool isActive)
    {
        if (ansiEnabled)
        {
            return isActive ? PaintGreen("●") : PaintGray("●");
        }

        return isActive ? "●" : "○";
    }

    private WorkerProgress ResolveWorkerProgress(WorkerState worker)
    {
        if (string.IsNullOrWhiteSpace(worker.SongName))
        {
            return WorkerProgress.Zero;
        }

        bool analyzeCompleted = worker.AnalyzeCompleted;
        if (!songs.TryGetValue(worker.SongName, out SongState? songState))
        {
            return analyzeCompleted
                ? new WorkerProgress(1, 0)
                : WorkerProgress.Zero;
        }

        analyzeCompleted |= songState.AnalyzeCompleted;

        if (songState.ExpectedPointsKnown && songState.ExpectedPoints is long expectedPoints && expectedPoints > 0)
        {
            double analyzeRatio = analyzeCompleted
                ? 1
                : ClampRatio((double)songState.Enqueued / expectedPoints);
            double insertRatio = Math.Min(
                analyzeRatio,
                ClampRatio((double)songState.Inserted / expectedPoints));
            return new WorkerProgress(analyzeRatio, insertRatio);
        }

        if (!analyzeCompleted)
        {
            return WorkerProgress.Zero;
        }

        long fallbackDenominator = Math.Max(songState.Enqueued, 1);
        double fallbackInsertRatio = ClampRatio((double)songState.Inserted / fallbackDenominator);
        return new WorkerProgress(1, fallbackInsertRatio);
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
        writer.Write(text);
        int displayLength = GetDisplayLength(text);
        if (displayLength < width)
        {
            writer.Write(new string(' ', width - displayLength));
        }

        writer.WriteLine();
        writer.Flush();
    }

    private static int GetDisplayLength(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        int displayLength = 0;
        int index = 0;
        while (index < text.Length)
        {
            if (text[index] == '\u001b' && index + 1 < text.Length && text[index + 1] == '[')
            {
                int escapeTail = index + 2;
                while (escapeTail < text.Length && text[escapeTail] != 'm')
                {
                    escapeTail++;
                }

                index = escapeTail < text.Length ? escapeTail + 1 : text.Length;
                continue;
            }

            displayLength++;
            index++;
        }

        return displayLength;
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

        public bool ExpectedPointsKnown { get; set; }

        public long? ExpectedPoints { get; set; }
    }

    private sealed class WorkerState
    {
        public bool IsActive { get; set; }

        public string SongName { get; set; } = string.Empty;

        public bool AnalyzeCompleted { get; set; }
    }

    private readonly record struct WorkerProgress(double AnalyzeRatio, double InsertRatio)
    {
        public static WorkerProgress Zero { get; } = new(0, 0);
    }
}
