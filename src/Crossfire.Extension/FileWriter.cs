// Not ported from a Crossfire file: the file writer of the Crossfire extension (the design's section 10): the thread Wr.
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using System.Diagnostics;

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
}

/// <summary>
/// Writes the files of the data folder from its own thread. The worker hands it immutable snapshots (a file's whole content as bytes)
/// and never waits for it, except where <see cref="WaitWritten"/> is called (<c>session.shutdown</c> with save true, the exit).
/// A file is written to a temporary file in its own folder and renamed over the old one, so a reader sees the old or the new
/// content, never a part. The writer holds one lock, for its own table; it is never held while a file is written, and nothing a
/// request needs is behind it.
/// </summary>
internal sealed class FileWriter
{
    private readonly string root;
    private readonly FileWriterOptions options;
    private readonly Counters counters;
    private readonly Action<int, string> log;
    private readonly object gate = new();
    private readonly Thread thread;

    private Dictionary<string, byte[]> pending = new();
    private long submitted;       // the number of the last submission
    private long written;         // every submission up to this number is on disk (or was discarded)
    private long failed;          // the last submission that a failed pass could not write
    private long firstPending;    // timestamp of the oldest change not yet written
    private long lastChange;      // timestamp of the newest one
    private bool force;
    private bool stopping;

    public FileWriter(string root, FileWriterOptions options, Counters counters, Action<int, string> log)
    {
        this.root = root;
        this.options = options;
        this.counters = counters;
        this.log = log;
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
            if (pending.Count == 0)
            {
                firstPending = now;
            }
            lastChange = now;
            pending[relativePath] = content;
            submitted++;
            Monitor.PulseAll(gate);
        }
    }

    /// <summary>Asks for a write now instead of after the quiet time, and returns at once.</summary>
    public void Kick()
    {
        lock (gate)
        {
            force = true;
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
            force = true;
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
            return true;
        }
    }

    /// <summary>Forgets what was submitted and not yet written (<c>session.shutdown</c> with save false writes nothing).</summary>
    public void Discard()
    {
        lock (gate)
        {
            pending.Clear();
            written = submitted;
            force = false;
            Monitor.PulseAll(gate);
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
        if (relativePath.Length == 0 || relativePath[0] == '/' || relativePath.Contains('\\')
            || relativePath.Split('/').Any(part => part is "" or "." or ".."))
        {
            throw new ArgumentException("not a path under the data folder: " + relativePath, nameof(relativePath));
        }
    }

    private void Run()
    {
        for (; ; )
        {
            Dictionary<string, byte[]> batch;
            long batchNumber;
            lock (gate)
            {
                for (; ; )
                {
                    if (stopping)
                    {
                        return;
                    }
                    if (pending.Count == 0)
                    {
                        Monitor.Wait(gate);
                        continue;
                    }
                    long now = Stopwatch.GetTimestamp();
                    long due = force ? now : Math.Min(lastChange + Ticks(options.Quiet), firstPending + Ticks(options.MaxDelay));
                    if (now >= due)
                    {
                        break;
                    }
                    Monitor.Wait(gate, TimeSpan.FromSeconds((double)(due - now) / Stopwatch.Frequency));
                }
                batch = pending;
                pending = new Dictionary<string, byte[]>();
                batchNumber = submitted;
                force = false;
            }

            long started = Stopwatch.GetTimestamp();
            var notWritten = new Dictionary<string, byte[]>();
            foreach ((string path, byte[] content) in batch)
            {
                try
                {
                    WriteAtomically(path, content);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    counters.CountWriteFailure();
                    log(0, "the file " + path + " could not be written: " + e.Message);
                    notWritten[path] = content;
                }
            }
            counters.CountWrite(batch.Count - notWritten.Count, Stopwatch.GetTimestamp() - started);

            lock (gate)
            {
                if (notWritten.Count == 0)
                {
                    written = Math.Max(written, batchNumber);
                }
                else
                {
                    failed = Math.Max(failed, batchNumber);
                    foreach ((string path, byte[] content) in notWritten)
                    {
                        pending.TryAdd(path, content); // a newer submission of the same file stays
                    }
                    firstPending = lastChange = Stopwatch.GetTimestamp(); // tried again after the quiet time
                }
                Monitor.PulseAll(gate);
            }
        }
    }

    private static long Ticks(TimeSpan span) => (long)(span.TotalSeconds * Stopwatch.Frequency);

    private void WriteAtomically(string relativePath, byte[] content)
    {
        string path = Path.Combine(root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary = path + "." + Environment.ProcessId + ".tmp";
        try
        {
            using (var file = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                file.Write(content, 0, content.Length);
                file.Flush(true);
            }
            options.BeforeRename?.Invoke(temporary);
            File.Move(temporary, path, true);
        }
        catch
        {
            try
            {
                File.Delete(temporary);
            }
            catch (IOException)
            {
            }
            throw;
        }
    }
}
