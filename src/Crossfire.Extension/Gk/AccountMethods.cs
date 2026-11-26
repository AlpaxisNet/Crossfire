// Not ported from a Crossfire file: the methods of TMudPlayer that lane 4D (the start path) declares: the ids, and their entry in the
// type's member table (docs/mud-phase3.md section 4.7). The dispatcher's code for them is in Dispatcher.Start.cs.
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
namespace Crossfire.Extension.Gk;

internal static partial class TMudPlayerMembers
{
    // the lane's methods of TMudPlayer take the ids from the first of its range (Lane.Start.PlayerMembersFirst) in this order
    public const int MethodMudResetPlayer = 2500;
    public const int MethodRollStats = 2501;
    public const int MethodSetMudClass = 2502;
    public const int MethodSetMudClassNoAction = 2503;
    public const int MethodLoadMudAccount = 2504;
    public const int MethodUnloadMudAccount = 2505;
    public const int MethodSaveMudAccount = 2506;
    public const int MethodBackupMudAccount = 2507;
    public const int MethodMudLoadAccount = 2508;

    static partial void ExtendStart(FieldBuilder<PlayerState> b, List<MethodDef> methods)
    {
        Method(b, methods, MethodMudResetPlayer, "mudresetplayer", Array.Empty<string>(), "void");
        Method(b, methods, MethodRollStats, "rollstats", Array.Empty<string>(), "void");
        Method(b, methods, MethodSetMudClass, "setmudclass", new[] { "string" }, "bool");
        Method(b, methods, MethodSetMudClassNoAction, "setmudclassnoaction", new[] { "string" }, "void");
        Method(b, methods, MethodLoadMudAccount, "loadmudaccount", new[] { "string" }, "bool");
        Method(b, methods, MethodUnloadMudAccount, "unloadmudaccount", Array.Empty<string>(), "void");
        Method(b, methods, MethodSaveMudAccount, "savemudaccount", Array.Empty<string>(), "void");
        Method(b, methods, MethodBackupMudAccount, "backupmudaccount", Array.Empty<string>(), "void");
        Method(b, methods, MethodMudLoadAccount, "Mud_LoadAccount", new[] { "string" }, "bool");
    }

    private static void Method(FieldBuilder<PlayerState> b, List<MethodDef> methods, int id, string name, string[] parameters, string returns)
    {
        int given = b.NextMethodId();
        if (given != id)
        {
            throw new InvalidOperationException("method " + name + " has id " + given + ", not " + id);
        }
        methods.Add(new MethodDef(id, name, parameters, returns));
    }
}
