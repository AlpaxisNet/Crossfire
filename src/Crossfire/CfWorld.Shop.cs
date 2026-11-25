// Port to C# of Crossfire's server (commit 8b7ee7f90, 2002-06-27): server/shop.c (prices and the texts of money).
//
//     CrossFire, A Multiplayer game for X-windows
//
//     server/shop.c:
//         Copyright (C) 2000 Mark Wedel
//         Copyright (C) 1992 Frank Tore Johansen
//
//    This program is free software; you can redistribute it and/or modify
//    it under the terms of the GNU General Public License as published by
//    the Free Software Foundation; either version 2 of the License, or
//    (at your option) any later version.
//
//    This program is distributed in the hope that it will be useful,
//    but WITHOUT ANY WARRANTY; without even the implied warranty of
//    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//    GNU General Public License for more details.
//
//    You should have received a copy of the GNU General Public License
//    along with this program; if not, write to the Free Software
//    Foundation, Inc., 675 Mass Ave, Cambridge, MA 02139, USA.
// ---- end of notices ----
using System.Globalization;
using System.Text;

namespace Crossfire;

/// <summary>
/// What an object costs and how money is written: server/shop.c <c>query_cost</c>, <c>query_money</c>, <c>cost_string_from_value</c>
/// and <c>query_cost_string</c>. The payment, the sale and the listing of a shop (maps, containers, the client) are not ported.
/// </summary>
public sealed partial class CfWorld
{
    private const int NumCoins = 3;

    /// <summary>shop.c: <c>coins</c>, the archetype names of the coins, the most valuable first.</summary>
    private static readonly string[] Coins = { "platinacoin", "goldcoin", "silvercoin" };

