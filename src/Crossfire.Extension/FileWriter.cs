// Not ported from a Crossfire file: the file writer of the Crossfire extension (the design's section 10): the thread Wr.
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace Crossfire.Extension;

/// <summary>When the writer writes (the defaults are the protocol's 5 s; the tests shorten them).</summary>
internal sealed class FileWriterOptions
{
    /// <summary>A change is written this long after the last change that followed it.</summary>
    public TimeSpan Quiet { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>No change waits longer than this, however many others follow it.</summary>
    public TimeSpan MaxDelay { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>Called on the writer thread after a temporary file is complete and before it is renamed (a test looks at the folder here).</summary>
    public Action<string>? BeforeRename { get; init; }

    /// <summary>The longest wait before a failed write is tried again (the wait starts at <see cref="Quiet"/> and doubles).</summary>
    public TimeSpan MaxRetryDelay { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>After this many failed passes in a row the files that could not be written are given up (counted, logged once).</summary>
    public int MaxFailedPasses { get; init; } = 8;
}

/// <summary>
/// Writes the files of the data folder from its own thread. The worker hands it immutable snapshots (a file's whole content as bytes)
/// and never waits for it, except where <see cref="WaitWritten"/> is called (<c>session.shutdown</c> with save true, the exit).
/// A file is written to a temporary file in its own folder and renamed over the old one, so a reader sees the old or the new
/// content, never a part. The writer holds one lock, for its own table; it is never held while a file is written, and nothing a
/// request needs is behind it.
/// </summary>
internal sealed partial class FileWriter
{
    private readonly string root;
    private readonly FileWriterOptions options;
    private readonly Counters counters;
    private readonly Action<int, string> log;
    private readonly object gate = new();
    private readonly Thread thread;

    private Dictionary<string, byte[]> pending = new();           // files not yet tried: written by the quiet time
    private readonly Dictionary<string, Retry> retrying = new(); // files whose last write failed: each waits its own back-off
    private long submitted;       // the number of the last submission
    private long written;         // every submission up to this number is on disk (or was discarded)
    private long failed;          // the last submission that a failed pass could not write
    private long dropped;         // the last submission of which a file was given up (it will never be on disk)
    private long generation;      // raised by Discard: a batch of an older generation writes nothing more
    private readonly object moveGate = new(); // held for the check and the rename of one file, never for a whole write
    private long firstPending;    // timestamp of the oldest change not yet written
    private long lastChange;      // timestamp of the newest one
    private bool force;
    private bool stopping;

    /// <summary>A file whose write failed: its newest content, how many passes failed in a row and when it may be tried again.</summary>
    private sealed class Retry
    {
        public Retry(byte[] content, int passes, long at)
        {
            Content = content;
            Passes = passes;
            At = at;
        }

        public byte[] Content { get; set; }

        public int Passes { get; }

        public long At { get; }
    }

    public FileWriter(string root, FileWriterOptions options, Counters counters, Action<int, string> log)
    {
        this.root = root;
        this.options = options;
        this.counters = counters;
        this.log = log;
        CleanTemporaries(root);
        thread = new Thread(Run) { IsBackground = true, Name = "crossfire-writer" };
        thread.Start();
    }

    /// <summary>Gives the writer the whole new content of a file (a path under the data folder, with <c>/</c>); the newest content of a name wins.</summary>
    public void Submit(string relativePath, byte[] content)
    {
        CheckPath(relativePath);
        lock (gate)
        {
            long now = Stopwatch.GetTimestamp();
            if (retrying.TryGetValue(relativePath, out Retry? waiting))
            {
                waiting.Content = content; // the newest content, tried when the file's own back-off is over
            }
            else
            {
                if (pending.Count == 0)
                {
                    firstPending = now;
                }
                lastChange = now;
                pending[relativePath] = content;
            }
            submitted++;
            Monitor.PulseAll(gate);
        }
    }

    /// <summary>
    /// Whether everything submitted is on disk: nothing pending or being written, no file given up. A pure query: it neither forces a
    /// write nor clears an earlier failure (unlike <see cref="WaitWritten"/>).
    /// </summary>
    public bool IsIdle()
    {
        lock (gate)
        {
            return pending.Count == 0 && retrying.Count == 0 && written >= submitted && dropped == 0;
        }
    }

    /// <summary>Asks for a write now instead of after the quiet time, and returns at once.</summary>
    public void Kick()
    {
        lock (gate)
        {
            if (pending.Count > 0 || retrying.Count > 0)
            {
                force = true; // with nothing pending the flag would outlive its write and defeat the quiet time of the next change
            }
            Monitor.PulseAll(gate);
        }
    }

    /// <summary>
    /// Writes now what was submitted so far and waits until it is on disk. False when it was not done within
    /// <paramref name="timeout"/> or a file could not be written.
    /// </summary>
    public bool WaitWritten(TimeSpan timeout)
    {
        lock (gate)
        {
            long target = submitted;
            failed = 0; // a failure before this call is tried again
            if (pending.Count > 0 || retrying.Count > 0)
            {
                force = true;
            }
            Monitor.PulseAll(gate);
            long deadline = Stopwatch.GetTimestamp() + (long)(timeout.TotalSeconds * Stopwatch.Frequency);
            while (written < target)
            {
                if (failed >= target)
                {
                    return false;
                }
                long left = deadline - Stopwatch.GetTimestamp();
                if (left <= 0)
                {
                    return false;
                }
                Monitor.Wait(gate, TimeSpan.FromSeconds((double)left / Stopwatch.Frequency));
            }
            return dropped == 0 || dropped < target; // a file given up at or before this call's submissions is not on disk
        }
    }

    /// <summary>Forgets what was submitted and not yet written (<c>session.shutdown</c> with save false writes nothing).</summary>
    public void Discard()
    {
        lock (gate)
        {
            pending.Clear();
            retrying.Clear();
            written = submitted;
            force = false;
            generation++;
            Monitor.PulseAll(gate);
        }
        // a batch that is being written finishes no file after this: the one rename that may be under way is waited for
        lock (moveGate)
        {
        }
    }

    /// <summary>Ends the thread (what is pending is not written: <see cref="WaitWritten"/> first); true when it ended within <paramref name="timeout"/>.</summary>
    public bool Stop(TimeSpan timeout)
    {
        lock (gate)
        {
            stopping = true;
            Monitor.PulseAll(gate);
        }
        return thread.Join(timeout);
    }

    private static void CheckPath(string relativePath)
    {
        if (relativePath.Length == 0 || relativePath[0] == '/' || relativePath.Contains('\\') || relativePath.Any(char.IsControl)
            || relativePath.EndsWith(".tmp", StringComparison.Ordinal)
            || relativePath.Split('/').Any(part => part is "" or "." or ".."))
        {
            throw new ArgumentException("not a path under the data folder: " + relativePath, nameof(relativePath));
        }
    }

    private void Run()
    {
        for (; ; )
        {
            var batch = new Dictionary<string, byte[]>();
            var passesBefore = new Dictionary<string, int>();
            long batchNumber;
            long batchGeneration;
            bool seriesOpen;
            lock (gate)
            {
                bool takeOrdinary;
                for (; ; )
                {
                    if (stopping)
                    {
                        return;
                    }
                    if (pending.Count == 0 && retrying.Count == 0)
                    {
                        Monitor.Wait(gate);
                        continue;
                    }
                    long now = Stopwatch.GetTimestamp();
                    long ordinaryDue = pending.Count == 0 ? long.MaxValue : Math.Min(lastChange + Ticks(options.Quiet), firstPending + Ticks(options.MaxDelay));
                    takeOrdinary = pending.Count > 0 && (force || now >= ordinaryDue);
                    long retryDue = long.MaxValue;
                    foreach ((string path, Retry r) in retrying)
                    {
                        if (force || now >= r.At)
                        {
                            batch[path] = r.Content;
                            passesBefore[path] = r.Passes;
                        }
                        else
                        {
                            retryDue = Math.Min(retryDue, r.At);
                        }
                    }
                    if (takeOrdinary || batch.Count > 0)
                    {
                        break;
                    }
                    long next = Math.Min(ordinaryDue, retryDue);
                    Monitor.Wait(gate, TimeSpan.FromSeconds((double)(next - now) / Stopwatch.Frequency));
                }
                foreach (string path in batch.Keys)
                {
                    retrying.Remove(path);
                }
                if (takeOrdinary)
                {
                    foreach ((string path, byte[] content) in pending)
                    {
                        batch[path] = content;
                        passesBefore.TryAdd(path, 0);
                    }
                    pending = new Dictionary<string, byte[]>();
                }
                seriesOpen = retrying.Count > 0;
                batchNumber = submitted;
                batchGeneration = generation;
                force = false;
            }

            long started = Stopwatch.GetTimestamp();
            var notWritten = new Dictionary<string, byte[]>();
            var given = new List<string>();
            foreach ((string path, byte[] content) in batch)
            {
                try
                {
                    WriteAtomically(path, content, batchGeneration);
                }
                catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException)
                {
                    counters.CountWriteFailure(); // a name that can never be written is not tried again
                    log(0, "the file " + path + " cannot be written and is given up: " + e.Message);
                    given.Add(path);
                }
                catch (Exception e)
                {
                    counters.CountWriteFailure();
                    if (!seriesOpen && passesBefore[path] == 0)
                    {
                        seriesOpen = true; // the first failure of a series is logged, not every file's
                        log(0, "the file " + path + " could not be written (it is tried again, later and later): " + e.Message);
                    }
                    notWritten[path] = content;
                }
            }
            counters.CountWrite(batch.Count - notWritten.Count - given.Count, Stopwatch.GetTimestamp() - started);

            lock (gate)
            {
                if (generation == batchGeneration)
                {
                    if (given.Count > 0)
                    {
                        dropped = Math.Max(dropped, batchNumber);
                    }
                    var gaveUp = new List<string>();
                    foreach ((string path, byte[] content) in notWritten)
                    {
                        int passes = passesBefore[path] + 1;
                        if (passes >= options.MaxFailedPasses)
                        {
                            gaveUp.Add(path);
                            pending.Remove(path); // a newer submission of a file given up is given up with it
                            continue;
                        }
                        // tried again later and later: Quiet, twice that, ... up to MaxRetryDelay, for this file only
                        double seconds = Math.Min(options.MaxRetryDelay.TotalSeconds, options.Quiet.TotalSeconds * Math.Pow(2, passes - 1));
                        long at = Stopwatch.GetTimestamp() + (long)(seconds * Stopwatch.Frequency);
                        // a newer submission of the same file (it came while this one was written) is the content that waits
                        byte[] newest = pending.Remove(path, out byte[]? newer) ? newer : content;
                        retrying[path] = new Retry(newest, passes, at);
                        failed = Math.Max(failed, batchNumber);
                    }
                    if (gaveUp.Count > 0)
                    {
                        log(0, gaveUp.Count + " file(s) could not be written in " + options.MaxFailedPasses + " passes and are given up: " + gaveUp[0]);
                        dropped = Math.Max(dropped, batchNumber);
                        failed = Math.Max(failed, batchNumber);
                    }
                    if (pending.Count == 0 && retrying.Count == 0)
                    {
                        written = Math.Max(written, submitted); // nothing is outstanding: everything is on disk or given up
                    }
                } // else: discarded while it was written; Discard has accounted for everything and nothing comes back
                Monitor.PulseAll(gate);
            }
        }
    }

    private static long Ticks(TimeSpan span) => (long)(span.TotalSeconds * Stopwatch.Frequency);

    private void WriteAtomically(string relativePath, byte[] content, long batchGeneration)
    {
        string path = Path.Combine(root, relativePath);
        string folder = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(folder);
        string temporary = path + "." + Environment.ProcessId + ".tmp";
        try
        {
            using (var file = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                file.Write(content, 0, content.Length);
                file.Flush(true);
            }
            options.BeforeRename?.Invoke(temporary);
            lock (moveGate)
            {
                if (Volatile.Read(ref generation) != batchGeneration)
                {
                    File.Delete(temporary); // discarded: the file stays as it was
                    return;
                }
                File.Move(temporary, path, true);
            }
            SyncFolder(folder);
        }
        catch
        {
            try
            {
                File.Delete(temporary);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
            }
            throw;
        }
    }

    // open(2) and fsync(2) of the folder, so that the rename itself survives a power loss; the flags are Linux's. Best effort: a
    // file system that cannot sync a folder is not a failed write.
    [DllImport("libc", SetLastError = true)]
    private static extern int open([MarshalAs(UnmanagedType.LPUTF8Str)] string path, int flags);

    [DllImport("libc", SetLastError = true)]
    private static extern int fsync(int fd);

    [DllImport("libc")]
    private static extern int close(int fd);

    private static void SyncFolder(string folder)
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }
        int fd = open(folder, 0x10000 | 0x80000); // O_DIRECTORY | O_CLOEXEC, read only
        if (fd >= 0)
        {
            fsync(fd);
            close(fd);
        }
    }

    /// <summary>Removes the temporary files an older process left in a crash (the caller holds the lock of the folder).</summary>
    private static void CleanTemporaries(string root)
    {
        try
        {
            foreach (string file in Directory.EnumerateFiles(root, "*.tmp", SearchOption.AllDirectories))
            {
                if (TemporaryName().IsMatch(Path.GetFileName(file)))
                {
                    File.Delete(file);
                }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // what cannot be removed stays; it is never read
        }
    }

    [GeneratedRegex(@"\.\d+\.tmp$")]
    private static partial Regex TemporaryName();
}
