// Not ported from a Crossfire file: tests of lane 5C of phase 3 wave 5, money in the Crossfire extension: mudaddmoney, mudpay (function and
// method), mudvalue (item and archetype), mudobjectattr and mudidentify, with the identified-at-creation rule and its golden prices
// (docs/mud-phase3.md sections 3 and 4.3, 9).
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using Crossfire.Extension.Gk;
using Crossfire.Extension.Protocol;
using static Crossfire.Extension.Tests.Say;

namespace Crossfire.Extension.Tests;

/// <summary>An invented world for money: the three coins, a potion, a pouch that holds gold, a sack that does not, and the seed level of the other tests.</summary>
internal static class MoneyWorld
{
    public const string Arc =
        ObjectWorld.Arc
        + "Object platinacoin\nname platinum coin\nname_pl platinum coins\nface plat.111\ntype 36\nweight 1\nvalue 50\nend\n"
        + "Object goldcoin\nname gold coin\nname_pl gold coins\nface gold.111\ntype 36\nweight 2\nvalue 10\nend\n"
        + "Object silvercoin\nname silver coin\nname_pl silver coins\nface silver.111\ntype 36\nweight 3\nvalue 1\nend\n"
        + "Object potion\nname potion\nname_pl potions\nface potion.111\ntype 5\nweight 200\nvalue 5\nend\n"
        + "Object pouch\nname pouch\nface pouch.111\ntype 122\nrace gold\nweight 100\ncontainer 10\nend\n"
        + "Object sack\nname sack\nface sack.111\ntype 122\nrace arrows\nweight 100\nend\n";

    public static TempFolder Make(string account, string inventory, string header = "", bool hall = false)
    {
        TempFolder world = TestWorld.Make(arc: Arc);
        Directory.CreateDirectory(Path.Combine(world.Path, "scripts"));
        foreach (string c in ObjectWorld.Classes)
        {
            File.WriteAllText(Path.Combine(world.Path, "scripts", c + ".txt"), "// class\n");
        }
        Directory.CreateDirectory(Path.Combine(world.Path, "mudlevels"));
        if (hall)
        {
            File.WriteAllText(Path.Combine(world.Path, "mudlevels", "hall.nw"), ObjectWorld.SeedHall);
        }
        ItemWorld.Account(world, account, header, inventory);
        return world;
    }
}

public sealed class MoneyTests
{
    private const string Silver30 = "arch silvercoin\nnrof 30\nend\n";

    private static ExtensionOptions Options() => new()
    {
        Writer = new FileWriterOptions { Quiet = TimeSpan.FromMilliseconds(20), MaxDelay = TimeSpan.FromMilliseconds(100) },
        ExtraManifest = ItemWorld.Register,
    };

    private static DirectExt Start(TempFolder world, long protoMinor = 1) => new(world, Options(), 1, true, 16_777_216, protoMinor);

    private static ulong Login(DirectExt ext, string account = "bob") => Ok(ext.Call(5, new[] { S(account) }, player: account));

    private static ReplyFrame Fn(DirectExt ext, int op, string? account, params Val[] args) => ext.Call((ulong)op, args, player: account);

    private static ReplyFrame Invoke(DirectExt ext, ulong target, int method, string? account, params Val[] args) =>
        ext.Call(13, new[] { H(target), Val.Int(method), Val.List(args) }, player: account);

    private static long MoneyOf(DirectExt ext, ulong player) => Get(ext, player, Ply("mudmoney")).AsInt;

    /// <summary>The kinds of the top-level items of a holder with their quantities (the sum when a kind has two stacks).</summary>
    private static Dictionary<string, long> Stacks(DirectExt ext, ulong holder, bool player = true)
    {
        ReplyFrame r = ext.Call(12, new[] { H(holder), Val.Int(player ? Ply("muditems") : Obj("muditems")), Val.List(Val.Int(Obj("archname")), Val.Int(Obj("mudquantity"))) });
        Assert.Equal(0UL, r.Status);
        var sums = new Dictionary<string, long>();
        foreach (Val row in r.Result.Items)
        {
            string kind = row.Items[1].AsString;
            sums[kind] = sums.GetValueOrDefault(kind) + row.Items[2].AsInt;
        }
        return sums;
    }

    private static List<ulong> ItemHandles(DirectExt ext, ulong holder, bool player = true)
    {
        ReplyFrame r = ext.Call(12, new[] { H(holder), Val.Int(player ? Ply("muditems") : Obj("muditems")), Val.List() });
        return r.Result.Items.Select(row => row.Items[0].AsHandle).ToList();
    }

    private static ulong ItemOf(DirectExt ext, ulong holder, string archname, bool player = true) =>
        ItemHandles(ext, holder, player).Single(h => Get(ext, h, Obj("archname")).AsString == archname);

    // ------------------------------------------------------------------ mudaddmoney: positive amounts

    [Fact]
    public void MudAddMoney_Given_a_positive_amount_When_called_Then_platinum_first_then_gold_then_silver()
    {
        // Arrange
        using DirectExt ext = Start(MoneyWorld.Make("bob", ""));
        ulong bob = Login(ext);

        // Act
        ReplyFrame r = Fn(ext, Manifest.OpMudAddMoney, "bob", Val.Int(12345));

        // Assert
        Assert.Equal(0UL, r.Status);
        var stacks = Stacks(ext, bob);
        Assert.Equal(246, stacks["platinacoin"]);
        Assert.Equal(4, stacks["goldcoin"]);
        Assert.Equal(5, stacks["silvercoin"]);
        Assert.Equal(3, stacks.Count);
        Assert.Equal(12345, MoneyOf(ext, bob));
    }