    /// <summary>
    /// shop.c <c>query_cost</c>: what the object (the whole stack) is worth: to buy it (<see cref="Define.F_BUY"/>), to sell it
    /// (<see cref="Define.F_SELL"/>), or its true value (<see cref="Define.F_TRUE"/>, not adjusted by charisma). Money and gems have
    /// their own rules; an unidentified object is worth less; the magic bonus changes the value; the charisma of
    /// <paramref name="who"/> (a player; the bargaining skill adds to it) changes the price. The spell table that a wand's value
    /// needs is the host's (<see cref="CrossfireHost.SpellCharges"/>). Integer arithmetic wraps like the machine's.
    /// </summary>
    public int QueryCost(CfObject tmp, CfObject? who, int flag)
    {
        unchecked
        {
            int val;
            int number; /* used to better calculate value */
            int charisma;

            if (tmp.Type == Define.MONEY)
            {
                return (int)(tmp.Nrof * (uint)tmp.Value);
            }
            if (tmp.Type == Define.GEM)
            {
                if (flag == Define.F_TRUE)
                {
                    return (int)(tmp.Nrof * (uint)tmp.Value);
                }
                if (flag == Define.F_BUY)
                {
                    return CStr.ToInt(1.03 * tmp.Nrof * tmp.Value);
                }
                if (flag == Define.F_SELL)
                {
                    return CStr.ToInt(0.97 * tmp.Nrof * tmp.Value);
                }
                Log(LogLevel.Error, "Query_cost: Gem type with unknown flag : " + flag.ToString(CultureInfo.InvariantCulture) + "\n");
                return 0;
            }
            number = (int)tmp.Nrof;
            if (number == 0)
            {
                number = 1;
            }
            if (tmp.QueryFlag(Define.FLAG_IDENTIFIED) || NeedIdentify(tmp) == 0)
            {
                if (tmp.QueryFlag(Define.FLAG_CURSED) || tmp.QueryFlag(Define.FLAG_DAMNED))
                {
                    return 0;
                }
                else
                {
                    val = tmp.Value * number;
                }
            }
            /* This area deals with objects that are not identified, but can be */
            else
            {
                if (tmp.Arch != null)
                {
                    if (flag == Define.F_BUY)
                    {
                        Log(LogLevel.Error, "Asking for buy-value of unidentified object.");
                        val = tmp.Arch.Clone.Value * 50 * number;
                    }
                    else
                    {
                        /* Trying to sell something, or get true value */
                        if (tmp.Type == Define.POTION)
                        {
                            val = number * 1000; /* Don't want to give anything away */
                        }
                        else
                        {
                            /* Get 2/3'rd value for applied objects, 1/3'rd for totally
                             * unknown objects
                             */
                            if (tmp.QueryFlag(Define.FLAG_BEEN_APPLIED))
                            {
                                val = number * tmp.Arch.Clone.Value * 2 / 3;
                            }
                            else
                            {
                                val = number * tmp.Arch.Clone.Value / 3;
                            }
                        }
                    }
                }
                else
                {
                    /* No archetype with this object */
                    Log(LogLevel.Debug, "In sell item: Have object with no archetype: " + (tmp.Name ?? "(null)") + "\n");
                    if (flag == Define.F_BUY)
                    {
                        Log(LogLevel.Error, "Asking for buy-value of unidentified object without arch.");
                        val = number * tmp.Value * 10;
                    }
                    else
                    {
                        val = number * tmp.Value / 5;
                    }
                }
            }

            /* If the item has been applied or identifed or does not need to be
             * identified, AND the object is magical and the archetype is non
             * magical, then change values accordingly.  The tmp->arch==NULL is
             * really just a check to prevent core dumps for when it checks
             * tmp->arch->clone.magic for any magic.  The check for archetype
             * magic is to not give extra money for archetypes that are by
             * default magical.  This is because the archetype value should have
             * already figured in that value.
             */
            if ((tmp.QueryFlag(Define.FLAG_IDENTIFIED) || NeedIdentify(tmp) == 0 || tmp.QueryFlag(Define.FLAG_BEEN_APPLIED))
                && tmp.Magic != 0 && (tmp.Arch == null || tmp.Arch.Clone.Magic == 0))
            {
                if (tmp.Magic > 0)
                {
                    val *= 3 * tmp.Magic * tmp.Magic * tmp.Magic;
                }
                else
                {
                    /* Note that tmp->magic is negative, so that this
                     * will actually be something like val /=2, /=3, etc.
                     */
                    val /= 1 - tmp.Magic;
                }
            }
            if (tmp.Type == Define.WAND)
            {
                if (tmp.QueryFlag(Define.FLAG_IDENTIFIED) || NeedIdentify(tmp) == 0)
                {
                    /* Give money based on how many charges the item could have.  Not
                     * really a good method - some spells are certainly better than others.
                     */
                    val = (val * tmp.Stats.Food) / Math.Max(1, Host.SpellCharges(tmp.Stats.Sp));
                }
                else
                {
                    val /= 3;
                }
            }

            /* Limit amount of money you can get for really great items. */
            if (flag == Define.F_TRUE || flag == Define.F_SELL)
            {
                if (val / number > 25000)
                {
                    val = 8000 + Isqrt(val / number) * 20;
                    val *= number;
                }
            }

            /* this modification is for merchant skill
             * now players with readied merchant skill get
             * more Cha up to the limit of modified Cha = 30.
             * -b.t. thomas@nomad.astro.psu.edu
             */
            if (who != null && who.Type == Define.PLAYER)
            {
                float diff;

                charisma = who.Stats.Cha; /* used for SK_BARGAINING modification */

                if (FindSkill(who, Define.SK_BARGAINING) != null)
                {
                    charisma += (who.Level + 2) / 3;
                    if (charisma > 30)
                    {
                        charisma = 30;
                    }
                }
                /* C reads outside cha_bonus for a charisma under 0 or over MAX_STAT: the port takes it as an illegal bonus */
                float bonus = charisma >= 0 && charisma <= Define.MAX_STAT ? Tables.ChaBonus[charisma] : 0.0f;
                if (bonus <= 1.0)
                {
                    Log(LogLevel.Error, "Illegal charisma bonus, " + CStr.ToInt(bonus).ToString(CultureInfo.InvariantCulture) + " <=1.0");
                    diff = 0.9f; /* pretty bad case */
                }
                else
                {
                    /* Diff is now a float between 0 and 1 */
                    diff = (bonus - 1) / (1 + bonus);
                }

                /* we need to multiply these by 4.0 to keep buy costs roughly the same
                 * (otherwise, you could buy a potion of charisma for around 400 pp.
                 * Arguable, the costs in the archetypes should be updated to better
                 * reflect values (potion charisma list for 1250 gold)
                 */
                if (flag == Define.F_BUY)
                {
                    val = CStr.ToInt(4.0 * (float)val * (1.0 + diff));
                }
                else if (flag == Define.F_SELL)
                {
                    val = CStr.ToInt(4.0 * (float)val * (1.0 - diff));
                }
                else
                {
                    val *= 4;
                }
            }

            /* I don't understand this code...., I'm changing it to
             * use 0 instead of 1000000 if we're selling */
            if (val < 0)
            {
                if (flag == Define.F_SELL)
                {
                    val = 0;
                }
                else
                {
                    val = 1000000;
                }
            }

            /* Unidentified stuff won't sell for more than 60gp */
            if (flag == Define.F_SELL && !tmp.QueryFlag(Define.FLAG_IDENTIFIED) && NeedIdentify(tmp) != 0)
            {
                val = val > 600 ? 600 : val;
            }
            return val;
        }
    }

