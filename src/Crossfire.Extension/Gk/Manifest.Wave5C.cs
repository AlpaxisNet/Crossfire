// Not ported from a Crossfire file: what lane 5C of wave 5 registers in the manifest of the Crossfire extension: the global functions of
// money and examine. The lane owns this file; its ids are the ranges of Lane.Wave5C (docs/extension.md, "Lanes and id ranges"). Its
// methods of TMudObject, TMudPlayer and TMudArch are in TMudObjectMembers.Wave5C.cs, their code in Dispatcher.Wave5C.cs.
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
namespace Crossfire.Extension;

internal static partial class Manifest
{
    /// <summary>The operation of <c>mudaddmoney(n)</c>: the player of the request's context gets or loses money (docs/mud-phase3.md section 4.3).</summary>
    public const int OpMudAddMoney = 6000;

    /// <summary>The operation of <c>mudpay(n)</c> (global form): the player of the context pays; true when he could.</summary>
    public const int OpMudPay = 6001;

    /// <summary>The operation of <c>mudobjectattr(a, b)</c>: an attribute of an item or an archetype, or the text of an amount (<c>"money"</c>).</summary>
    public const int OpMudObjectAttr = 6002;

    /// <summary>The operation of <c>mudexamineinvobject(id)</c>: the player of the context examines an item of his own tree by its id.</summary>
    public const int OpMudExamineInvObject = 6003;

    /// <summary>The functions, types and events of lane 5C; <paramref name="b"/> refuses an id outside <see cref="Lane.Wave5C"/>.</summary>
    private static void RegisterWave5C(ManifestBuilder b)
    {
        // the functions that change money or items are slow: a busy extension answers before anything changes (rule 10 of docs/mud-phase3.md section 3)
        b.AddFunction(Lane.Wave5C, new FunctionDef(OpMudAddMoney, "mudaddmoney", new[] { "int" }, "void", Slow: true, Handler: (d, args) => d.MudAddMoneyOfContext(args)));
        b.AddFunction(Lane.Wave5C, new FunctionDef(OpMudPay, "mudpay", new[] { "int" }, "bool", Slow: true, Handler: (d, args) => d.MudPayOfContext(args)));
        b.AddFunction(Lane.Wave5C, new FunctionDef(OpMudObjectAttr, "mudobjectattr", new[] { "string", "string" }, "string", Handler: (d, args) => d.MudObjectAttr(args)));
        b.AddFunction(Lane.Wave5C, new FunctionDef(OpMudExamineInvObject, "mudexamineinvobject", new[] { "int" }, "bool", Handler: (d, args) => d.MudExamineInvObjectOfContext(args)));
    }
}
