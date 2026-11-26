// Not ported from a Crossfire file: <c>mudobjectattr</c> of Graal Kingdoms' scripts, the one function that tells a script a few facts of an
// item or an archetype and writes an amount of money as text (docs/mud-phase3.md section 4.3). It reads Crossfire's fields and calls
// <c>cost_string_from_value</c> of server/shop.c (CfWorld.CostStringFromValue).
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using System.Globalization;

namespace Crossfire.Extension.Gk;

/// <summary>The attributes <c>mudobjectattr</c> gives; the choices are in docs/extension.md ("Money and examine").</summary>
internal static class MoneyObjectAttr
{
    /// <summary>The attribute whose first argument is not an object but an amount (<c>mudobjectattr("money", 150)</c>).</summary>
    public const string Money = "money";

    /// <summary>The text of an amount in coins (<c>cost_string_from_value</c>): "1 platinum coin, 2 gold coins and 3 silver coins"; "nothing" for an amount that is not above 0.</summary>
    public static string MoneyText(CfWorld w, double amount) => w.CostStringFromValue(CStr.ToInt(amount));

    /// <summary>
    /// The number a script's text stands for (a script number reaches a <c>string</c> parameter as its text): digits with an optional
    /// sign and fraction, the fraction cut off; 0 for any other text.
    /// </summary>
    public static double AmountOf(string text) =>
        double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double d) && double.IsFinite(d) ? Math.Truncate(d) : 0;

    /// <summary>
    /// One attribute of an object (a tagged item, or the clone of an archetype): <c>archname</c> (the archetype's name), <c>name</c> (the
    /// name the player's window shows: <c>query_short_name</c>), <c>icon</c> (the picture, as the member <c>icon</c>), <c>food</c>, <c>type</c>,
    /// <c>quantity</c> (<c>nrof</c>), <c>weight</c> (of one, in grams) and <c>value</c> (of one); "" for any other attribute.
    /// </summary>
    public static string Of(CfWorld w, CfObject ob, string attribute)
    {
        string Number(long n) => n.ToString(CultureInfo.InvariantCulture);

        switch (attribute.ToLowerInvariant())
        {
            case "archname":
                return ob.Arch?.Name ?? "";
            case "name":
                return FileText.FromBytesText(w.QueryShortName(ob));
            case "icon":
                return TMudArchMembers.Icon(ob.Face?.Name);
            case "food":
                return Number(ob.Stats.Food);
            case "type":
                return Number(ob.Type);
            case "quantity":
                return Number(ob.Nrof);
            case "weight":
                return Number(ob.Weight);
            case "value":
                return Number(ob.Value);
            default:
                return "";
        }
    }
}