    [Fact]
    public void MudAddMoney_Given_coins_that_are_made_When_a_stack_of_the_file_is_not_identified_Then_they_are_identified_and_a_stack_of_their_own()
    {
        // Arrange
        using DirectExt ext = Start(MoneyWorld.Make("bob", Silver30));
        ulong bob = Login(ext);

        // Act
        Fn(ext, Manifest.OpMudAddMoney, "bob", Val.Int(5));

        // Assert: identified and not identified coins do not merge (the next payment merges them: MudPay_Given_two_stacks...)
        var flags = ItemHandles(ext, bob).Select(h => (Get(ext, h, Obj("identified")).AsBool, Get(ext, h, Obj("mudquantity")).AsInt)).OrderBy(t => t.Item2).ToList();
        Assert.Equal(new[] { (true, 5L), (false, 30L) }, flags);
        Assert.Equal(35, MoneyOf(ext, bob));
    }

    [Fact]
    public void MudAddMoney_Given_a_stack_that_is_identified_When_coins_are_added_Then_they_merge_into_it()
    {
        // Arrange
        using DirectExt ext = Start(MoneyWorld.Make("bob", "arch silvercoin\nnrof 30\nidentified 1\nend\n"));
        ulong bob = Login(ext);

        // Act
        Fn(ext, Manifest.OpMudAddMoney, "bob", Val.Int(5));

        // Assert
        Assert.Equal(35, Get(ext, ItemOf(ext, bob, "silvercoin"), Obj("mudquantity")).AsInt);
    }

    [Fact]
    public void MudAddMoney_Given_a_gold_pouch_that_is_applied_When_coins_are_added_Then_the_pouch_gets_them_up_to_its_weight_limit_and_the_pack_the_rest()
    {
        // Arrange: weight_limit 10, a platinum coin weighs 1
        using DirectExt ext = Start(MoneyWorld.Make("bob", "arch pouch\napplied 1\nend\n"));
        ulong bob = Login(ext);
        ulong pouch = ItemOf(ext, bob, "pouch");

        // Act
        Fn(ext, Manifest.OpMudAddMoney, "bob", Val.Int(1000));

        // Assert
        Assert.Equal(10, Stacks(ext, pouch, player: false)["platinacoin"]);
        Assert.Equal(10, Stacks(ext, bob)["platinacoin"]);
        Assert.Equal(1000, MoneyOf(ext, bob));
    }

    [Fact]
    public void MudAddMoney_Given_a_pouch_that_makes_the_coins_weigh_nothing_When_coins_are_added_Then_one_unit_is_counted_and_nothing_is_divided_by_zero()
    {
        // Arrange: Str 50 halves the platinum coin's weight of 1 to 0, which C raises to 1 ("Prevent divide by zero")
        using DirectExt ext = Start(MoneyWorld.Make("bob", "arch pouch\napplied 1\nStr 50\nend\n"));
        ulong bob = Login(ext);
        ulong pouch = ItemOf(ext, bob, "pouch");

        // Act
        ReplyFrame r = Fn(ext, Manifest.OpMudAddMoney, "bob", Val.Int(1000));

        // Assert
        Assert.Equal(0UL, r.Status);
        Assert.Equal(10, Stacks(ext, pouch, player: false)["platinacoin"]);
        Assert.Equal(1000, MoneyOf(ext, bob));
    }

    [Fact]
    public void MudAddMoney_Given_a_pouch_that_is_not_applied_or_does_not_hold_gold_When_coins_are_added_Then_the_pack_gets_them()
    {
        // Arrange
        using DirectExt ext = Start(MoneyWorld.Make("bob", "arch pouch\nend\narch sack\napplied 1\nend\n"));
        ulong bob = Login(ext);

        // Act
        Fn(ext, Manifest.OpMudAddMoney, "bob", Val.Int(100));

        // Assert
        Assert.Empty(Stacks(ext, ItemOf(ext, bob, "pouch"), player: false));
        Assert.Empty(Stacks(ext, ItemOf(ext, bob, "sack"), player: false));
        Assert.Equal(2, Stacks(ext, bob)["platinacoin"]);
    }

    [Fact]
    public void MudAddMoney_Given_a_big_sum_When_called_Then_no_weight_test_refuses_it()
    {
        // Arrange: 10,000 platinum coins at the end of the start tasks weigh 10 kg and the player's limit is lower (docs/mud-phase3.md 4.3)
        using DirectExt ext = Start(MoneyWorld.Make("bob", ""));
        ulong bob = Login(ext);

        // Act
        Fn(ext, Manifest.OpMudAddMoney, "bob", Val.Int(500_000));

        // Assert
        Assert.Equal(10_000, Stacks(ext, bob)["platinacoin"]);
        Assert.Equal(500_000, MoneyOf(ext, bob));
    }

    [Fact]
    public void MudAddMoney_Given_zero_When_called_Then_nothing_changes()
    {
        // Arrange
        using DirectExt ext = Start(MoneyWorld.Make("bob", Silver30));
        ulong bob = Login(ext);

        // Act
        ReplyFrame r = Fn(ext, Manifest.OpMudAddMoney, "bob", Val.Int(0));

        // Assert
        Assert.Equal(0UL, r.Status);
        Assert.Equal(30, MoneyOf(ext, bob));
        Assert.Equal(new[] { 30L }, Stacks(ext, bob).Values);
    }

    // ------------------------------------------------------------------ mudaddmoney: negative amounts

    [Fact]
    public void MudAddMoney_Given_a_negative_amount_When_the_player_has_one_big_coin_Then_the_change_is_in_coins_to_the_unit()
    {
        // Arrange
        using DirectExt ext = Start(MoneyWorld.Make("bob", "arch platinacoin\nnrof 1\nend\n"));
        ulong bob = Login(ext);

        // Act
        Fn(ext, Manifest.OpMudAddMoney, "bob", Val.Int(-7));

        // Assert
        var stacks = Stacks(ext, bob);
        Assert.False(stacks.ContainsKey("platinacoin"));
        Assert.Equal(4, stacks["goldcoin"]);
        Assert.Equal(3, stacks["silvercoin"]);
        Assert.Equal(43, MoneyOf(ext, bob));
    }

