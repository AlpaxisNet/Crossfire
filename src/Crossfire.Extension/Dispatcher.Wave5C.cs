// Not ported from a Crossfire file: the functions and methods of lane 5C of wave 5: money (mudaddmoney, mudpay, mudvalue,
// mudobjectattr, mudidentify) and examine (mudexamineinvobject, examine, mudexamineobject). The lane owns this file; its ids are in the
// ranges of Lane.Wave5C (docs/mud-phase3.md sections 4.3 and 4.5; docs/extension.md, "Money and examine").
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using System.Globalization;
using Crossfire.Extension.Gk;
using Crossfire.Extension.Protocol;

namespace Crossfire.Extension;

internal sealed partial class Dispatcher
{
    /// <summary>The lines of text that examine has written (each is one <c>onRPGMessage</c> event on a 1.1 link): counted apart from the events of other kinds (the <c>examine_lines</c> of <c>stats</c>).</summary>
    internal long ExamineLines { get; private set; }

    private partial Outcome? InvokeWave5C(object target, long method, IReadOnlyList<Val> args)
    {
        if (!Lane.Wave5C.OwnsMember(method))
        {
            return null;
        }
        switch (target)
        {
            case LevelObject lo:
                switch (method)
                {
                    case TMudObjectMembers.MethodMudValue:
                        return MudValue(lo.Ob, args);
                    case TMudObjectMembers.MethodMudIdentify:
                        return MudIdentify(lo, args);
                    case TMudObjectMembers.MethodExamine:
                    case TMudObjectMembers.MethodMudExamineObject:
                        return args.Count == 0 ? ContextPlayerOr(ps => ExamineBy(ps, lo.Ob)) : Outcome.Fail(Status.BadArgs, "examine takes no arguments");
                }
                return null;
            case PlayerState player when method == TMudPlayerMembers.MethodMudPay:
                return CheckBusy() ?? MudPay(player, args);
            case Archetype at when method == TMudArchMembers.MethodMudValue:
                return MudValueOfArchetype(at, args);
            default:
                return null;
        }
    }

    // ------------------------------------------------------------------ money

    /// <summary>
    /// Runs a part of the ported C with the errors it writes to the log (<c>LOG(llevError, ...)</c>: a coin that is not one, an
    /// unidentified object that is priced to buy) forwarded to the host's log, the first five of the request, as a level's load does.
    /// </summary>
    private T WithWorldLog<T>(Func<T> run)
    {
        forwardWorldLog = true;
        try
        {
            return run();
        }
        finally
        {
            forwardWorldLog = false;
        }
    }

    /// <summary>The player of the request's context, when he is logged in (the actor of the global functions).</summary>
    private PlayerState? ActorOf() => ctxPlayer != null && players.TryGetValue(ctxPlayer, out PlayerState? ps) ? ps : null;

    /// <summary>The one argument of a call as a whole number: an int or a float (cut), nil as 0; null for any other kind or count.</summary>
    private static long? Number1(IReadOnlyList<Val> args) => args.Count == 1 ? (args[0].IsNil ? 0 : args[0].AsNumberToInt()) : null;

    /// <summary><c>mudaddmoney(n)</c>: <see cref="Money.AddMoney"/> for the player of the context (the function is slow: busy was answered before).</summary>
    internal Outcome MudAddMoneyOfContext(IReadOnlyList<Val> args)
    {
        if (Number1(args) is not { } n)
        {
            return Outcome.Fail(Status.BadArgs, "mudaddmoney takes a number");
        }
        return ContextPlayerOr(ps =>
        {
            WithWorldLog(() => Money.AddMoney(world!, NewItem, ps.Ob, n));
            ps.Dirty = true;
            return Outcome.Ok(Val.Nil);
        });
    }

    /// <summary><c>mudpay(n)</c>, the global form: the same as the player's method for the player of the context.</summary>
    internal Outcome MudPayOfContext(IReadOnlyList<Val> args) => ContextPlayerOr(ps => MudPay(ps, args));

    /// <summary>
    /// <c>mudpay(n)</c> of a player: <c>pay_for_amount</c>; true when he paid, false and no change when he has less (or when
    /// the amount is negative: nothing is paid for a negative price).
    /// </summary>
    private Outcome MudPay(PlayerState ps, IReadOnlyList<Val> args)
    {
        if (Number1(args) is not { } n)
        {
            return Outcome.Fail(Status.BadArgs, "mudpay takes a number");
        }
        bool paid = n >= 0 && WithWorldLog(() => Money.PayForAmount(world!, NewItem, (int)Math.Min(n, int.MaxValue), ps.Ob));
        if (paid && n > 0)
        {
            ps.Dirty = true;
        }
        return Outcome.Ok(Val.Bool(paid));
    }

    /// <summary>The flag of <c>mudvalue(flag)</c>: 0 is <c>F_BUY</c>, 1 is <c>F_SELL</c>; null for any other.</summary>
    private static int? PriceFlag(IReadOnlyList<Val> args) => Number1(args) switch
    {
        0 => Define.F_BUY,
        1 => Define.F_SELL,
        _ => null,
    };

    /// <summary><c>mudvalue(flag)</c> of an object: <c>query_cost</c> of the stack, for the charisma of the player of the context (none: no bargaining).</summary>
    private Outcome MudValue(CfObject ob, IReadOnlyList<Val> args) =>
        PriceFlag(args) is { } flag
            ? Outcome.Ok(Val.Int(WithWorldLog(() => world!.QueryCost(ob, ActorOf()?.Ob, flag))))
            : Outcome.Fail(Status.BadArgs, "mudvalue takes 0 (what a shop asks) or 1 (what it pays)");

