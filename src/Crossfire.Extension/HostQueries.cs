// Not ported from a Crossfire file: the extension's queries to the host (docs/protocol.md section 4.3 and 12). The worker sends a QUERY
// while it handles a REQ and waits for the QRESULT, which the reader thread hands over; at most one query is outstanding.
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using System.Collections.Concurrent;
using Crossfire.Extension.Protocol;

namespace Crossfire.Extension;

/// <summary>The host's answer to a query: a status (0 is success, section 8) and a result (a string that says what went wrong otherwise).</summary>
internal readonly record struct QueryAnswer(int Status, Val Result)
{
    public bool IsOk => Status == Protocol.Status.Ok;
}

/// <summary>Asks the host a query (section 12) and returns its answer. Worker only.</summary>
internal delegate QueryAnswer HostQuery(string name, IReadOnlyList<Val> args);

/// <summary>The query channel of one connection.</summary>
internal sealed class HostQueries
{
    private readonly FrameWriter writer;
    private readonly CancellationToken stopping;
    private readonly TimeSpan timeout;
    private readonly BlockingCollection<QResultFrame> answers = new();
    private long lastAsked;
    private long waiting;

    public HostQueries(FrameWriter writer, CancellationToken stopping, TimeSpan timeout)
    {
        this.writer = writer;
        this.stopping = stopping;
        this.timeout = timeout;
    }

    /// <summary>Sends the query and waits for its answer; <c>unavailable</c> when the connection is gone, <c>internal</c> when the host does not answer in time.</summary>
    public QueryAnswer Ask(string name, IReadOnlyList<Val> args)
    {
        ulong qid = (ulong)Interlocked.Increment(ref lastAsked);
        Interlocked.Exchange(ref waiting, (long)qid);
        try
        {
            if (!writer.Send(new QueryFrame(qid, name, args)))
            {
                return new QueryAnswer(Status.Unavailable, Val.Str("the connection is gone"));
            }
            while (answers.TryTake(out QResultFrame? answer, (int)timeout.TotalMilliseconds, stopping) && answer != null)
            {
                if (answer.Qid == qid)
                {
                    return new QueryAnswer((int)answer.Status, answer.Result);
                }
            }
            return new QueryAnswer(Status.Internal, Val.Str("the host did not answer the query " + name));
        }
        catch (OperationCanceledException)
        {
            return new QueryAnswer(Status.Unavailable, Val.Str("the connection ended"));
        }
        finally
        {
            Interlocked.Exchange(ref waiting, 0);
        }
    }

    /// <summary>
    /// Called by the reader thread with a QRESULT: false when it answers a query that was never sent (a protocol error). An answer
    /// that comes after its query was given up is dropped.
    /// </summary>
    public bool Deliver(QResultFrame frame)
    {
        if (frame.Qid == 0 || (long)frame.Qid > Interlocked.Read(ref lastAsked))
        {
            return false;
        }
        if ((long)frame.Qid == Interlocked.Read(ref waiting))
        {
            answers.Add(frame);
        }
        return true;
    }
}
