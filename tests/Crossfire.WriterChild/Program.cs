// Not ported from a Crossfire file: the child process of the write-order fault tests (FileWriterMoveTests). It submits one move to a
// real file writer and stops between the two writes, where the test kills it with SIGKILL.
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
//
// Arguments: <data folder> <source path> <source content file> <destination path> <destination content file> <marker file>
// The marker file is created when the source file is on disk and the destination is not yet; the process then waits to be killed.
using Crossfire.Extension;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length != 6)
        {
            return 2;
        }
        var options = new FileWriterOptions
        {
            BetweenMove = (_, _) =>
            {
                File.WriteAllText(args[5], "between");
                Thread.Sleep(Timeout.Infinite);
            },
        };
        var writer = new FileWriter(args[0], options, new Counters(), (_, _) => { });
        writer.SubmitMove(args[1], File.ReadAllBytes(args[2]), args[3], File.ReadAllBytes(args[4]));
        Thread.Sleep(Timeout.Infinite);
        return 0;
    }
}
