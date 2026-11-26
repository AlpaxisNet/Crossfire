// Not ported from a Crossfire file: the methods of lane 6B of wave 6 (the apply core and wearing) on TMudObject (<c>apply</c>,
// <c>mudapplyobject</c>, <c>defaultApply</c>) and on TMudPlayer (<c>applyinvobject</c>, <c>getwieldedobject</c>): their ids and their
// entry in the types' member tables (docs/mud-phase3.md section 4.4). The code is in Dispatcher.Wave6B.cs.
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
namespace Crossfire.Extension.Gk;

internal static partial class TMudObjectMembers
{
    // the lane's methods of TMudObject take the ids from the first of its range (Lane.Wave6B.ObjectMembersFirst) in this order
    public const int MethodApply = 9000;
    public const int MethodMudApplyObject = 9001;
    public const int MethodDefaultApply = 9002;

    static partial void ExtendWave6B(FieldBuilder<LevelObject> b, List<MethodDef> methods)
    {
        // apply() and mudapplyobject() apply the receiver for ctx.player; defaultApply(pl) is what an event's fallback invokes
        Method6B(b, methods, MethodApply, "apply", Array.Empty<string>(), "bool");
        Method6B(b, methods, MethodMudApplyObject, "mudapplyobject", Array.Empty<string>(), "bool");
        Method6B(b, methods, MethodDefaultApply, "defaultApply", new[] { "handle:" + TMudPlayerMembers.TypeName }, "int");
    }

    private static void Method6B(FieldBuilder<LevelObject> b, List<MethodDef> methods, int id, string name, string[] parameters, string returns)
    {
        int given = b.NextMethodId();
        if (given != id)
        {
            throw new InvalidOperationException("method " + name + " has id " + given + ", not " + id);
        }
        methods.Add(new MethodDef(id, name, parameters, returns));
    }
}

internal static partial class TMudPlayerMembers
{
    // the lane's methods of TMudPlayer take the ids from the first of its range (Lane.Wave6B.PlayerMembersFirst) in this order
    public const int MethodApplyInvObject = 9500;
    public const int MethodGetWieldedObject = 9501;

    static partial void ExtendWave6B(FieldBuilder<PlayerState> b, List<MethodDef> methods)
    {
        Method6B(b, methods, MethodApplyInvObject, "applyinvobject", new[] { "int" }, "bool");
        Method6B(b, methods, MethodGetWieldedObject, "getwieldedobject", Array.Empty<string>(), "handle:" + TMudObjectMembers.TypeName);
    }

    private static void Method6B(FieldBuilder<PlayerState> b, List<MethodDef> methods, int id, string name, string[] parameters, string returns)
    {
        int given = b.NextMethodId();
        if (given != id)
        {
            throw new InvalidOperationException("method " + name + " has id " + given + ", not " + id);
        }
        methods.Add(new MethodDef(id, name, parameters, returns));
    }
}
