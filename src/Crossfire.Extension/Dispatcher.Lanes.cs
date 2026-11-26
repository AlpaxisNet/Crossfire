// Not ported from a Crossfire file: the methods of the lanes of waves 5 to 7 (docs/extension.md, "Methods of the lanes"), their
// setup after session.init, and the apply of an object type by the lane that owns it ("Apply by lane"). Each lane implements its
// partial methods in its own file (Dispatcher.Wave5A.cs and so on) and answers null for a method or type that is not its own.
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using Crossfire.Extension.Protocol;

namespace Crossfire.Extension;

internal sealed partial class Dispatcher
{
    /// <summary>
    /// A method that a lane of waves 5 to 7 declared, run on the target of the handle (a <see cref="LevelObject"/>, an item's proxy, a
    /// <see cref="PlayerState"/> or an <see cref="Archetype"/>); null when no lane has the method, so that the caller
    /// goes on with the methods of the core and of wave 4.
    /// </summary>
    private Outcome? InvokeLaneMethod(object target, long method, IReadOnlyList<Val> args) =>
        InvokeWave5A(target, method, args)
        ?? InvokeWave5B(target, method, args)
        ?? InvokeWave5C(target, method, args)
        ?? InvokeWave5D(target, method, args)
        ?? InvokeWave6A(target, method, args)
        ?? InvokeWave6B(target, method, args)
        ?? InvokeWave6C(target, method, args)
        ?? InvokeWave6D(target, method, args)
        ?? InvokeWave7A(target, method, args)
        ?? InvokeWave7B(target, method, args);

    /// <summary>Lane 5A's methods (Dispatcher.Wave5A.cs); null for a method that is not its own.</summary>
    private partial Outcome? InvokeWave5A(object target, long method, IReadOnlyList<Val> args);

    /// <summary>Lane 5B's methods (Dispatcher.Wave5B.cs); null for a method that is not its own.</summary>
    private partial Outcome? InvokeWave5B(object target, long method, IReadOnlyList<Val> args);

    /// <summary>Lane 5C's methods (Dispatcher.Wave5C.cs); null for a method that is not its own.</summary>
    private partial Outcome? InvokeWave5C(object target, long method, IReadOnlyList<Val> args);

    /// <summary>Lane 5D's methods (Dispatcher.Wave5D.cs); null for a method that is not its own.</summary>
    private partial Outcome? InvokeWave5D(object target, long method, IReadOnlyList<Val> args);

    /// <summary>Lane 6A's methods (Dispatcher.Wave6A.cs); null for a method that is not its own.</summary>
    private partial Outcome? InvokeWave6A(object target, long method, IReadOnlyList<Val> args);

    /// <summary>Lane 6B's methods (Dispatcher.Wave6B.cs); null for a method that is not its own.</summary>
    private partial Outcome? InvokeWave6B(object target, long method, IReadOnlyList<Val> args);

    /// <summary>Lane 6C's methods (Dispatcher.Wave6C.cs); null for a method that is not its own.</summary>
    private partial Outcome? InvokeWave6C(object target, long method, IReadOnlyList<Val> args);

    /// <summary>Lane 6D's methods (Dispatcher.Wave6D.cs); null for a method that is not its own.</summary>
    private partial Outcome? InvokeWave6D(object target, long method, IReadOnlyList<Val> args);

    /// <summary>Lane 7A's methods (Dispatcher.Wave7A.cs); null for a method that is not its own.</summary>
    private partial Outcome? InvokeWave7A(object target, long method, IReadOnlyList<Val> args);

    /// <summary>Lane 7B's methods (Dispatcher.Wave7B.cs); null for a method that is not its own.</summary>
    private partial Outcome? InvokeWave7B(object target, long method, IReadOnlyList<Val> args);

    /// <summary>
    /// The setup of the lanes after the world is loaded at <c>session.init</c> (after the skill table): a lane sets its hooks there
    /// (<see cref="ApplySpecialUnapply"/>, <see cref="PutObjectInSack"/>, <c>gk.Skills.ApplySpecial</c>), each in its own file.
    /// </summary>
    private void InitLanes(CfWorld w)
    {
        InitWave6A(w);
        InitWave6B(w);
        InitWave6C(w);
        InitWave6D(w);
        InitWave7A(w);
        InitWave7B(w);
    }

    /// <summary>Lane 6A's setup after <c>session.init</c> (Dispatcher.Wave6A.cs); nothing when the lane has none.</summary>
    partial void InitWave6A(CfWorld w);

    /// <summary>Lane 6B's setup after <c>session.init</c> (Dispatcher.Wave6B.cs); nothing when the lane has none.</summary>
    partial void InitWave6B(CfWorld w);

    /// <summary>Lane 6C's setup after <c>session.init</c> (Dispatcher.Wave6C.cs); nothing when the lane has none.</summary>
    partial void InitWave6C(CfWorld w);

    /// <summary>Lane 6D's setup after <c>session.init</c> (Dispatcher.Wave6D.cs); nothing when the lane has none.</summary>
    partial void InitWave6D(CfWorld w);

    /// <summary>Lane 7A's setup after <c>session.init</c> (Dispatcher.Wave7A.cs); nothing when the lane has none.</summary>
    partial void InitWave7A(CfWorld w);

    /// <summary>Lane 7B's setup after <c>session.init</c> (Dispatcher.Wave7B.cs); nothing when the lane has none.</summary>
    partial void InitWave7B(CfWorld w);

    /// <summary>
    /// The apply of an object of a type that lane 6A, 6C or 6D owns (docs/extension.md, "Apply by lane"): what C's
    /// <c>manual_apply</c> returns for it (0: the applier cannot apply objects of that type, 1: applied, or an error was told,
    /// 2: objects of that type cannot be applied when not in the inventory), or null when no lane owns the type, so that 6B's
    /// switch goes on. <paramref name="aflag"/> is <c>manual_apply</c>'s (AP_APPLY, AP_UNAPPLY, AP_NOPRINT and so on).
    /// </summary>
    internal int? ApplyByLane(CfObject op, CfObject tmp, int aflag) =>
        ApplyWave6A(op, tmp, aflag)
        ?? ApplyWave6C(op, tmp, aflag)
        ?? ApplyWave6D(op, tmp, aflag);

    /// <summary>Lane 6A's apply of its types (containers, keys; Dispatcher.Wave6A.cs); null for a type that is not its own.</summary>
    private partial int? ApplyWave6A(CfObject op, CfObject tmp, int aflag);

    /// <summary>Lane 6C's apply of its types (food, drink, flesh, potions, poison; Dispatcher.Wave6C.cs); null for a type that is not its own.</summary>
    private partial int? ApplyWave6C(CfObject op, CfObject tmp, int aflag);

    /// <summary>Lane 6D's apply of its types (signs, books, lighters, treasures, the reading of types 161 to 166; Dispatcher.Wave6D.cs); null for a type that is not its own.</summary>
    private partial int? ApplyWave6D(CfObject op, CfObject tmp, int aflag);
}