    /// <summary>
    /// <c>mudvalue(flag)</c> of an archetype: <c>query_cost</c> of one object made of it, as it would be made for a script
    /// (<see cref="NewItem"/>: identified), whatever the archetype's <c>nrof</c> is.
    /// </summary>
    private Outcome MudValueOfArchetype(Archetype at, IReadOnlyList<Val> args)
    {
        if (PriceFlag(args) is not { } flag)
        {
            return Outcome.Fail(Status.BadArgs, "mudvalue takes 0 (what a shop asks) or 1 (what it pays)");
        }
        CfObject one = NewItem(at);
        one.Nrof = 1;
        int cost = WithWorldLog(() => world!.QueryCost(one, ActorOf()?.Ob, flag));
        world!.FreeObject(one);
        return Outcome.Ok(Val.Int(cost));
    }

    /// <summary><c>mudidentify()</c>: <c>identify</c> of the object; the player's file or the level that holds it is saved afterwards, and the player's window shows the new name.</summary>
    private Outcome MudIdentify(LevelObject lo, IReadOnlyList<Val> args)
    {
        if (args.Count != 0)
        {
            return Outcome.Fail(Status.BadArgs, "mudidentify takes no arguments");
        }
        if (CheckBusy() is { } busy)
        {
            return busy;
        }
        WithWorldLog(() => { world!.Identify(lo.Ob); return 0; });
        AfterItemSet(lo, true);
        return Outcome.Ok(Val.Nil);
    }

    /// <summary>
    /// <c>mudobjectattr(a, b)</c>: <c>mudobjectattr("money", n)</c> is the text of the amount in coins (<c>cost_string_from_value</c>);
    /// <c>mudobjectattr(id, attribute)</c> is an attribute of the item of that id in the player's tree, <c>mudobjectattr(name, attribute)</c>
    /// of the archetype of that name (<see cref="MoneyObjectAttr.Of"/>); "" for an id that names no item of the player, a name that is no
    /// archetype and an unknown attribute.
    /// </summary>
    internal Outcome MudObjectAttr(IReadOnlyList<Val> args)
    {
        if (args.Count != 2 || !Scalar(args[0]) || !Scalar(args[1]))
        {
            return Outcome.Fail(Status.BadArgs, "mudobjectattr takes two texts");
        }
        string first = ScriptText(args[0]);
        string second = ScriptText(args[1]);
        if (string.Equals(first, MoneyObjectAttr.Money, StringComparison.OrdinalIgnoreCase))
        {
            return Outcome.Ok(Val.Str(FileText.FromBytesText(MoneyObjectAttr.MoneyText(world!, MoneyObjectAttr.AmountOf(second)))));
        }
        if (first.Length > 0 && first.All(char.IsAsciiDigit))
        {
            // an id, as the client sent it: only an item of the player's own tree is found
            CfObject? item = ActorOf() is { } ps && long.TryParse(first, NumberStyles.None, CultureInfo.InvariantCulture, out long id) ? FindItemById(ps, id, deep: true) : null;
            return Outcome.Ok(Val.Str(item == null ? "" : MoneyObjectAttr.Of(world!, item, second)));
        }
        if (world!.FindArchetype(FileText.ToBytesText(first)) is not { } at)
        {
            return Outcome.Ok(Val.Str(""));
        }
        // an archetype is read as one object made of it (a script's: NewItem), so that its name is the singular and its quantity 1
        CfObject one = NewItem(at);
        one.Nrof = Math.Max(1u, one.Nrof);
        string text = MoneyObjectAttr.Of(world!, one, second);
        world!.FreeObject(one);
        return Outcome.Ok(Val.Str(text));
    }

    private static bool Scalar(Val v) => v.Kind is ValKind.Nil or ValKind.String or ValKind.Int or ValKind.Float;

    /// <summary>A script value as the text a script would make of it (a number without a fraction as an integer).</summary>
    private static string ScriptText(Val v) => v.Kind switch
    {
        ValKind.Int => v.AsInt.ToString(CultureInfo.InvariantCulture),
        ValKind.Float => double.IsFinite(v.AsFloat) && v.AsFloat == Math.Truncate(v.AsFloat) && Math.Abs(v.AsFloat) < 1e15
            ? ((long)v.AsFloat).ToString(CultureInfo.InvariantCulture)
            : v.AsFloat.ToString("R", CultureInfo.InvariantCulture),
        _ => v.AsString,
    };

    // ------------------------------------------------------------------ examine

    /// <summary><c>mudexamineinvobject(id)</c>: the player of the context examines the item of that id (his own tree only); true when he did.</summary>
    internal Outcome MudExamineInvObjectOfContext(IReadOnlyList<Val> args)
    {
        if (Number1(args) is not { } id)
        {
            return Outcome.Fail(Status.BadArgs, "mudexamineinvobject takes an id");
        }
        return ContextPlayerOr(ps =>
        {
            if (FindItemById(ps, id, deep: true) is not { } item)
            {
                return Outcome.Ok(Val.False);
            }
            ExamineBy(ps, item);
            return Outcome.Ok(Val.True);
        });
    }

    /// <summary>c_object.c <c>examine(op, tmp)</c> for the player <paramref name="ps"/>; its lines are counted in <see cref="ExamineLines"/>.</summary>
    private Outcome ExamineBy(PlayerState ps, CfObject item)
    {
        int before = events.Count + effects.Count;
        WithWorldLog(() => { Examine.ExamineObject(world!, ps.Ob, item); return 0; });
        ExamineLines += events.Count + effects.Count - before;
        return Outcome.Ok(Val.Nil);
    }
}
