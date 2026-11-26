// Not ported from a Crossfire file: tests of the fix lane of wave 6 for the money code of wave 5C (Gk/Money.cs, Gk/MoneyObjectAttr.cs): the
// cases that the review's mutants of that code showed untested, at the level of the ported functions with a recording host.
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using Crossfire.Extension.Gk;

namespace Crossfire.Extension.Tests;

/// <summary>The host of the money tests: what the client is told and how often fix_player runs.</summary>
internal sealed class MoneyHost : CrossfireHost
{
    public int Fixes { get; set; }

    public List<uint> Deleted { get; } = new();

    public List<(CfObject Who, string Item)> Sent { get; } = new();

    public override void FixPlayer(CfObject player) => Fixes++;

    public override void EsrvDelItem(IPlayerHandle? player, uint tag) => Deleted.Add(tag);

    public override void EsrvSendItem(CfObject player, CfObject op) => Sent.Add((player, op.Name ?? ""));

    public override int PlayerDigestion(IPlayerHandle contr) => 0;

    public override int PlayerGenHp(IPlayerHandle contr) => 0;
}

public sealed class MoneyUnitTests
{
    private const string Arc =
        MoneyWorld.Arc
        + "Object bigpouch\nname bigpouch\ntype 122\nrace gold\nweight 100\nend\n"
        + "Object halfpouch\nname halfpouch\ntype 122\nrace gold\nweight 100\ncontainer 4\nStr 50\nend\n"
        + "Object plainsack\nname plainsack\ntype 122\nweight 100\ncontainer 1000\nend\n"
        + "Object ripe\nname ripe banana\ntype 6\nweight 10\nvalue 7\nfood 5\nend\n"
        + "Object nopeer\nname nopeer\ntype 15\nweight 1\nend\n";

    private sealed class Fixture
    {
        public MoneyHost Host { get; } = new();

        public CfWorld World { get; }

        public CfObject Player { get; }

        public Fixture()
        {
            World = new CfWorld(Host, (_, _) => { });
            World.InitArchetypes("Object empty_archetype\n" + Arc);
            Player = Make("human_player");
            Player.Name = "bob";
        }

        public CfObject Make(string arch) => World.ArchToObject(World.FindArchetype(arch))!;

        public CfObject Give(CfObject to, string arch, uint nrof = 1, bool applied = false)
        {
            CfObject o = Make(arch);
            o.Nrof = nrof;
            if (applied)
            {
                o.SetFlag(Define.FLAG_APPLIED);
            }
            return World.InsertObInOb(o, to);
        }

        public CfObject Create(Archetype at) => World.ArchToObject(at)!;

        public bool Pay(int amount) => Money.PayForAmount(World, Create, amount, Player);

        public long Count(CfObject holder, string arch)
        {
            long n = 0;
            for (CfObject? c = holder.Inv; c != null; c = c.Below)
            {
                n += c.Arch?.Name == arch ? Math.Max(1, c.Nrof) : 0;
            }
            return n;
        }
    }

    // ------------------------------------------------------------------ pay_for_amount

    [Fact]
    public void PayForAmount_Given_a_negative_amount_When_paid_Then_it_is_refused_and_nothing_changes()
    {
        // Arrange
        var f = new Fixture();
        f.Give(f.Player, "silvercoin", 30);

        // Act
        bool paid = f.Pay(-5);

        // Assert
        Assert.False(paid);
        Assert.Equal(30, f.World.QueryMoney(f.Player));
        Assert.Equal(0, f.Host.Fixes);
    }

    [Fact]
    public void PayForAmount_Given_money_in_the_pack_and_an_applied_container_without_a_race_When_paid_Then_the_container_pays_the_rest()
    {
        // Arrange: a sack with no race holds gold as C's pay_for_amount sees it
        var f = new Fixture();
        f.Give(f.Player, "silvercoin", 5);
        CfObject sack = f.Give(f.Player, "plainsack", applied: true);
        f.Give(sack, "silvercoin", 10);

        // Act
        bool paid = f.Pay(12);

        // Assert
        Assert.True(paid);
        Assert.Equal(3, f.World.QueryMoney(f.Player));
        Assert.Equal(3, f.Count(sack, "silvercoin"));
        Assert.Equal(0, f.Count(f.Player, "silvercoin"));
    }

    [Fact]
    public void PayForAmount_Given_a_payment_that_is_made_and_one_that_is_refused_When_paid_Then_fix_player_runs_only_for_the_first()
    {
        // Arrange
        var f = new Fixture();
        f.Give(f.Player, "silvercoin", 30);

        // Act
        bool paid = f.Pay(10);
        int afterPaid = f.Host.Fixes;
        bool refused = f.Pay(500);

        // Assert
        Assert.True(paid);
        Assert.Equal(1, afterPaid);
        Assert.False(refused);
        Assert.Equal(1, f.Host.Fixes);
    }

    // ------------------------------------------------------------------ pay_from_container: what the client is told

