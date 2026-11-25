// Not ported from a Crossfire file: the lock on the data folder (docs/protocol.md section 1, "Running").
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Crossfire.Extension;

/// <summary>
/// An exclusive <c>flock(2)</c> on a file, held until it is disposed or the process ends (the kernel then drops it, a crash
/// included). The lock belongs to the open file, so another process that opens the same file, and another <see cref="DataLock"/> in
/// this process, see it as held.
/// </summary>
internal sealed class DataLock : IDisposable
{
    private const int LockEx = 2;
    private const int LockNb = 4;
    private const int ErrInterrupted = 4;
    private const int ErrWouldBlock = 11;

    // open(2) directly: File.OpenHandle makes the runtime take advisory locks of its own on the file (for FileShare), which would make
    // a plain open fail while another process holds the flock, instead of waiting for it. The flags are Linux's.
    private const int OpenReadWrite = 2;
    private const int OpenCreate = 0x40;
    private const int OpenCloseOnExec = 0x80000;
    private const int Mode = 0x180; // 0600

    /// <summary>How often a held lock is tried again (flock has no wait with a time limit).</summary>
    public static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(25);

    private readonly SafeFileHandle handle;

    private DataLock(SafeFileHandle handle)
    {
        this.handle = handle;
    }

    [DllImport("libc", SetLastError = true)]
    private static extern int open([MarshalAs(UnmanagedType.LPUTF8Str)] string path, int flags, int mode);

    [DllImport("libc", SetLastError = true)]
    private static extern int flock(int fd, int operation);

    /// <summary>
    /// Takes the lock on <paramref name="path"/> (the file is created when it is missing), waiting up to <paramref name="wait"/> for
    /// a process that holds it. Null when it is still held then, or when <paramref name="cancel"/> was cancelled.
    /// </summary>
    public static DataLock? Acquire(string path, TimeSpan wait, CancellationToken cancel = default)
    {
        if (!OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException("the lock on the data folder is a Linux flock");
        }
        int fd = open(path, OpenReadWrite | OpenCreate | OpenCloseOnExec, Mode);
        if (fd < 0)
        {
            throw new IOException("cannot open " + path + ": error " + Marshal.GetLastPInvokeError());
        }
        SafeFileHandle handle = new(fd, ownsHandle: true);
        try
        {
            DateTime end = DateTime.UtcNow + wait;
            for (; ; )
            {
                if (flock(fd,LockEx | LockNb) == 0)
                {
                    var taken = new DataLock(handle);
                    handle = null!;
                    return taken;
                }
                int errno = Marshal.GetLastPInvokeError();
                if (errno != ErrInterrupted && errno != ErrWouldBlock)
                {
                    throw new IOException("flock on " + path + " failed with error " + errno);
                }
                if (cancel.IsCancellationRequested || DateTime.UtcNow >= end)
                {
                    return null;
                }
                cancel.WaitHandle.WaitOne(PollInterval);
            }
        }
        finally
        {
            handle?.Dispose();
        }
    }

    public void Dispose() => handle.Dispose(); // closing the file drops the lock
}