    [Fact]
    public void MudAddMoney_Given_a_negative_amount_above_what_the_player_has_When_called_Then_everything_goes_and_the_call_returns()
    {
        // Arrange
        using DirectExt ext = Start(MoneyWorld.Make("bob", "arch goldcoin\nnrof 3\nend\narch silvercoin\nnrof 4\nend\narch pouch\napplied 1\narch goldcoin\nnrof 2\nend\nend\narch banana\nnrof 1\nend\n"));
        ulong bob = Login(ext);

        // Act
        ReplyFrame r = Fn(ext, Manifest.OpMudAddMoney, "bob", Val.Int(-100_000));

        // Assert
        Assert.Equal(0UL, r.Status);
        Assert.Equal(0, MoneyOf(ext, bob));
        var stacks = Stacks(ext, bob);
        Assert.Equal(new[] { "banana", "pouch" }, stacks.Keys.OrderBy(k => k));
        Assert.Empty(Stacks(ext, ItemOf(ext, bob, "pouch"), player: false));
    }

    [Fact]
    public void MudAddMoney_Given_differences_as_the_start_tasks_pass_them_When_called_in_a_row_Then_the_sum_is_exact_every_time()
    {
        // Arrange: GK sets money to an amount with mudaddmoney(amount - mudmoney) (starttask_functions.txt)
        using DirectExt ext = Start(MoneyWorld.Make("bob", "arch goldcoin\nnrof 3\nend\narch silvercoin\nnrof 4\nend\n"));
        ulong bob = Login(ext);
        long[] targets = { 0, 75, 10_000, 5, 123_456, 0, 999, 1, 50, 49, 51, 7_777_777, 3, 0 };

        foreach (long target in targets)
        {
            // Act
            long quant = MoneyOf(ext, bob);
            ReplyFrame r = Fn(ext, Manifest.OpMudAddMoney, "bob", Val.Int(target - quant));

            // Assert
            Assert.Equal(0UL, r.Status);
            Assert.Equal(target, MoneyOf(ext, bob));
        }
    }

    [Fact]
    public void MudAddMoney_Given_coins_in_a_gold_pouch_and_the_pack_When_a_difference_is_taken_Then_the_pack_pays_first_and_the_sum_is_exact()
    {
        // Arrange
        using DirectExt ext = Start(MoneyWorld.Make("bob", "arch silvercoin\nnrof 4\nend\narch pouch\napplied 1\narch goldcoin\nnrof 9\nend\nend\n"));
        ulong bob = Login(ext);
        ulong pouch = ItemOf(ext, bob, "pouch");

        // Act
        Fn(ext, Manifest.OpMudAddMoney, "bob", Val.Int(-36));

        // Assert: 94 - 36; the 4 silver of the pack are spent first, then 4 gold of the pouch (32 to pay), and the change of 8 is silver in the pouch
        Assert.Equal(58, MoneyOf(ext, bob));
        Assert.Equal(5, Stacks(ext, pouch, player: false)["goldcoin"]);
        Assert.Equal(8, Stacks(ext, pouch, player: false)["silvercoin"]);
        Assert.False(Stacks(ext, bob).ContainsKey("silvercoin"));
        Assert.Equal(new[] { "goldcoin", "silvercoin" }, Stacks(ext, pouch, player: false).Keys.OrderBy(k => k));
    }

    [Fact]
    public void MudAddMoney_Given_a_float_or_nil_argument_When_called_Then_it_is_cut_or_zero_and_a_text_is_bad_args()
    {
        // Arrange
        using DirectExt ext = Start(MoneyWorld.Make("bob", ""));
        ulong bob = Login(ext);

        // Act / Assert
        Assert.Equal(0UL, Fn(ext, Manifest.OpMudAddMoney, "bob", Val.Float(12.9)).Status);
        Assert.Equal(12, MoneyOf(ext, bob));
        Assert.Equal(0UL, Fn(ext, Manifest.OpMudAddMoney, "bob", Val.Nil).Status);
        Assert.Equal(12, MoneyOf(ext, bob));
        Assert.Equal((ulong)Status.BadArgs, Fn(ext, Manifest.OpMudAddMoney, "bob", S("5")).Status);
        Assert.Equal((ulong)Status.BadArgs, Fn(ext, Manifest.OpMudAddMoney, "bob").Status);
        Assert.Equal(12, MoneyOf(ext, bob));
    }

    [Fact]
    public void MudAddMoney_Given_no_player_in_the_context_When_called_Then_bad_args()
    {
        // Arrange
        using DirectExt ext = Start(MoneyWorld.Make("bob", ""));
        Login(ext);

        // Act
        ReplyFrame r = Fn(ext, Manifest.OpMudAddMoney, null, Val.Int(5));

        // Assert
        Assert.Equal((ulong)Status.BadArgs, r.Status);
    }

    [Fact]
    public void MudAddMoney_Given_the_extension_is_busy_When_called_Then_busy_and_nothing_changes()
    {
        // Arrange
        using DirectExt ext = Start(MoneyWorld.Make("bob", Silver30));
        ulong bob = Login(ext);
        ext.Dispatcher.Busy = "saving";

        // Act
        ReplyFrame add = Fn(ext, Manifest.OpMudAddMoney, "bob", Val.Int(500));
        ReplyFrame sub = Fn(ext, Manifest.OpMudAddMoney, "bob", Val.Int(-5));

        // Assert
        Assert.Equal((ulong)Status.Busy, add.Status);
        Assert.Equal((ulong)Status.Busy, sub.Status);
        ext.Dispatcher.Busy = null;
        Assert.Equal(30, MoneyOf(ext, bob));
    }

