using System.Globalization;
using System.Text;
using Cli.Shared.Application.Ports;

namespace SoundAnalyzer.Cli.Infrastructure.Progress;

internal sealed class SoundAnalyzerProgressTracker : IDisposable
{
    private const int NameColumnWidth = 12;
    private const int SongsBarWidth = 24;
    private const int QueueBarWidth = 24;
    private const int WorkerGaugeWidth = 24;

    private readonly object sync = new();
    private readonly bool enabled;
    private readonly bool ansiEnabled;
    private readonly ITextBlockProgressDisplay progressDisplay;
    private readonly Dictionary<string, SongState> songs = new(StringComparer.OrdinalIgnoreCase);

    private WorkerState[] workers = Array.Empty<WorkerState>();
    private long totalSongs;
    private long totalEnqueued;
    private long totalInserted;
    private int queueCapacity = 1;
    private bool disposed;

    private SoundAnalyzerProgressTracker(
        bool enabled,
        bool ansiEnabled,
        ITextBlockProgressDisplay progressDisplay)
    {
        this.enabled = enabled;
        this.ansiEnabled = ansiEnabled;
        this.progressDisplay = progressDisplay ?? throw new ArgumentNullException(nameof(progressDisplay));
    }

    public static SoundAnalyzerProgressTracker Create(
        bool showProgress,
        ITextBlockProgressDisplayFactory progressDisplayFactory)
    {
        ArgumentNullException.ThrowIfNull(progressDisplayFactory);

        if (!showProgress)
        {
            return new SoundAnalyzerProgressTracker(false, false, NullTextBlockProgressDisplay.Instance);
        }

        ITextBlockProgressDisplay progressDisplay = progressDisplayFactory.Create(enabled: true);
        return new SoundAnalyzerProgressTracker(true, IsAnsiEnabled(), progressDisplay);
    }

    internal static SoundAnalyzerProgressTracker CreateForTest(
        ITextBlockProgressDisplay progressDisplay,
        bool ansiEnabled)
    {
        return new SoundAnalyzerProgressTracker(true, ansiEnabled, progressDisplay);
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
            progressDisplay.Complete();
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

        List<string> lines = BuildLines();
        progressDisplay.Report(lines, force);
    }

    private List<string> BuildLines()
    {
        long completedSongs = songs.Values.LongCount(static state => state.AnalyzeCompleted && state.Inserted >= state.Enqueued);
        long safeTotalSongs = totalSongs > 0 ? totalSongs : 1;
        double songsRatio = ClampRatio((double)completedSongs / safeTotalSongs);
        string songsPercent = (songsRatio * 100.0).ToString("0.0", CultureInfo.InvariantCulture);

        long queueDepth = Math.Max(0, totalEnqueued - totalInserted);
        double queueRatio = ClampRatio(queueCapacity > 0 ? (double)queueDepth / queueCapacity : 0);
        string queuePercent = (queueRatio * 100.0).ToString("0.0", CultureInfo.InvariantCulture);

        string songsLeft = string.Create(
            CultureInfo.InvariantCulture,
            $"Songs {Math.Min(completedSongs, safeTotalSongs)}/{safeTotalSongs}");
        string queueLeft = string.Create(
            CultureInfo.InvariantCulture,
            $"Queue {queuePercent,6}% {queueDepth}/{queueCapacity}");

        string workerCircles = string.Join(
            " ",
            workers.Select(worker => FormatThreadCircle(worker.IsActive)));
        string threadsLine = string.Create(CultureInfo.InvariantCulture, $"Threads {workerCircles}");

        List<WorkerLineState> workerLines = new(workers.Length);
        int gaugeStartColumn = Math.Max(GetDisplayLength(songsLeft), GetDisplayLength(queueLeft));
        for (int i = 0; i < workers.Length; i++)
        {
            WorkerState worker = workers[i];
            string active = FormatThreadCircle(worker.IsActive);
            string songLabel = string.IsNullOrWhiteSpace(worker.SongName) ? "(idle)" : worker.SongName;
            if (songLabel.Length > NameColumnWidth)
            {
                songLabel = songLabel[..NameColumnWidth];
            }

            string workerLeft = string.Create(
                CultureInfo.InvariantCulture,
                $"T{i + 1:00} {active} {songLabel,-12}");
            if (GetDisplayLength(workerLeft) > gaugeStartColumn)
            {
                gaugeStartColumn = GetDisplayLength(workerLeft);
            }

            WorkerProgress progress = ResolveWorkerProgress(worker);
            string mergedGauge = BuildMergedGauge(progress.AnalyzeRatio, progress.InsertRatio, WorkerGaugeWidth);
            string analyzePercent = (progress.AnalyzeRatio * 100.0).ToString("0.0", CultureInfo.InvariantCulture);
            string insertPercent = (progress.InsertRatio * 100.0).ToString("0.0", CultureInfo.InvariantCulture);
            workerLines.Add(new WorkerLineState(workerLeft, mergedGauge, analyzePercent, insertPercent));
        }

        gaugeStartColumn++;

        string songsBar = BuildProgressBar(songsRatio, SongsBarWidth, "97");
        string queueBar = BuildProgressBar(queueRatio, QueueBarWidth, "97");
        string songsLine = string.Create(
            CultureInfo.InvariantCulture,
            $"{PadRightDisplayWidth(songsLeft, gaugeStartColumn)}|{songsBar}| {songsPercent,6}%");
        string queueLine = string.Create(
            CultureInfo.InvariantCulture,
            $"{PadRightDisplayWidth(queueLeft, gaugeStartColumn)}|{queueBar}|");

        List<string> lines = new(3 + workers.Length)
        {
            songsLine,
            threadsLine,
            queueLine,
        };

        for (int i = 0; i < workerLines.Count; i++)
        {
            WorkerLineState workerLine = workerLines[i];
            string line = string.Create(
                CultureInfo.InvariantCulture,
                $"{PadRightDisplayWidth(workerLine.LeftLabel, gaugeStartColumn)}|{workerLine.Gauge}| A {workerLine.AnalyzePercent,5}% I {workerLine.InsertPercent,5}%");
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
        string symbol = isActive ? "●" : "○";
        if (!ansiEnabled)
        {
            return symbol;
        }

        return isActive
            ? Paint(symbol, "32")
            : Paint(symbol, "90");
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

    private static string PadRightDisplayWidth(string text, int targetDisplayWidth)
    {
        int displayLength = GetDisplayLength(text);
        if (displayLength >= targetDisplayWidth)
        {
            return text;
        }

        return string.Concat(text, new string(' ', targetDisplayWidth - displayLength));
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

    private static bool IsAnsiEnabled()
    {
        string? term = Environment.GetEnvironmentVariable("TERM");
        if (term is not null && term.Equals("dumb", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
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

    private readonly record struct WorkerLineState(
        string LeftLabel,
        string Gauge,
        string AnalyzePercent,
        string InsertPercent);

    private sealed class NullTextBlockProgressDisplay : ITextBlockProgressDisplay
    {
        public static NullTextBlockProgressDisplay Instance { get; } = new();

        private NullTextBlockProgressDisplay()
        {
        }

        public void Report(IReadOnlyList<string> lines, bool force = false)
        {
            _ = lines;
            _ = force;
        }

        public void Complete()
        {
        }
    }
}
