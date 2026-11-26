// Not ported from a Crossfire file: the methods of lane 5C of wave 5 on the types TMudObject (an item, an object of a level),
// TMudPlayer and TMudArch (docs/mud-phase3.md sections 4.1, 4.3 and 4.5). Their code is in Dispatcher.Wave5C.cs. The lane adds no member.
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
namespace Crossfire.Extension.Gk;

internal static partial class TMudObjectMembers
{
    /// <summary><c>mudvalue(flag)</c>: <c>query_cost</c> of the object (the whole stack): 0 is what a shop asks (<c>F_BUY</c>), 1 what it pays (<c>F_SELL</c>).</summary>
    public const int MethodMudValue = 6000;

    /// <summary><c>mudidentify()</c>: <c>identify</c> of the object.</summary>
    public const int MethodMudIdentify = 6001;

    /// <summary><c>examine()</c>: the ctx player examines the object.</summary>
    public const int MethodExamine = 6002;

    /// <summary><c>mudexamineobject()</c>: the same as <c>examine</c>.</summary>
    public const int MethodMudExamineObject = 6003;

    static partial void ExtendWave5C(FieldBuilder<LevelObject> b, List<MethodDef> methods)
    {
        Wave5CMethods.Add(b, methods, MethodMudValue, "mudvalue", new[] { "int" }, "int");
        Wave5CMethods.Add(b, methods, MethodMudIdentify, "mudidentify", Array.Empty<string>(), "void");
        Wave5CMethods.Add(b, methods, MethodExamine, "examine", Array.Empty<string>(), "void");
        Wave5CMethods.Add(b, methods, MethodMudExamineObject, "mudexamineobject", Array.Empty<string>(), "void");
    }
}

internal static partial class TMudPlayerMembers
{
    /// <summary><c>mudpay(n)</c> (the player's form; the function of the same name is the global form): <c>pay_for_amount</c>, true when he could pay.</summary>
    public const int MethodMudPay = 6500;

    static partial void ExtendWave5C(FieldBuilder<PlayerState> b, List<MethodDef> methods)
    {
        Wave5CMethods.Add(b, methods, MethodMudPay, "mudpay", new[] { "int" }, "bool");
    }
}

internal static partial class TMudArchMembers
{
    /// <summary><c>mudvalue(flag)</c> of an archetype: <c>query_cost</c> of one object made of it (the first id of the lane's own-type range).</summary>
    public const int MethodMudValue = 6750;

    static partial void ExtendMethods(List<MethodDef> methods) =>
        methods.Add(new MethodDef(MethodMudValue, "mudvalue", new[] { "int" }, "int"));
}

internal static class Wave5CMethods
{
    /// <summary>Adds a method with the id the lane's code expects; a different next id is a programming fault.</summary>
    public static void Add<T>(FieldBuilder<T> b, List<MethodDef> methods, int expected, string name, string[] parameters, string ret)
    {
        int id = b.NextMethodId();
        if (id != expected)
        {
            throw new InvalidOperationException("the method " + name + " of lane 5C got the id " + id + " instead of " + expected);
        }
        methods.Add(new MethodDef(id, name, parameters, ret));
    }
}
