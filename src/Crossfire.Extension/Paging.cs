// Not ported from a Crossfire file: the paging helpers of the Crossfire extension (docs/protocol.md op 7, and the limit of a frame in
// sections 2 and 4.2): the pages of <c>level.load</c> and the splitting of a reply's lists.
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using Crossfire.Extension.Protocol;

namespace Crossfire.Extension;

/// <summary>One page: the items of this call, whether more follow, and the cursor for the next call (nil when none).</summary>
internal sealed record Page<T>(IReadOnlyList<T> Items, bool More, Val Cursor)
{
    /// <summary>The result of <c>level.load</c>: <c>{more, cursor}</c>.</summary>
    public Val Result() => Val.Map(("more", More ? Val.True : Val.False), ("cursor", Cursor));
}

/// <summary>The host sent a cursor this extension did not give out.</summary>
internal sealed class BadCursorException : Exception
{
    public BadCursorException(string message) : base(message)
    {
    }
}

/// <summary>One item is larger than a whole reply may be.</summary>
internal sealed class ItemTooLargeException : Exception
{
    public ItemTooLargeException(string message) : base(message)
    {
    }
}

internal static class Paging
{
    /// <summary>Most <c>npc.place</c> effects of one <c>level.load</c> call (op 7).</summary>
    public const int MaxPerLoadCall = 64;

    /// <summary>The largest cursor the host accepts, encoded (op 7).</summary>
    public const int MaxCursorBytes = 1024;

    /// <summary>The bytes a value takes in a frame.</summary>
    public static long EncodedSize(Val v) => ValueCodec.Encode(v).Length;

    /// <summary>
    /// The page of <paramref name="items"/> that the cursor asks for. The cursor is nil for the first call, then the one this method
    /// returned: the index of the first item of the page (an int; the list must keep its order and its items between the calls).
    /// A page holds at most <paramref name="perPage"/> items (at most 64) and, with <paramref name="sizeOf"/>, as many as fit in
    /// <paramref name="budget"/> bytes; it always holds one item when one is left and fits alone.
    /// </summary>
    /// <exception cref="BadCursorException">The cursor is not nil or an int in 0..count.</exception>
    /// <exception cref="ItemTooLargeException">The next item alone is over the budget.</exception>
    public static Page<T> LevelPage<T>(IReadOnlyList<T> items, Val cursor, int perPage = MaxPerLoadCall, Func<T, long>? sizeOf = null, long budget = long.MaxValue)
    {
        if (perPage < 1 || perPage > MaxPerLoadCall)
        {
            throw new ArgumentOutOfRangeException(nameof(perPage), "a page holds 1 to " + MaxPerLoadCall + " items");
        }
        long start = 0;
        if (!cursor.IsNil)
        {
            if (cursor.Kind != ValKind.Int || cursor.AsInt < 0 || cursor.AsInt > items.Count)
            {
                throw new BadCursorException("the cursor is not one this extension gave out");
            }
            start = cursor.AsInt;
        }
        var taken = new List<T>(Math.Min(perPage, items.Count - (int)start));
        long used = 0;
        for (int i = (int)start; i < items.Count && taken.Count < perPage; i++)
        {
            if (sizeOf != null)
            {
                long size = sizeOf(items[i]);
                if (used + size > budget)
                {
                    if (taken.Count == 0)
                    {
                        throw new ItemTooLargeException("item " + i + " is " + size + " bytes, the budget " + budget);
                    }
                    break;
                }
                used += size;
            }
            taken.Add(items[i]);
        }
        int next = (int)start + taken.Count;
        bool more = next < items.Count;
        Val nextCursor = more ? Val.Int(next) : Val.Nil;
        return new Page<T>(taken, more, nextCursor);
    }

    /// <summary>True when a cursor of any kind encodes to the 1,024 bytes that op 7 allows or fewer (for a cursor a later version builds itself).</summary>
    public static bool CursorFits(Val cursor) => EncodedSize(cursor) <= MaxCursorBytes;

    /// <summary>
    /// The bytes that the lists of a REPLY to <paramref name="result"/> may hold together so that the frame stays within
    /// <paramref name="maxFrame"/>: the limit less the frame as it is with empty lists (largest request id and status) and the
    /// growth of the two counts. <paramref name="reserve"/> is room for what is not known yet (a cursor: <see cref="MaxCursorBytes"/>).
    /// </summary>
    public static long ReplyBudget(int maxFrame, Val result, long reserve = 0)
    {
        var skeleton = new ReplyFrame(ulong.MaxValue, ulong.MaxValue, result, Array.Empty<Val>(), Array.Empty<Val>());
        const int CountGrowth = 2 * 9; // a count is a varint of 1 to 10 bytes, 1 of them is in the skeleton
        return maxFrame - (FrameCodec.Encode(skeleton).Length - 4) - CountGrowth - reserve;
    }

    /// <summary>
    /// Splits <paramref name="items"/> into consecutive parts of at most <paramref name="budget"/> bytes each (by
    /// <paramref name="sizeOf"/>), so that a reply carries one part and the next call the next. A part is never empty.
    /// </summary>
    /// <exception cref="ItemTooLargeException">An item alone is over the budget.</exception>
    public static List<List<T>> Split<T>(IReadOnlyList<T> items, Func<T, long> sizeOf, long budget)
    {
        var parts = new List<List<T>>();
        var current = new List<T>();
        long used = 0;
        for (int i = 0; i < items.Count; i++)
        {
            long size = sizeOf(items[i]);
            if (size > budget)
            {
                throw new ItemTooLargeException("item " + i + " is " + size + " bytes, the budget " + budget);
            }
            if (used + size > budget)
            {
                parts.Add(current);
                current = new List<T>();
                used = 0;
            }
            current.Add(items[i]);
            used += size;
        }
        if (current.Count > 0)
        {
            parts.Add(current);
        }
        return parts;
    }

    /// <summary>The same split for lists of values, by their encoded size.</summary>
    public static List<List<Val>> Split(IReadOnlyList<Val> items, long budget) => Split(items, EncodedSize, budget);
}
