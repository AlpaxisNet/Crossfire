// Not ported from a Crossfire file: the methods of lane 5A of wave 5 on TMudObject: <c>mudpickupobject</c> and <c>mudpickupobject2</c>,
// the ground object picking itself up for <c>ctx.player</c> (docs/mud-phase3.md sections 3 and 4.1; docs/extension.md "Pick up and
// drop"). The dispatcher's code for them is in Dispatcher.Wave5A.cs; the function <c>muddropinvobject</c> is in Manifest.Wave5A.cs.
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
namespace Crossfire.Extension.Gk;

internal static partial class TMudObjectMembers
{
    // the lane's methods of TMudObject take the ids from the first of its range (Lane.Wave5A.ObjectMembersFirst) in this order
    public const int MethodMudPickUpObject = 4000;
    public const int MethodMudPickUpObject2 = 4001;

    static partial void ExtendWave5A(FieldBuilder<LevelObject> b, List<MethodDef> methods)
    {
        Method5A(b, methods, MethodMudPickUpObject, "mudpickupobject");
        Method5A(b, methods, MethodMudPickUpObject2, "mudpickupobject2");
    }

    private static void Method5A(FieldBuilder<LevelObject> b, List<MethodDef> methods, int id, string name)
    {
        int given = b.NextMethodId();
        if (given != id)
        {
            throw new InvalidOperationException("method " + name + " has id " + given + ", not " + id);
        }
        methods.Add(new MethodDef(id, name, new[] { "int" }, "bool"));
    }
}
