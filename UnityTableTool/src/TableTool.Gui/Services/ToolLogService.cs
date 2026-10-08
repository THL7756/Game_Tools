using System.Collections.ObjectModel;

namespace TableTool.Gui.Services;

public sealed record ToolLogEntry(
    DateTime Time,
    string Level,
    string Source,
    string Message)
{
    public string TimeText => Time.ToString("HH:mm:ss");
    public string CopyText => $"{Time:yyyy-MM-dd HH:mm:ss}\t{Level}\t{Source}\t{Message}";
}

public static class ToolLogService
{
    private const int MaxEntries = 1000;
    private static readonly object Sync = new();
    private static readonly List<ToolLogEntry> Entries = [];

    public static event EventHandler<ToolLogEntry>? EntryAdded;
    public static event EventHandler? Cleared;

    public static void Add(string level, string source, string message)
    {
        var entry = new ToolLogEntry(DateTime.Now, level, source, message);
        lock (Sync)
        {
            Entries.Add(entry);
            if (Entries.Count > MaxEntries)
                Entries.RemoveRange(0, Entries.Count - MaxEntries);
        }

        EntryAdded?.Invoke(null, entry);
    }

    public static void Info(string source, string message) => Add("INFO", source, message);
    public static void Success(string source, string message) => Add("SUCCESS", source, message);
    public static void Warning(string source, string message) => Add("WARNING", source, message);
    public static void Error(string source, string message) => Add("ERROR", source, message);

    public static void Clear()
    {
        lock (Sync)
            Entries.Clear();
        Cleared?.Invoke(null, EventArgs.Empty);
    }

    public static IReadOnlyList<ToolLogEntry> Snapshot()
    {
        lock (Sync)
            return new ReadOnlyCollection<ToolLogEntry>(Entries.ToArray());
    }
}
