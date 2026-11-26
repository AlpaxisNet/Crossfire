// Port to C# of Crossfire's server (commit 8b7ee7f90, 2002-06-27): the payment and the coin loop of the sale of server/shop.c, for
// the money of a player's inventory (group C of docs/mud-phase3.md).
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

namespace Crossfire.Extension.Gk;

/// <summary>
/// The money of a player: server/shop.c <c>pay_for_amount</c>, <c>pay_from_container</c> and the coin loop of <c>sell_item</c> (here
/// <see cref="AddCoins"/>). The C functions tell the client with <c>esrv_send_item</c> and the like; the host's hooks do that here
/// (<see cref="CfWorld.Host"/>). A coin that is made comes from <c>create</c> (<see cref="Dispatcher.NewItem"/>: identified at
/// creation, docs/mud-phase3.md section 4.3).
/// </summary>
internal static class Money
{
    private const int NumCoins = 3;

    /// <summary>shop.c: <c>coins</c>, the archetype names of the coins, the most valuable first.</summary>
    private static readonly string[] Coins = { "platinacoin", "goldcoin", "silvercoin" };

    /// <summary>True when the three coin archetypes exist with a value above 0 (C divides by the value and dereferences the archetype without looking).</summary>
    public static bool HasCoins(CfWorld w)
    {
        foreach (string name in Coins)
        {
            Archetype? at = w.FindArchetype(name);
            if (at == null || at.Clone.Value <= 0)
            {
                w.Log(LogLevel.Error, "Could not find " + name + " archetype (or its value is not above 0)\n");
                return false;
            }
        }
        return true;
    }

    private static bool HoldsGold(CfObject pouch) =>
        pouch.Type == Define.CONTAINER && pouch.QueryFlag(Define.FLAG_APPLIED) && (pouch.Race == null || pouch.Race.Contains("gold", StringComparison.Ordinal));

    /// <summary>
    /// shop.c <c>pay_for_amount</c>: takes <paramref name="toPay"/> from the player's pack and then from each applied container that
    /// holds gold, with change in coins; false and no change when the player has less (or when a coin archetype is missing, where
    /// C would crash). 0 is paid without a look. A negative amount is false (C does not ask for one). The
    /// <c>FLAG_WAS_WIZ</c> lines of C name a variable the function does not have (they are not compiled) and are left out.
    /// </summary>
    public static bool PayForAmount(CfWorld w, Func<Archetype, CfObject> create, int toPay, CfObject pl)
    {
        if (toPay == 0)
        {
            return true;
        }
        if (toPay < 0 || toPay > w.QueryMoney(pl) || !HasCoins(w))
        {
            return false;
        }

        toPay = PayFromContainer(w, create, pl, toPay);

        for (CfObject? pouch = pl.Inv; pouch != null && toPay > 0; pouch = pouch.Below)
        {
            if (HoldsGold(pouch))
            {
                toPay = PayFromContainer(w, create, pouch, toPay);
            }
        }

        w.Host.FixPlayer(pl);
        return true;
    }

    /// <summary>
    /// shop.c <c>pay_from_container</c>: takes the coins out of a player or a container, pays with the smallest coins first, makes
    /// change downwards, and puts what is left back; returns what is still to pay. The coin archetypes must exist (<see cref="HasCoins"/>).
    /// </summary>
    public static int PayFromContainer(CfWorld w, Func<Archetype, CfObject> create, CfObject pouch, int toPay)
    {
        if (pouch.Type != Define.PLAYER && pouch.Type != Define.CONTAINER)
        {
            return toPay;
        }

        int remain = toPay;
        var coinObjs = new CfObject?[NumCoins];
        CfObject? next;

        /* This hunk should remove all the money objects from the player/container */
        for (CfObject? tmp = pouch.Inv; tmp != null; tmp = next)
        {
            next = tmp.Below;

            if (tmp.Type == Define.MONEY)
            {
                int i;
                for (i = 0; i < NumCoins; i++)
                {
                    if (tmp.Arch != null && Coins[NumCoins - 1 - i] == tmp.Arch.Name && tmp.Value == tmp.Arch.Clone.Value)
                    {
                        /* This should not happen, but if it does, just merge the two. */
                        if (coinObjs[i] != null)
                        {
                            w.Log(LogLevel.Error, (pouch.Name ?? "(null)") + " has two money entries of (" + Coins[NumCoins - 1 - i] + ")\n");
                            w.RemoveOb(tmp);
                            coinObjs[i]!.Nrof += tmp.Nrof;
                            w.Host.EsrvDelItem(pouch.Contr, tmp.Count);
                            w.FreeObject(tmp);
                        }
                        else
                        {
                            w.RemoveOb(tmp);
                            if (pouch.Type == Define.PLAYER)
                            {
                                w.Host.EsrvDelItem(pouch.Contr, tmp.Count);
                            }
                            coinObjs[i] = tmp;
                        }
                        break;
                    }
                }
                if (i == NumCoins)
                {
                    w.Log(LogLevel.Error, "in pay_for_item: Did not find string match for " + (tmp.Arch?.Name ?? "(null)") + "\n");
                }
            }
        }

        /* Fill in any gaps in the coin_objs array - needed to make change. */
        /* Note that the coin_objs array goes from least value to greatest value */
        for (int i = 0; i < NumCoins; i++)
        {
            if (coinObjs[i] == null)
            {
                CfObject coin = create(w.FindArchetype(Coins[NumCoins - 1 - i])!);
                coin.Nrof = 0;
                coinObjs[i] = coin;
            }
        }

        unchecked
        {
            for (int i = 0; i < NumCoins; i++)
            {
                int numCoins;
                CfObject coin = coinObjs[i]!;

                // C: uint32 nrof times sint32 value, compared with remain as an unsigned number
                if (coin.Nrof * (uint)coin.Value > (uint)remain)
                {
                    numCoins = remain / coin.Value;
                    if (numCoins * coin.Value < remain)
                    {
                        numCoins++;
                    }
                }
                else
                {
                    numCoins = (int)coin.Nrof;
                }

                remain -= numCoins * coin.Value;
                coin.Nrof -= (uint)numCoins;
                /* Now start making change.  Start at the coin value below the one we just did, and work down to the lowest value. */
                int count = i - 1;
                while (remain < 0 && count >= 0)
                {
                    numCoins = -remain / coinObjs[count]!.Value;
                    coinObjs[count]!.Nrof += (uint)numCoins;
                    remain += numCoins * coinObjs[count]!.Value;
                    count--;
                }
            }
        }

        for (int i = 0; i < NumCoins; i++)
        {
            if (coinObjs[i]!.Nrof != 0)
            {
                CfObject tmp = w.InsertObInOb(coinObjs[i]!, pouch);
                CfObject? who = pouch;
                while (who != null && who.Type != Define.PLAYER && who.Env != null)
                {
                    who = who.Env;
                }
                w.Host.EsrvSendItem(who!, tmp);
                w.Host.EsrvSendItem(who!, pouch);
                w.Host.EsrvUpdateItem(CfWorld.Define_UPD_WEIGHT, who!, pouch);
                if (pouch.Type != Define.PLAYER)
                {
                    w.Host.EsrvSendItem(who!, who!);
                    w.Host.EsrvUpdateItem(CfWorld.Define_UPD_WEIGHT, who!, who!);
                }
            }
            else
            {
                w.FreeObject(coinObjs[i]!);
            }
        }
        return remain;
    }

