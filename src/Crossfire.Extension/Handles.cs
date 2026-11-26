// Not ported from a Crossfire file: the handles of the Crossfire extension (docs/protocol.md section 5).
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
namespace Crossfire.Extension;

/// <summary>The tags of this epoch are used up (protocol section 5): the request that needed a new tag answers <c>internal</c>.</summary>
internal sealed class TagsUsedUpException : Exception
{
}

/// <summary>
/// The tags of one epoch: one per archetype, level object and logged-in player, given out once and never again. A handle is the
/// epoch in its high 32 bits and the tag in its low 32 bits. An object that is destroyed, unloaded or logged out gives its tag up
/// for good: the tag names nothing afterwards, and is never given to another object. Worker only.
/// </summary>
internal sealed class HandleTable
{
    private readonly long epoch;
    private readonly uint limit;
    private readonly List<object?> slots = new();
    private readonly Dictionary<object, uint> tags = new(ReferenceEqualityComparer.Instance);

    public HandleTable(long epoch, uint limit)
    {
        this.epoch = epoch;
        this.limit = limit;
    }

    /// <summary>How many tags were given out (dead ones included).</summary>
    public int Issued => slots.Count;

    /// <summary>The tag of an object, given out at the first call.</summary>
    /// <exception cref="TagsUsedUpException">The tags of this epoch are used up.</exception>
    public uint TagOf(object target)
    {
        if (!tags.TryGetValue(target, out uint tag))
        {
            if ((uint)slots.Count >= limit)
            {
                throw new TagsUsedUpException();
            }
            slots.Add(target);
            tag = (uint)slots.Count;
            tags[target] = tag;
        }
        return tag;
    }

    /// <summary>The handle of an object (its tag is given out at the first call).</summary>
    public ulong HandleOf(object target) => ((ulong)epoch << 32) | TagOf(target);

    /// <summary>The object of a handle, or null for a handle of another epoch, with tag 0, never given out, or dead.</summary>
    public object? Resolve(ulong handle)
    {
        uint tag = (uint)handle;
        if ((handle >> 32) != (ulong)epoch || tag == 0 || tag > slots.Count)
        {
            return null;
        }
        return slots[(int)tag - 1];
    }

    /// <summary>The tag stops naming the object for good (it is never given to another object).</summary>
    public void Kill(object target)
    {
        if (tags.Remove(target, out uint tag))
        {
            slots[(int)tag - 1] = null;
        }
    }

    /// <summary>True when the object has a live tag.</summary>
    public bool Has(object target) => tags.ContainsKey(target);
}
