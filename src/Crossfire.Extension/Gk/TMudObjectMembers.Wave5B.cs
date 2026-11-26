// Not ported from a Crossfire file: the methods of lane 5B on the types TMudObject (the inventory subset for containers, and
// muddecreasequantity) and TMudPlayer (the inventory methods): their ids and their entry in the types' member tables
// (docs/mud-phase3.md sections 4.1 and 4.2). The dispatcher's code for them is in Dispatcher.Wave5B.cs.
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using Crossfire.Extension.Protocol;

namespace Crossfire.Extension.Gk;

internal static partial class TMudObjectMembers
{
    // the lane's methods of TMudObject take the ids from the first of its range (Lane.Wave5B.ObjectMembersFirst) in this order
    public const int MethodFindInvObject = 5000;
    public const int MethodFindInvObjectById = 5001;
    public const int MethodFindInvObjectByTitle = 5002;
    public const int MethodCountInvObject = 5003;
    public const int MethodMudDecreaseQuantity = 5004;

    static partial void ExtendWave5B(FieldBuilder<LevelObject> b, List<MethodDef> methods)
    {
        string item = "handle:" + TypeName;
        Method5B(b, methods, MethodFindInvObject, "findinvobject", new[] { "string", "bool" }, item);
        Method5B(b, methods, MethodFindInvObjectById, "findinvobjectbyid", new[] { "int", "bool" }, item);
        Method5B(b, methods, MethodFindInvObjectByTitle, "findinvobjectbytitle", new[] { "string", "bool" }, item);
        Method5B(b, methods, MethodCountInvObject, "countinvobject", new[] { "string", "bool" }, "int");
        Method5B(b, methods, MethodMudDecreaseQuantity, "muddecreasequantity", new[] { "int" }, "void");
    }

    private static void Method5B(FieldBuilder<LevelObject> b, List<MethodDef> methods, int id, string name, string[] parameters, string returns)
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
    // the lane's methods of TMudPlayer take the ids from the first of its range (Lane.Wave5B.PlayerMembersFirst) in this order
    public const int MethodAddInvObject = 5500;
    public const int MethodAddInvObject2 = 5501;
    public const int MethodRemoveInvObject = 5502;
    public const int MethodRemoveInvObjectById = 5503;
    public const int MethodRemoveInvObjects = 5504;
    public const int MethodFindInvObject = 5505;
    public const int MethodFindInvObjectById = 5506;
    public const int MethodFindInvObjectByTitle = 5507;
    public const int MethodCountInvObject = 5508;

    static partial void ExtendWave5B(FieldBuilder<PlayerState> b, List<MethodDef> methods)
    {
        string item = "handle:" + TMudObjectMembers.TypeName;
        Method5B(b, methods, MethodAddInvObject, "addinvobject", new[] { "int", "string" }, item);
        Method5B(b, methods, MethodAddInvObject2, "addinvobject2", new[] { "int", "string" }, item);
        Method5B(b, methods, MethodRemoveInvObject, "removeinvobject", new[] { "int", "string" }, "bool");
        Method5B(b, methods, MethodRemoveInvObjectById, "removeinvobjectbyid", new[] { "int", "int" }, "bool");
        Method5B(b, methods, MethodRemoveInvObjects, "removeinvobjects", new[] { "string" }, "int");
        Method5B(b, methods, MethodFindInvObject, "findinvobject", new[] { "string", "bool" }, item);
        Method5B(b, methods, MethodFindInvObjectById, "findinvobjectbyid", new[] { "int", "bool" }, item);
        Method5B(b, methods, MethodFindInvObjectByTitle, "findinvobjectbytitle", new[] { "string", "bool" }, item);
        Method5B(b, methods, MethodCountInvObject, "countinvobject", new[] { "string", "bool" }, "int");
    }

    private static void Method5B(FieldBuilder<PlayerState> b, List<MethodDef> methods, int id, string name, string[] parameters, string returns)
    {
        int given = b.NextMethodId();
        if (given != id)
        {
            throw new InvalidOperationException("method " + name + " has id " + given + ", not " + id);
        }
        methods.Add(new MethodDef(id, name, parameters, returns));
    }
}
