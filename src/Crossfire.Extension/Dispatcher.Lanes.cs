// Not ported from a Crossfire file: the methods of wave 5's lanes (docs/extension.md, "Methods of the lanes"). Each lane implements
// its partial method in its own file (Dispatcher.Wave5A.cs to Dispatcher.Wave5D.cs) and answers null for a method that is not its own.
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using Crossfire.Extension.Protocol;

namespace Crossfire.Extension;

internal sealed partial class Dispatcher
{
    /// <summary>
    /// A method that a wave 5 lane declared, run on the target of the handle (a <see cref="LevelObject"/>, an item's proxy, a
    /// <see cref="PlayerState"/> or an <see cref="Archetype"/>); null when no lane has the method, so that the caller
    /// goes on with the methods of the core and of wave 4.
    /// </summary>
    private Outcome? InvokeLaneMethod(object target, long method, IReadOnlyList<Val> args) =>
        InvokeWave5A(target, method, args)
        ?? InvokeWave5B(target, method, args)
        ?? InvokeWave5C(target, method, args)
        ?? InvokeWave5D(target, method, args);

    /// <summary>Lane 5A's methods (Dispatcher.Wave5A.cs); null for a method that is not its own.</summary>
    private partial Outcome? InvokeWave5A(object target, long method, IReadOnlyList<Val> args);

    /// <summary>Lane 5B's methods (Dispatcher.Wave5B.cs); null for a method that is not its own.</summary>
    private partial Outcome? InvokeWave5B(object target, long method, IReadOnlyList<Val> args);

    /// <summary>Lane 5C's methods (Dispatcher.Wave5C.cs); null for a method that is not its own.</summary>
    private partial Outcome? InvokeWave5C(object target, long method, IReadOnlyList<Val> args);

    /// <summary>Lane 5D's methods (Dispatcher.Wave5D.cs); null for a method that is not its own.</summary>
    private partial Outcome? InvokeWave5D(object target, long method, IReadOnlyList<Val> args);
}