    [Fact]
    public void PayFromContainer_Given_coins_in_a_pouch_When_paid_Then_only_the_coins_of_the_player_himself_are_deleted_on_the_client()
    {
        // Arrange: delete the strings of the coins that leave the player's own pack; the ones in a container are not shown
        var f = new Fixture();
        CfObject own = f.Give(f.Player, "silvercoin", 5);
        uint ownTag = own.Count;
        CfObject pouch = f.Give(f.Player, "bigpouch", applied: true);
        CfObject inner = f.Give(pouch, "silvercoin", 10);
        uint innerTag = inner.Count;

        // Act
        bool paid = f.Pay(15);

        // Assert
        Assert.True(paid);
        Assert.Contains(ownTag, f.Host.Deleted);
        Assert.DoesNotContain(innerTag, f.Host.Deleted);
    }

    [Fact]
    public void PayFromContainer_Given_change_made_in_a_pouch_of_the_pack_When_paid_Then_the_client_is_told_by_way_of_the_player()
    {
        // Arrange
        var f = new Fixture();
        CfObject pouch = f.Give(f.Player, "bigpouch", applied: true);
        f.Give(pouch, "silvercoin", 10);

        // Act
        bool paid = f.Pay(4);

        // Assert: the coins that are left (the change) are sent to the player, not to the pouch
        Assert.True(paid);
        Assert.NotEmpty(f.Host.Sent);
        Assert.All(f.Host.Sent, s => Assert.Same(f.Player, s.Who));
        Assert.Equal(6, f.World.QueryMoney(f.Player));
    }

    // ------------------------------------------------------------------ the coin loop of sell_item

    [Fact]
    public void AddCoins_Given_a_pouch_that_weighs_less_with_strength_When_coins_come_Then_its_limit_counts_the_reduced_weight()
    {
        // Arrange: silver weighs 3, a pouch of strength 50 carries it as 1: 4 fit in a pouch that holds 4, the rest goes to the pack
        var f = new Fixture();
        CfObject pouch = f.Give(f.Player, "halfpouch", applied: true);

        // Act
        int left = Money.AddCoins(f.World, f.Create, f.Player, 9);

        // Assert
        Assert.Equal(0, left);
        Assert.Equal(4, f.Count(pouch, "silvercoin"));
        Assert.Equal(5, f.Count(f.Player, "silvercoin"));
    }

    [Fact]
    public void AddCoins_Given_a_pouch_with_no_weight_limit_When_coins_come_Then_all_of_them_go_into_it()
    {
        // Arrange
        var f = new Fixture();
        CfObject pouch = f.Give(f.Player, "bigpouch", applied: true);

        // Act
        int left = Money.AddCoins(f.World, f.Create, f.Player, 120);

        // Assert: 2 platinum, 2 gold and 0 silver of 120 = 100 + 20
        Assert.Equal(0, left);
        Assert.Equal(2, f.Count(pouch, "platinacoin"));
        Assert.Equal(2, f.Count(pouch, "goldcoin"));
        Assert.Equal(0, f.Count(f.Player, "goldcoin"));
    }

    // ------------------------------------------------------------------ mudaddmoney

    [Fact]
    public void AddMoney_Given_the_smallest_amount_When_added_Then_one_silver_coin_comes()
    {
        // Arrange
        var f = new Fixture();

        // Act
        int total = Money.AddMoney(f.World, f.Create, f.Player, 1);

        // Assert
        Assert.Equal(1, total);
        Assert.Equal(1, f.Count(f.Player, "silvercoin"));
    }

    [Fact]
    public void AddMoney_Given_more_than_an_int_When_added_Then_the_amount_is_clamped_not_wrapped()
    {
        // Arrange
        var f = new Fixture();

        // Act
        int total = Money.AddMoney(f.World, f.Create, f.Player, 3_000_000_000L);

        // Assert: int.MaxValue, in coins; a wrapped (negative) amount would give nothing
        Assert.True(total > 2_000_000_000);
    }

    // ------------------------------------------------------------------ mudobjectattr

    [Fact]
    public void Of_Given_an_object_whose_name_is_not_its_archetype_When_asked_Then_archname_is_the_archetype()
    {
        // Arrange
        var f = new Fixture();
        CfObject ripe = f.Make("ripe");
        ripe.Name = "very ripe banana";

        // Act
        string arch = MoneyObjectAttr.Of(f.World, ripe, "archname");

        // Assert
        Assert.Equal("ripe", arch);
    }

    [Fact]
    public void Of_Given_an_attribute_in_capitals_When_asked_Then_it_is_the_same_as_in_small_letters()
    {
        // Arrange
        var f = new Fixture();
        CfObject ripe = f.Make("ripe");
        ripe.Nrof = 3;

        // Act and Assert
        Assert.Equal("3", MoneyObjectAttr.Of(f.World, ripe, "QUANTITY"));
        Assert.Equal("ripe", MoneyObjectAttr.Of(f.World, ripe, "ArchName"));
        Assert.Equal("7", MoneyObjectAttr.Of(f.World, ripe, "Value"));
    }

    [Theory]
    [InlineData("12.7", 12.0)]
    [InlineData("-3.9", -3.0)]
    [InlineData("  15  ", 15.0)]
    [InlineData("nothing", 0.0)]
    [InlineData("1e400", 0.0)]
    public void AmountOf_Given_the_text_of_a_number_When_read_Then_the_fraction_is_cut_off_the_blanks_are_ignored_and_anything_else_is_0(string text, double expected)
    {
        // Act
        double amount = MoneyObjectAttr.AmountOf(text);

        // Assert
        Assert.Equal(expected, amount);
    }
}
