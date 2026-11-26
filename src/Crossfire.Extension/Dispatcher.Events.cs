// Not ported from a Crossfire file: the events a part of the Crossfire extension emits (docs/protocol.md section 11 and, for items and
// fallbacks, section 11.1).
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using Crossfire.Extension.Protocol;

namespace Crossfire.Extension;

/// <summary>What <see cref="Dispatcher"/>'s event methods did with an event.</summary>
internal enum EmitResult
{
    /// <summary>The event is not sent: it needs a link of 1.1 and this one is 1.0 (nothing is added to the REPLY).</summary>
    NotSent,

    /// <summary>The event is in the REPLY, with its fallback if it had one.</summary>
    Sent,

    /// <summary>The event is in the REPLY without its fallback: the key <c>fallback</c> is 1.1's and the link is 1.0, so the host will skip an event whose function is missing and nothing replaces it.</summary>
    SentWithoutFallback,
}

internal sealed partial class Dispatcher
{
    /// <summary>
    /// Emits an event of target <c>item</c> (protocol 11.1): it runs on the proxy of <paramref name="target"/>, an object that has a
    /// handle (an inventory item, a container: whatever <see cref="HandleTable"/> keys), of the manifest type
    /// <paramref name="type"/>, joined to the script classes <paramref name="classes"/>, with <paramref name="args"/>. The event must
    /// have been registered with target <c>item</c> (<see cref="ManifestBuilder.AddEvent"/>). Only on a link of 1.1: on 1.0 nothing is
    /// added and the result is <see cref="EmitResult.NotSent"/>. <paramref name="fallback"/> is an effect map or an invoke map
    /// (<see cref="TextFallback"/>, <see cref="InvokeFallback"/>): the host runs it when no class of the proxy defines the function.
    /// </summary>
    public EmitResult EmitItemEvent(string name, object target, string type, IReadOnlyList<string> classes, IReadOnlyList<Val> args, Val? fallback = null)
    {
        EventDef def = DeclaredEvent(name, "item");
        if (!Speaks11 || def.MinMinor > LinkMinor)
        {
            return EmitResult.NotSent;
        }
        if (!manifest.TypeNames.Contains(type))
        {
            throw new InvalidOperationException("the item event " + name + " names the type " + type + ", which is not in the manifest");
        }
        var pairs = new List<(string, Val)>
        {
            ("name", Val.Str(name)),
            ("target", Val.Str("item")),
            ("handle", Val.Handle(handles.HandleOf(target))),
            ("type", Val.Str(type)),
            ("classes", Val.List(classes.Select(Val.Str))),
            ("args", Val.List(args)),
        };
        if (fallback is { } fb)
        {
            pairs.Add(("fallback", fb));
        }
        events.Add(Val.Map(pairs.ToArray()));
        return EmitResult.Sent;
    }

    /// <summary>
    /// Emits an event of target <c>player</c> for an account that is logged in (protocol 11), with the optional 1.1 <paramref name="fallback"/>
    /// (11.1: run when the player's classes define no function of that name). On a link of 1.0 the event is sent without the
    /// fallback (<see cref="EmitResult.SentWithoutFallback"/>), as a 1.0 host does not know the key.
    /// </summary>
    public EmitResult EmitPlayerEvent(string name, string account, IReadOnlyList<Val> args, Val? fallback = null)
    {
        DeclaredEvent(name, "player");
        var pairs = new List<(string, Val)>
        {
            ("name", Val.Str(name)),
            ("target", Val.Str("player")),
            ("player", Val.Str(account)),
            ("args", Val.List(args)),
        };
        EmitResult result = EmitResult.Sent;
        if (fallback is { } fb)
        {
            if (Speaks11)
            {
                pairs.Add(("fallback", fb));
            }
            else
            {
                result = EmitResult.SentWithoutFallback;
            }
        }
        events.Add(Val.Map(pairs.ToArray()));
        return result;
    }

    /// <summary>
    /// Emits an event of target <c>object</c> (protocol 11) for an object a level NPC shows, with the optional 1.1 <paramref name="fallback"/>;
    /// as for <see cref="EmitPlayerEvent"/>, the fallback is left out on a link of 1.0.
    /// </summary>
    public EmitResult EmitObjectEvent(string name, object target, IReadOnlyList<Val> args, Val? fallback = null)
    {
        DeclaredEvent(name, "object");
        var pairs = new List<(string, Val)>
        {
            ("name", Val.Str(name)),
            ("target", Val.Str("object")),
            ("handle", Val.Handle(handles.HandleOf(target))),
            ("args", Val.List(args)),
        };
        EmitResult result = EmitResult.Sent;
        if (fallback is { } fb)
        {
            if (Speaks11)
            {
                pairs.Add(("fallback", fb));
            }
            else
            {
                result = EmitResult.SentWithoutFallback;
            }
        }
        events.Add(Val.Map(pairs.ToArray()));
        return result;
    }

    /// <summary>The fallback that shows a text to a player: the effect <c>client.text</c> (protocol 10, 11.1).</summary>
    public static Val TextFallback(string account, string text) =>
        Val.Map(("k", Val.Str("client.text")), ("player", Val.Str(account)), ("text", Val.Str(text)));

    /// <summary>The fallback that calls a method of the event's type on <paramref name="target"/> (protocol 11.1: an invoke map).</summary>
    public Val InvokeFallback(string method, object target, IReadOnlyList<Val> args) =>
        Val.Map(("invoke", Val.Str(method)), ("handle", Val.Handle(handles.HandleOf(target))), ("args", Val.List(args)));

    private EventDef DeclaredEvent(string name, string target)
    {
        EventDef? def = manifest.FindEvent(name, target);
        if (def == null)
        {
            throw new InvalidOperationException("the event " + name + " is not registered with the target " + target);
        }
        return def;
    }
}