    /// <summary>
    /// shop.c <c>find_next_coin</c> (static): the next coin archetype, from <paramref name="cointype"/> on, whose value is not over
    /// <paramref name="c"/>; null when there is none or when an archetype of the list is missing.
    /// </summary>
    private Archetype? FindNextCoin(int c, ref int cointype)
    {
        Archetype? coin;

        do
        {
            if (cointype >= NumCoins)
            {
                return null;
            }
            coin = FindArchetype(Coins[cointype]);
            if (coin == null)
            {
                return null;
            }
            cointype += 1;
        }
        while (coin.Clone.Value > c);

        return coin;
    }

    private static string CoinText(int num, Archetype coin) =>
        num == 1 ? "1 " + coin.Clone.Name : num.ToString(CultureInfo.InvariantCulture) + " " + coin.Clone.Name + "s";

    /// <summary>
    /// shop.c <c>cost_string_from_value</c>: the money of an amount in the coins of the archetypes platinacoin, goldcoin and
    /// silvercoin: "1 platinum coin, 2 gold coins and 3 silver coins"; "nothing" for an amount of 0 (or when the coin archetypes
    /// are missing).
    /// </summary>
    public string CostStringFromValue(int cost)
    {
        Archetype? coin;
        Archetype? nextCoin;
        int num;
        int cointype = 0;

        coin = FindNextCoin(cost, ref cointype);
        if (coin == null)
        {
            return "nothing";
        }

        num = cost / coin.Clone.Value;
        cost -= num * coin.Clone.Value;
        var buf = new StringBuilder(CoinText(num, coin));

        nextCoin = FindNextCoin(cost, ref cointype);
        if (nextCoin == null)
        {
            return buf.ToString();
        }

        do
        {
            coin = nextCoin;
            num = cost / coin.Clone.Value;
            cost -= num * coin.Clone.Value;

            if (cost == 0)
            {
                nextCoin = null;
            }
            else
            {
                nextCoin = FindNextCoin(cost, ref cointype);
            }

            if (nextCoin != null)
            {
                /* There will be at least one more string to add to the list,
                 * use a comma.
                 */
                buf.Append(", ");
            }
            else
            {
                buf.Append(" and ");
            }
            buf.Append(CoinText(num, coin));
        }
        while (nextCoin != null);

        return buf.ToString();
    }

    /// <summary>shop.c <c>query_cost_string</c>: <see cref="CostStringFromValue"/> of <see cref="QueryCost"/>.</summary>
    public string QueryCostString(CfObject tmp, CfObject? who, int flag) => CostStringFromValue(QueryCost(tmp, who, flag));

    /// <summary>
    /// shop.c <c>query_money</c>: the money a player or container carries: the coins in the inventory and, recursively, in the
    /// containers that are applied and hold gold (no race, or one with "gold" in it). 0 (and an error in the log) for any other object.
    /// </summary>
    public int QueryMoney(CfObject op)
    {
        int total = 0;

        if (op.Type != Define.PLAYER && op.Type != Define.CONTAINER)
        {
            Log(LogLevel.Error, "Query money called with non player/container");
            return 0;
        }
        for (CfObject? tmp = op.Inv; tmp != null; tmp = tmp.Below)
        {
            if (tmp.Type == Define.MONEY)
            {
                total = unchecked(total + (int)(tmp.Nrof * (uint)tmp.Value));
            }
            else if (tmp.Type == Define.CONTAINER && tmp.QueryFlag(Define.FLAG_APPLIED)
                && (tmp.Race == null || tmp.Race.Contains("gold", StringComparison.Ordinal)))
            {
                total = unchecked(total + QueryMoney(tmp));
            }
        }
        return total;
    }
}
