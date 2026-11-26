// Not ported from a Crossfire file: the method of lane 7B of wave 7 on TMudPlayer (<c>examineinvobject</c>): its id and its entry in
// the type's member table (docs/mud-phase3.md section 4.5). The code is in Dispatcher.Wave7B.cs.
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
namespace Crossfire.Extension.Gk;

internal static partial class TMudObjectMembers
{
    // lane 7B has no method of TMudObject: the account life and the trade are the player's
    static partial void ExtendWave7B(FieldBuilder<LevelObject> b, List<MethodDef> methods)
    {
    }
}

internal static partial class TMudPlayerMembers
{
    // the lane's methods of TMudPlayer take the ids from the first of its range (Lane.Wave7B.PlayerMembersFirst) in this order
    public const int MethodExamineInvObject = 13500;

    static partial void ExtendWave7B(FieldBuilder<PlayerState> b, List<MethodDef> methods)
    {
        Method7B(b, methods, MethodExamineInvObject, "examineinvobject", new[] { "int" }, "bool");
    }

    private static void Method7B(FieldBuilder<PlayerState> b, List<MethodDef> methods, int id, string name, string[] parameters, string returns)
    {
        int given = b.NextMethodId();
        if (given != id)
        {
            throw new InvalidOperationException("method " + name + " has id " + given + ", not " + id);
        }
        methods.Add(new MethodDef(id, name, parameters, returns));
    }
}