    [Fact]
    public void MudAddMoney_Given_a_change_of_money_When_the_request_ends_Then_the_strings_of_the_coins_and_the_weight_are_written_and_the_account_is_saved()
    {
        // Arrange
        using DirectExt ext = Start(MoneyWorld.Make("bob", ""));
        Login(ext);

        // Act
        ReplyFrame r = Fn(ext, Manifest.OpMudAddMoney, "bob", Val.Int(100));
        string file = Flushed(ext, "accounts/bob.txt", t => t.Contains("platinacoin", StringComparison.Ordinal));

        // Assert
        var sets = Effects(r, "client.set").ToDictionary(e => e.Get("name")!.AsString, e => e.Get("value")!.AsString);
        Assert.Contains(sets, kv => kv.Key.StartsWith("clientr.mud_item", StringComparison.Ordinal) && kv.Value.StartsWith("platinum coins,mud_plat.png,36,2,2,", StringComparison.Ordinal));
        Assert.Equal("70002", sets["clientr.mud_weight"]);
        Assert.Contains("nrof 2", file, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------ mudpay

    [Fact]
    public void MudPay_Given_enough_money_When_called_as_a_function_Then_true_and_the_smallest_coins_pay_with_change_in_smaller_ones()
    {
        // Arrange: 50 + 3
        using DirectExt ext = Start(MoneyWorld.Make("bob", "arch goldcoin\nnrof 5\nend\narch silvercoin\nnrof 3\nend\n"));
        ulong bob = Login(ext);

        // Act
        ReplyFrame r = Fn(ext, Manifest.OpMudPay, "bob", Val.Int(12));

        // Assert: 3 silver, then 1 gold (9 left to pay), change 1 silver
        Assert.True(r.Result.AsBool);
        var stacks = Stacks(ext, bob);
        Assert.Equal(4, stacks["goldcoin"]);
        Assert.Equal(1, stacks["silvercoin"]);
        Assert.Equal(41, MoneyOf(ext, bob));
    }

    [Fact]
    public void MudPay_Given_enough_money_When_called_as_a_method_of_the_player_Then_the_same_as_the_function()
    {
        // Arrange
        using DirectExt ext = Start(MoneyWorld.Make("bob", "arch goldcoin\nnrof 5\nend\narch silvercoin\nnrof 3\nend\n"));
        ulong bob = Login(ext);

        // Act
        ReplyFrame r = Invoke(ext, bob, TMudPlayerMembers.MethodMudPay, "bob", Val.Int(12));

        // Assert
        Assert.True(r.Result.AsBool);
        Assert.Equal(41, MoneyOf(ext, bob));
        Assert.Equal(1, Stacks(ext, bob)["silvercoin"]);
    }

    [Fact]
    public void MudPay_Given_less_money_than_the_price_When_called_Then_false_and_no_change_at_all()
    {
        // Arrange
        using DirectExt ext = Start(MoneyWorld.Make("bob", "arch goldcoin\nnrof 5\nend\narch silvercoin\nnrof 3\nend\n"));
        ulong bob = Login(ext);
        var before = Stacks(ext, bob);

        // Act
        ReplyFrame f = Fn(ext, Manifest.OpMudPay, "bob", Val.Int(54));
        ReplyFrame m = Invoke(ext, bob, TMudPlayerMembers.MethodMudPay, "bob", Val.Int(54));

        // Assert
        Assert.False(f.Result.AsBool);
        Assert.False(m.Result.AsBool);
        Assert.Equal(before, Stacks(ext, bob));
        Assert.Equal(53, MoneyOf(ext, bob));
    }

    [Fact]
    public void MudPay_Given_money_in_the_pack_and_in_a_gold_pouch_When_called_Then_the_pack_pays_first_and_the_pouch_the_rest()
    {
        // Arrange
        using DirectExt ext = Start(MoneyWorld.Make("bob", Silver30 + "arch pouch\napplied 1\narch goldcoin\nnrof 10\nend\nend\n"));
        ulong bob = Login(ext);
        ulong pouch = ItemOf(ext, bob, "pouch");

        // Act
        ReplyFrame r = Fn(ext, Manifest.OpMudPay, "bob", Val.Int(50));

        // Assert
        Assert.True(r.Result.AsBool);
        Assert.False(Stacks(ext, bob).ContainsKey("silvercoin"));
        Assert.Equal(8, Stacks(ext, pouch, player: false)["goldcoin"]);
        Assert.Equal(80, MoneyOf(ext, bob));
    }

    [Fact]
    public void MudPay_Given_containers_that_are_not_applied_or_hold_no_gold_When_called_Then_their_coins_are_not_money()
    {
        // Arrange
        using DirectExt ext = Start(MoneyWorld.Make("bob", "arch pouch\narch goldcoin\nnrof 10\nend\nend\narch sack\napplied 1\narch goldcoin\nnrof 10\nend\nend\n"));
        ulong bob = Login(ext);

        // Act
        ReplyFrame r = Fn(ext, Manifest.OpMudPay, "bob", Val.Int(10));

        // Assert
        Assert.False(r.Result.AsBool);
        Assert.Equal(0, MoneyOf(ext, bob));
        Assert.Equal(10, Stacks(ext, ItemOf(ext, bob, "pouch"), player: false)["goldcoin"]);
        Assert.Equal(10, Stacks(ext, ItemOf(ext, bob, "sack"), player: false)["goldcoin"]);
    }

    [Fact]
    public void MudPay_Given_zero_or_a_negative_price_When_called_Then_zero_is_paid_and_a_negative_price_is_refused_without_change()
    {
        // Arrange
        using DirectExt ext = Start(MoneyWorld.Make("bob", Silver30));
        ulong bob = Login(ext);

        // Act
        ReplyFrame zero = Fn(ext, Manifest.OpMudPay, "bob", Val.Int(0));
        ReplyFrame negative = Fn(ext, Manifest.OpMudPay, "bob", Val.Int(-5));
        ReplyFrame text = Fn(ext, Manifest.OpMudPay, "bob", S("x"));

        // Assert
        Assert.True(zero.Result.AsBool);
        Assert.False(negative.Result.AsBool);
        Assert.Equal((ulong)Status.BadArgs, text.Status);
        Assert.Equal(30, MoneyOf(ext, bob));
    }

    [Fact]
    public void MudPay_Given_the_extension_is_busy_When_called_as_function_or_method_Then_busy_and_nothing_changes()
    {
        // Arrange
        using DirectExt ext = Start(MoneyWorld.Make("bob", Silver30));
        ulong bob = Login(ext);
        ext.Dispatcher.Busy = "recovering";

        // Act
        ReplyFrame f = Fn(ext, Manifest.OpMudPay, "bob", Val.Int(5));
        ReplyFrame m = Invoke(ext, bob, TMudPlayerMembers.MethodMudPay, "bob", Val.Int(5));
        ext.Dispatcher.Busy = null;

        // Assert
        Assert.Equal((ulong)Status.Busy, f.Status);
        Assert.Equal((ulong)Status.Busy, m.Status);
        Assert.Equal(30, MoneyOf(ext, bob));
    }

    [Fact]
    public void MudPay_Given_two_stacks_of_one_coin_When_called_Then_they_are_merged_and_the_error_is_logged()
    {
        // Arrange: one stack is identified, the other is not: they do not merge on their own
        using DirectExt ext = Start(MoneyWorld.Make("bob", "arch silvercoin\nnrof 5\nend\narch silvercoin\nnrof 7\nidentified 1\nend\n"));
        ulong bob = Login(ext);
        Assert.Equal(2, ItemHandles(ext, bob).Count);

        // Act
        ReplyFrame r = Fn(ext, Manifest.OpMudPay, "bob", Val.Int(3));

        // Assert
        Assert.True(r.Result.AsBool);
        Assert.Single(ItemHandles(ext, bob));
        Assert.Equal(9, MoneyOf(ext, bob));
        Assert.Contains(ext.Logs, l => l.Text.Contains("has two money entries of (silvercoin)", StringComparison.Ordinal));
    }

    [Fact]
    public void MudPay_Given_a_coin_whose_value_was_changed_When_called_Then_it_is_not_taken_apart_and_the_error_is_logged()
    {
        // Arrange: a silver coin with value 5 is money (QueryMoney counts it) but is not one of the three coins of the shop
        using DirectExt ext = Start(MoneyWorld.Make("bob", "arch silvercoin\nvalue 5\nnrof 2\nend\narch silvercoin\nnrof 4\nidentified 1\nend\n"));
        ulong bob = Login(ext);

        // Act
        ReplyFrame r = Fn(ext, Manifest.OpMudPay, "bob", Val.Int(8));

        // Assert: the 4 plain silver coins are paid and the odd stack stays, as C's pay_for_amount does (it says true all the same)
        Assert.True(r.Result.AsBool);
        Assert.Equal(10, MoneyOf(ext, bob));
        Assert.Contains(ext.Logs, l => l.Text.Contains("Did not find string match for silvercoin", StringComparison.Ordinal));
    }

    // ------------------------------------------------------------------ mudvalue and the identified-at-creation rule

    [Fact]
    public void MudValue_Given_an_identified_stack_and_no_player_When_asked_to_buy_or_sell_Then_the_stack_is_worth_its_value()
    {
        // Arrange
        using DirectExt ext = Start(MoneyWorld.Make("bob", "arch banana\nnrof 4\nidentified 1\nend\n"));
        ulong bob = Login(ext);
        ulong banana = ItemOf(ext, bob, "banana");

        // Act
        ReplyFrame buy = Invoke(ext, banana, TMudObjectMembers.MethodMudValue, null, Val.Int(0));
        ReplyFrame sell = Invoke(ext, banana, TMudObjectMembers.MethodMudValue, null, Val.Int(1));

        // Assert
        Assert.Equal(20, buy.Result.AsInt);
        Assert.Equal(20, sell.Result.AsInt);
    }

    [Fact]
    public void MudValue_Given_a_player_in_the_context_When_asked_Then_the_charisma_bargains_as_query_cost_does()
    {
        // Arrange: Cha 15 has the bonus 2.5, diff (2.5 - 1) / (2.5 + 1); buying 4 * 20 * (1 + diff), selling 4 * 20 * (1 - diff)
        using DirectExt ext = Start(MoneyWorld.Make("bob", "arch banana\nnrof 4\nidentified 1\nend\n", header: "Cha 15\n"));
        ulong bob = Login(ext);
        ulong banana = ItemOf(ext, bob, "banana");

        // Act
        ReplyFrame buy = Invoke(ext, banana, TMudObjectMembers.MethodMudValue, "bob", Val.Int(0));
        ReplyFrame sell = Invoke(ext, banana, TMudObjectMembers.MethodMudValue, "bob", Val.Int(1));

        // Assert
        Assert.Equal(114, buy.Result.AsInt);
        Assert.Equal(45, sell.Result.AsInt);
    }

    [Fact]
    public void MudValue_Given_a_potion_that_the_archetype_does_not_identify_When_the_item_is_made_for_a_script_Then_selling_pays_its_value_and_buying_asks_its_value()
    {
        // Arrange: the golden prices of docs/mud-phase3.md 4.3: 3 potions of value 5 each
        using DirectExt ext = Start(MoneyWorld.Make("bob", ""));
        ulong bob = Login(ext);
        Assert.Equal(0UL, ext.Call(ItemWorld.OpDo, new[] { S("add"), S("bob"), S("potion"), Val.Int(3) }, player: "bob").Status);
        ulong potion = ItemOf(ext, bob, "potion");

        // Act
        ReplyFrame sell = Invoke(ext, potion, TMudObjectMembers.MethodMudValue, null, Val.Int(1));
        ReplyFrame buy = Invoke(ext, potion, TMudObjectMembers.MethodMudValue, null, Val.Int(0));

        // Assert: not 600 (unidentified potions sell for 1000 each, capped) and not 50 times the value (750)
        Assert.True(Get(ext, potion, Obj("identified")).AsBool);
        Assert.Equal(15, sell.Result.AsInt);
        Assert.Equal(15, buy.Result.AsInt);
    }

    [Fact]
    public void MudValue_Given_the_same_potion_made_by_a_file_that_does_not_identify_it_When_asked_Then_C_prices_apply_as_they_always_did()
    {
        // Arrange: the contrast of the golden test: an object that was not made for a script keeps what its file says
        using DirectExt ext = Start(MoneyWorld.Make("bob", "arch potion\nnrof 3\nend\n"));
        ulong bob = Login(ext);
        ulong potion = ItemOf(ext, bob, "potion");

        // Act
        ReplyFrame sell = Invoke(ext, potion, TMudObjectMembers.MethodMudValue, null, Val.Int(1));
        ReplyFrame buy = Invoke(ext, potion, TMudObjectMembers.MethodMudValue, null, Val.Int(0));

        // Assert
        Assert.Equal(600, sell.Result.AsInt);
        Assert.Equal(750, buy.Result.AsInt);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(-1)]
    public void MudValue_Given_a_flag_that_is_neither_0_nor_1_When_asked_Then_bad_args(int flag)
    {
        // Arrange
        using DirectExt ext = Start(MoneyWorld.Make("bob", "arch banana\nnrof 4\nend\n"));
        ulong bob = Login(ext);

        // Act
        ReplyFrame r = Invoke(ext, ItemOf(ext, bob, "banana"), TMudObjectMembers.MethodMudValue, null, Val.Int(flag));

        // Assert
        Assert.Equal((ulong)Status.BadArgs, r.Status);
    }

    [Fact]
    public void MudValue_Given_an_object_of_a_level_When_asked_Then_it_is_priced_like_an_item()
    {
        // Arrange: the seed hall's banana (3 of value 5, not identified: one third when selling, 50 times when buying)
        using DirectExt ext = Start(MoneyWorld.Make("bob", "", hall: true));
        var (places, _) = LoadAll(ext, "hall.nw");
        ulong banana = places[0].Get("handle")!.AsHandle;

        // Act
        ReplyFrame sell = Invoke(ext, banana, TMudObjectMembers.MethodMudValue, null, Val.Int(1));
        ReplyFrame buy = Invoke(ext, banana, TMudObjectMembers.MethodMudValue, null, Val.Int(0));

        // Assert
        Assert.Equal(5, sell.Result.AsInt);
        Assert.Equal(750, buy.Result.AsInt);
    }

    [Fact]
    public void MudValue_Given_an_archetype_When_asked_Then_one_object_made_for_a_script_is_priced()
    {
        // Arrange: fakeitem.txt asks the value of an archetype it found (findarchetype)
        using DirectExt ext = Start(MoneyWorld.Make("bob", ""));
        Login(ext);
        ulong sword = ArchHandle(ext, "sword");
        ulong potion = ArchHandle(ext, "potion");
        ulong platinum = ArchHandle(ext, "platinacoin");

        // Act / Assert: no player: the value; the potion is identified, as the item a script would make of it is
        Assert.Equal(50, Invoke(ext, sword, TMudArchMembers.MethodMudValue, null, Val.Int(0)).Result.AsInt);
        Assert.Equal(50, Invoke(ext, sword, TMudArchMembers.MethodMudValue, null, Val.Int(1)).Result.AsInt);
        Assert.Equal(5, Invoke(ext, potion, TMudArchMembers.MethodMudValue, null, Val.Int(1)).Result.AsInt);
        Assert.Equal(5, Invoke(ext, potion, TMudArchMembers.MethodMudValue, null, Val.Int(0)).Result.AsInt);
        Assert.Equal(50, Invoke(ext, platinum, TMudArchMembers.MethodMudValue, null, Val.Int(0)).Result.AsInt); // nrof 1, not the archetype's 0
        Assert.Equal((ulong)Status.BadArgs, Invoke(ext, sword, TMudArchMembers.MethodMudValue, null, Val.Int(3)).Status);
    }

    [Fact]
    public void MudValue_Given_an_archetype_and_a_player_in_the_context_When_asked_Then_the_player_bargains()
    {
        // Arrange: Cha 0 has the bonus 10: diff 9/11: selling 4 * 50 * (1 - diff) = 36
        using DirectExt ext = Start(MoneyWorld.Make("bob", ""));
        Login(ext);
        ulong sword = ArchHandle(ext, "sword");

        // Act
        long sell = Invoke(ext, sword, TMudArchMembers.MethodMudValue, "bob", Val.Int(1)).Result.AsInt;
        long buy = Invoke(ext, sword, TMudArchMembers.MethodMudValue, "bob", Val.Int(0)).Result.AsInt;

        // Assert
        Assert.Equal(36, sell);
        Assert.Equal(363, buy);
    }

    [Fact]
    public void MudValue_Given_the_manifest_When_read_Then_mudvalue_is_a_method_of_TMudObject_and_of_TMudArch_and_mudpay_a_function_and_a_method_of_TMudPlayer()
    {
        // Arrange
        using DirectExt ext = Start(MoneyWorld.Make("bob", ""));

        // Act
        Val manifest = Manifest.Build(false, 1);
        var types = manifest.Get("types")!.Items.ToDictionary(t => t.Get("name")!.AsString);
        var functions = manifest.Get("functions")!.Items.ToDictionary(f => f.Get("name")!.AsString);

        // Assert
        Assert.Contains(types["TMudObject"].Get("methods")!.Items, m => m.Get("name")!.AsString == "mudvalue" && m.Get("id")!.AsInt == 6000);
        Assert.Contains(types["TMudArch"].Get("methods")!.Items, m => m.Get("name")!.AsString == "mudvalue" && m.Get("id")!.AsInt == 6750 && m.Get("ret")!.AsString == "int");
        Assert.Contains(types["TMudPlayer"].Get("methods")!.Items, m => m.Get("name")!.AsString == "mudpay" && m.Get("id")!.AsInt == 6500);
        Assert.True(functions["mudpay"].Get("slow")!.AsBool);
        Assert.True(functions["mudaddmoney"].Get("slow")!.AsBool);
        Assert.False(functions["mudobjectattr"].Get("slow")!.AsBool);
        Assert.Equal(new long[] { 6000, 6001, 6002, 6003 }, new[] { "mudaddmoney", "mudpay", "mudobjectattr", "mudexamineinvobject" }.Select(n => functions[n].Get("id")!.AsInt));
        Assert.Equal(new[] { "examine", "mudexamineobject", "mudidentify", "mudvalue" }, types["TMudObject"].Get("methods")!.Items.Select(m => m.Get("name")!.AsString).Where(n => n is "examine" or "mudexamineobject" or "mudidentify" or "mudvalue").OrderBy(n => n));
    }

    // ------------------------------------------------------------------ mudobjectattr

    [Theory]
    [InlineData(0, "nothing")]
    [InlineData(1, "1 silver coin")]
    [InlineData(53, "1 platinum coin and 3 silver coins")]
    [InlineData(160, "3 platinum coins and 1 gold coin")]
    [InlineData(1_000_011, "20000 platinum coins, 1 gold coin and 1 silver coin")]
    [InlineData(-5, "nothing")]
    public void MudObjectAttr_Given_money_and_an_amount_When_called_Then_the_text_of_the_coins(int amount, string expected)
    {
        // Arrange
        using DirectExt ext = Start(MoneyWorld.Make("bob", ""));

        // Act
        ReplyFrame r = Fn(ext, Manifest.OpMudObjectAttr, null, S("money"), S(amount.ToString()));

        // Assert
        Assert.Equal(0UL, r.Status);
        Assert.Equal(expected, r.Result.AsString);
    }

    [Fact]
    public void MudObjectAttr_Given_an_amount_as_a_number_or_with_a_fraction_When_called_Then_it_is_cut_and_the_word_money_is_any_case()
    {
        // Arrange
        using DirectExt ext = Start(MoneyWorld.Make("bob", ""));

        // Act / Assert
        Assert.Equal("3 platinum coins", Fn(ext, Manifest.OpMudObjectAttr, null, S("money"), S("150.7")).Result.AsString);
        Assert.Equal("3 platinum coins", Fn(ext, Manifest.OpMudObjectAttr, null, S("MONEY"), Val.Int(150)).Result.AsString);
        Assert.Equal("3 platinum coins", Fn(ext, Manifest.OpMudObjectAttr, null, S("Money"), Val.Float(150.0)).Result.AsString);
        Assert.Equal("nothing", Fn(ext, Manifest.OpMudObjectAttr, null, S("money"), S("many")).Result.AsString);
        Assert.Equal("nothing", Fn(ext, Manifest.OpMudObjectAttr, null, S("money"), Val.Nil).Result.AsString);
    }

    [Fact]
    public void MudObjectAttr_Given_the_id_of_an_item_of_the_player_When_asked_each_attribute_Then_the_golden_texts()
    {
        // Arrange
        using DirectExt ext = Start(MoneyWorld.Make("bob", "arch banana\nnrof 4\nend\n"));
        ulong bob = Login(ext);
        string id = ((uint)ItemOf(ext, bob, "banana")).ToString();

        // Act / Assert
        string Attr(string attribute, Val? first = null) => Fn(ext, Manifest.OpMudObjectAttr, "bob", first ?? S(id), S(attribute)).Result.AsString;
        Assert.Equal("banana", Attr("archname"));
        Assert.Equal("bananas", Attr("name"));
        Assert.Equal("mud_banana.png", Attr("icon"));
        Assert.Equal("20", Attr("food"));
        Assert.Equal("6", Attr("type"));
        Assert.Equal("4", Attr("quantity"));
        Assert.Equal("100", Attr("weight"));
        Assert.Equal("5", Attr("value"));
        Assert.Equal("", Attr("colour"));
        Assert.Equal("banana", Attr("archname", Val.Int(long.Parse(id)))); // a number is its text
    }

    [Fact]
    public void MudObjectAttr_Given_an_archetype_name_When_asked_Then_the_attributes_of_its_clone()
    {
        // Arrange
        using DirectExt ext = Start(MoneyWorld.Make("bob", ""));

        // Act / Assert
        string Attr(string name, string attribute) => Fn(ext, Manifest.OpMudObjectAttr, null, S(name), S(attribute)).Result.AsString;
        Assert.Equal("sword", Attr("sword", "name"));
        Assert.Equal("mud_sword.png", Attr("sword", "icon"));
        Assert.Equal("15", Attr("sword", "type"));
        Assert.Equal("50", Attr("sword", "value"));
        Assert.Equal("3000", Attr("sword", "weight"));
        Assert.Equal("1", Attr("sword", "quantity"));
        Assert.Equal("20", Attr("banana", "food"));
        Assert.Equal("sword", Attr("sword", "archname"));
    }

    [Fact]
    public void MudObjectAttr_Given_an_id_or_name_that_names_nothing_the_player_may_see_When_asked_Then_the_empty_text()
    {
        // Arrange: bob's id 1 is a level object's tag at best, alice's item is not bob's
        TempFolder world = MoneyWorld.Make("bob", "arch banana\nnrof 4\nend\n");
        ItemWorld.Account(world, "alice", "", "arch sword\nnrof 1\nend\n");
        using DirectExt ext = Start(world);
        Login(ext, "bob");
        ulong alice = Login(ext, "alice");
        string alicesSword = ((uint)ItemOf(ext, alice, "sword")).ToString();

        // Act / Assert
        string Attr(string first, string attribute, string? account = "bob") => Fn(ext, Manifest.OpMudObjectAttr, account, S(first), S(attribute)).Result.AsString;
        Assert.Equal("sword", Attr(alicesSword, "archname", "alice"));
        Assert.Equal("", Attr(alicesSword, "archname"));
        Assert.Equal("", Attr("999999", "archname"));
        Assert.Equal("", Attr("0", "archname"));
        Assert.Equal("", Attr("4294967296", "archname"));
        Assert.Equal("", Attr("99999999999999999999999", "archname"));
        Assert.Equal("", Attr("notanarchetype", "name"));
        Assert.Equal("", Attr("", "name"));
        Assert.Equal("", Attr(alicesSword, "archname", null));
    }

    [Fact]
    public void MudObjectAttr_Given_arguments_that_are_not_texts_or_numbers_When_called_Then_bad_args()
    {
        // Arrange
        using DirectExt ext = Start(MoneyWorld.Make("bob", ""));

        // Act / Assert
        Assert.Equal((ulong)Status.BadArgs, Fn(ext, Manifest.OpMudObjectAttr, null, Val.List(), S("name")).Status);
        Assert.Equal((ulong)Status.BadArgs, Fn(ext, Manifest.OpMudObjectAttr, null, S("sword")).Status);
    }

    [Fact]
    public void MudObjectAttr_Given_the_extension_is_busy_When_called_Then_it_runs_because_it_changes_nothing()
    {
        // Arrange
        using DirectExt ext = Start(MoneyWorld.Make("bob", ""));
        ext.Dispatcher.Busy = "saving";

        // Act
        ReplyFrame r = Fn(ext, Manifest.OpMudObjectAttr, null, S("money"), S("1"));

        // Assert
        Assert.Equal(0UL, r.Status);
        Assert.Equal("1 silver coin", r.Result.AsString);
    }

    // ------------------------------------------------------------------ mudidentify

    [Fact]
    public void MudIdentify_Given_an_unidentified_potion_of_the_player_When_called_Then_it_is_identified_named_after_its_archetype_and_shown_and_saved()
    {
        // Arrange
        using DirectExt ext = Start(MoneyWorld.Make("bob", "arch potion\nname mystery brew\nnrof 1\nend\n"));
        ulong bob = Login(ext);
        ulong potion = ItemOf(ext, bob, "potion");
        Assert.False(Get(ext, potion, Obj("identified")).AsBool);
        Assert.Equal("mystery brew", Get(ext, potion, Obj("fullname")).AsString);

        // Act
        ReplyFrame r = Invoke(ext, potion, ItemMethod("mudidentify"), "bob");
        string file = Flushed(ext, "accounts/bob.txt", t => t.Contains("identified 1", StringComparison.Ordinal));

        // Assert
        Assert.Equal(0UL, r.Status);
        Assert.True(Get(ext, potion, Obj("identified")).AsBool);
        Assert.Equal("potion", Get(ext, potion, Obj("fullname")).AsString);
        Assert.Contains(Effects(r, "client.set"), e => e.Get("name")!.AsString == "clientr.mud_item" + (uint)potion && e.Get("value")!.AsString.StartsWith("potion,", StringComparison.Ordinal));
        Assert.Contains("identified 1", file, StringComparison.Ordinal);
    }

    [Fact]
    public void MudIdentify_Given_an_unidentified_object_of_a_level_When_called_Then_it_is_identified_and_the_level_is_saved()
    {
        // Arrange
        using DirectExt ext = Start(MoneyWorld.Make("bob", "", hall: true));
        var (places, _) = LoadAll(ext, "hall.nw");
        ulong banana = places[0].Get("handle")!.AsHandle;

        // Act
        ReplyFrame r = Invoke(ext, banana, ItemMethod("mudidentify"), null);
        string file = Flushed(ext, "levels/hall.nw.map", t => t.Contains("identified 1", StringComparison.Ordinal));

        // Assert
        Assert.Equal(0UL, r.Status);
        Assert.True(Get(ext, banana, Obj("identified")).AsBool);
        Assert.Contains("identified 1", file, StringComparison.Ordinal);
    }

    [Fact]
    public void MudIdentify_Given_the_extension_is_busy_When_called_Then_busy_and_the_item_is_not_identified()
    {
        // Arrange
        using DirectExt ext = Start(MoneyWorld.Make("bob", "arch potion\nnrof 2\nend\n"));
        ulong bob = Login(ext);
        ulong potion = ItemOf(ext, bob, "potion");
        ext.Dispatcher.Busy = "saving";

        // Act
        ReplyFrame r = Invoke(ext, potion, ItemMethod("mudidentify"), "bob");
        ext.Dispatcher.Busy = null;

        // Assert
        Assert.Equal((ulong)Status.Busy, r.Status);
        Assert.False(Get(ext, potion, Obj("identified")).AsBool);
    }

    [Fact]
    public void MudIdentify_Given_an_argument_When_called_Then_bad_args()
    {
        // Arrange
        using DirectExt ext = Start(MoneyWorld.Make("bob", "arch potion\nnrof 2\nend\n"));
        ulong bob = Login(ext);

        // Act
        ReplyFrame r = Invoke(ext, ItemOf(ext, bob, "potion"), ItemMethod("mudidentify"), "bob", Val.Int(1));

        // Assert
        Assert.Equal((ulong)Status.BadArgs, r.Status);
    }

    private static int ItemMethod(string name) =>
        (int)Manifest.Build(false, 1).Get("types")!.Items.Single(t => t.Get("name")!.AsString == "TMudObject").Get("methods")!.Items.Single(m => m.Get("name")!.AsString == name).Get("id")!.AsInt;
}