    /// <summary>
    /// The coin loop of shop.c <c>sell_item</c> (lines 538 to 570): gives the player <paramref name="amount"/> in coins, platinum first,
    /// then gold and silver. Each kind goes first into the applied containers that hold gold ("gold" in their race) as far as their
    /// weight limit allows, then the rest into the player's pack, with no test of the player's weight. Returns what could not be
    /// given (0: silver has the value 1). The text of C's sale ("You receive ...") and its <c>identify</c> are not part of it.
    /// </summary>
    public static int AddCoins(CfWorld w, Func<Archetype, CfObject> create, CfObject pl, int amount)
    {
        int i = amount;
        CfObject tmp;

        if (i <= 0 || !HasCoins(w))
        {
            return i;
        }
        for (int count = 0; count < NumCoins; count++)
        {
            Archetype at = w.FindArchetype(Coins[count])!;
            if (i / at.Clone.Value > 0)
            {
                for (CfObject? pouch = pl.Inv; pouch != null; pouch = pouch.Below)
                {
                    if (pouch.Type == Define.CONTAINER && pouch.QueryFlag(Define.FLAG_APPLIED) && pouch.Race != null && pouch.Race.Contains("gold", StringComparison.Ordinal))
                    {
                        int wgt = at.Clone.Weight * (100 - pouch.Stats.Str) / 100;
                        int n = i / at.Clone.Value;

                        if (wgt == 0)
                        {
                            wgt = 1; /* Prevent divide by zero */
                        }
                        if (n > 0 && (pouch.WeightLimit == 0 || pouch.Carrying + wgt <= pouch.WeightLimit))
                        {
                            if (pouch.WeightLimit != 0 && (pouch.WeightLimit - pouch.Carrying) / wgt < n)
                            {
                                n = (pouch.WeightLimit - pouch.Carrying) / wgt;
                            }
                            tmp = create(at);
                            tmp.Nrof = (uint)n;
                            i -= n * tmp.Value;
                            tmp = w.InsertObInOb(tmp, pouch);
                            w.Host.EsrvSendItem(pl, tmp);
                            w.Host.EsrvSendItem(pl, pouch);
                            w.Host.EsrvUpdateItem(CfWorld.Define_UPD_WEIGHT, pl, pouch);
                            w.Host.EsrvSendItem(pl, pl);
                            w.Host.EsrvUpdateItem(CfWorld.Define_UPD_WEIGHT, pl, pl);
                        }
                    }
                }
                if (i / at.Clone.Value > 0)
                {
                    tmp = create(at);
                    tmp.Nrof = (uint)(i / tmp.Value);
                    i -= (int)tmp.Nrof * tmp.Value;
                    tmp = w.InsertObInOb(tmp, pl);
                    w.Host.EsrvSendItem(pl, tmp);
                    w.Host.EsrvSendItem(pl, pl);
                    w.Host.EsrvUpdateItem(CfWorld.Define_UPD_WEIGHT, pl, pl);
                }
            }
        }

        if (i != 0)
        {
            w.Log(LogLevel.Error, "Warning - payment not zero: " + i.ToString(CultureInfo.InvariantCulture) + "\n");
        }
        return i;
    }

    /// <summary>
    /// What <c>mudaddmoney(n)</c> does to a player: a positive <paramref name="n"/> gives coins (<see cref="AddCoins"/>); a negative one
    /// takes <c>-n</c> with <see cref="PayForAmount"/> (change in coins); when the player has less than that, everything he carries
    /// of money goes. Graal Kingdoms passes differences, so the sum is exact. Returns the money the player has afterwards.
    /// </summary>
    public static int AddMoney(CfWorld w, Func<Archetype, CfObject> create, CfObject pl, long n)
    {
        if (n > 0)
        {
            AddCoins(w, create, pl, (int)Math.Min(n, int.MaxValue));
        }
        else if (n < 0)
        {
            long have = w.QueryMoney(pl);
            long pay = Math.Min(-n, have);
            PayForAmount(w, create, (int)pay, pl);
        }
        return w.QueryMoney(pl);
    }
}
