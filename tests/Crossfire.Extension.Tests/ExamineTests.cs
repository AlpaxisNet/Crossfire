// Not ported from a Crossfire file: tests of lane 5C of phase 3 wave 5, examine in the Crossfire extension: the port of server/c_object.c's
// examine, examine_monster, long_desc and command_examine line by line (ExamineUnitTests), and mudexamineinvobject, examine and
// mudexamineobject as a script calls them, with the line as one onRPGMessage event (ExamineTests; docs/mud-phase3.md sections 4.5 and 9).
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using Crossfire.Extension.Gk;
using Crossfire.Extension.Protocol;
using static Crossfire.Extension.Tests.Say;

namespace Crossfire.Extension.Tests;

internal sealed class ExamineHost : CrossfireHost
{
    public List<string> Lines { get; } = new();

    public override void DrawInfo(CfObject who, string text) => Lines.Add(text);

    public override string? SpellName(int index) => index == 7 ? "firebolt" : "";

    public override int SpellLevel(int index) => index == 7 ? 4 : 0;
}

public sealed class ExamineUnitTests
{
    private const string Arc =
        "Object empty_archetype\nend\n"
        + "Object platinacoin\nname platinum coin\nname_pl platinum coins\ntype 36\nweight 1\nvalue 50\nend\n"
        + "Object goldcoin\nname gold coin\nname_pl gold coins\ntype 36\nweight 2\nvalue 10\nend\n"
        + "Object silvercoin\nname silver coin\nname_pl silver coins\ntype 36\nweight 3\nvalue 1\nend\n"
        + "Object rock\nname rock\nname_pl rocks\nweight 3000\nvalue 10\nend\n"
        + "Object cleric_book\nname prayer book\nname_pl prayer books\ntype 85\nweight 100\nend\n"
        + "Object dragon_player\nname dragon\ntype 1\nrace dragon\nlevel 5\nend\n";

    private static (CfWorld W, ExamineHost H, CfObject Pl) Make(int cha = 15)
    {
        var host = new ExamineHost();
        var w = new CfWorld(host);
        w.InitArchetypes(Arc);
        CfObject pl = new() { Type = Define.PLAYER, Name = "bob", Level = 5 };
        pl.Stats.Cha = (sbyte)cha;
        return (w, host, pl);
    }

    private static CfObject Make(CfWorld w, string arch, Action<CfObject>? tweak = null)
    {
        CfObject ob = w.ArchToObject(w.FindArchetype(arch))!;
        ob.Nrof = 1;
        tweak?.Invoke(ob);
        return ob;
    }

    private static List<string> Lines(Action<CfWorld, CfObject, CfObject?> examine, Func<CfWorld, CfObject?> make, int cha = 15)
    {
        var (w, host, pl) = Make(cha);
        examine(w, pl, make(w));
        return host.Lines;
    }

    private static List<string> ExamineOf(Func<CfWorld, CfObject?> make, int cha = 15) =>
        Lines((w, pl, ob) => Examine.ExamineObject(w, pl, ob), make, cha);

    [Fact]
    public void Examine_Given_a_rock_that_is_identified_When_examined_Then_the_name_the_weight_the_price_to_sell_and_a_blank_line()
    {
        // Arrange / Act: Cha 15, value 10: 4 * 10 * (1 - 1.5 / 3.5) = 22
        var lines = ExamineOf(w => Make(w, "rock", o => o.SetFlag(Define.FLAG_IDENTIFIED)));

        // Assert
        Assert.Equal(new[] { "That is rock", "It weighs 3.000 kg.", "You would get 2 gold coins and 2 silver coins for the it.", " " }, lines);
    }

    [Fact]
    public void Examine_Given_a_stack_When_examined_Then_the_plural_forms_of_the_weight_and_the_price_lines()
    {
        // Arrange / Act
        var lines = ExamineOf(w => Make(w, "rock", o => { o.SetFlag(Define.FLAG_IDENTIFIED); o.Nrof = 4; }));

        // Assert: 4 * 40 * (1 - diff) = 91
        Assert.Equal(new[] { "That is rocks", "They weigh 12.000 kg.", "You would get 1 platinum coin, 4 gold coins and 1 silver coin for the them.", " " }, lines);
    }

    [Fact]
    public void Examine_Given_an_unpaid_object_When_examined_Then_what_it_would_cost_the_player()
    {
        // Arrange / Act: 4 * 10 * (1 + diff) = 57
        var lines = ExamineOf(w => Make(w, "rock", o => { o.SetFlag(Define.FLAG_IDENTIFIED); o.SetFlag(Define.FLAG_UNPAID); }));
        var stack = ExamineOf(w => Make(w, "rock", o => { o.SetFlag(Define.FLAG_IDENTIFIED); o.SetFlag(Define.FLAG_UNPAID); o.Nrof = 2; }));

        // Assert
        Assert.Equal("It would cost you 1 platinum coin and 7 silver coins.", lines[2]);
        Assert.Equal("They would cost you 2 platinum coins, 1 gold coin and 4 silver coins.", stack[2]); // 4 * 20 * 1.428571 = 114
    }

    [Fact]
    public void Examine_Given_an_object_of_no_value_or_a_start_equipment_When_examined_Then_there_is_no_price_line()
    {
        // Arrange / Act
        var free = ExamineOf(w => Make(w, "rock", o => o.Value = 0));
        var start = ExamineOf(w => Make(w, "rock", o => o.SetFlag(Define.FLAG_STARTEQUIP)));

        // Assert
        Assert.Equal(new[] { "That is rock", "It weighs 3.000 kg.", " " }, free);
        Assert.Equal(new[] { "That is rock", "It weighs 3.000 kg.", " " }, start);
    }

    [Fact]
    public void Examine_Given_an_object_without_weight_and_a_material_When_examined_Then_no_weight_line_and_the_materials_in_the_order_of_the_table()
    {
        // Arrange / Act
        var lines = ExamineOf(w => Make(w, "rock", o => { o.Weight = 0; o.Value = 0; o.Material = 0b11; }));

        // Assert: the table's first two materials
        Assert.Equal(new[] { "That is rock", "It is made of: " + Tables.Material[0].Name + " " + Tables.Material[1].Name + " ", " " }, lines);
    }

    [Fact]
    public void Examine_Given_an_object_with_a_message_When_examined_Then_the_materials_are_not_listed_and_the_message_is_shown()
    {
        // Arrange / Act
        var lines = ExamineOf(w => Make(w, "rock", o => { o.Weight = 0; o.Value = 0; o.Material = 1; o.Msg = "A runic stone.\n"; }));

        // Assert
        Assert.Equal(new[] { "That is rock", "A runic stone.\n", " " }, lines);
    }

    [Fact]
    public void Examine_Given_an_identified_object_that_needs_identifying_and_has_a_message_When_examined_Then_the_story_line_comes_first()
    {
        // Arrange / Act
        var identified = ExamineOf(w => Make(w, "rock", o => { o.Type = Define.POTION; o.Weight = 0; o.Value = 0; o.Msg = "Brewed in a hurry."; o.SetFlag(Define.FLAG_IDENTIFIED); }));
        var unknown = ExamineOf(w => Make(w, "rock", o => { o.Type = Define.POTION; o.Weight = 0; o.Value = 0; o.Msg = "Brewed in a hurry."; }));

        // Assert
        Assert.Equal(new[] { "The object has a story:", "Brewed in a hurry." }, identified.Skip(1).Take(2));
        Assert.DoesNotContain("The object has a story:", unknown);
        Assert.Contains("Brewed in a hurry.", unknown);

        // an identified object that needs no identifying (a rock) has no story line
        var plain = ExamineOf(w => Make(w, "rock", o => { o.Weight = 0; o.Value = 0; o.Msg = "Brewed in a hurry."; o.SetFlag(Define.FLAG_IDENTIFIED); }));
        Assert.DoesNotContain("The object has a story:", plain);
        Assert.Contains("Brewed in a hurry.", plain);
    }

    [Theory]
    [InlineData("@match", false)]
    [InlineData("@MATCH", false)]
    [InlineData("@matchx", true)]
    [InlineData("@mat", true)]
    [InlineData("@match foo", true)]
    public void Examine_Given_a_message_that_starts_with_a_match_When_examined_Then_only_the_message_that_is_just_the_word_is_left_out(string msg, bool shown)
    {
        // Arrange / Act: strncasecmp(msg, "@match", 7) compares the end of "@match" too
        var lines = ExamineOf(w => Make(w, "rock", o => { o.Weight = 0; o.Value = 0; o.Msg = msg; }));

        // Assert
        Assert.Equal(shown, lines.Contains(msg));
    }

    [Theory]
    [InlineData(Define.EXIT)]
    [InlineData(Define.CORPSE)]
    public void Examine_Given_an_exit_or_a_corpse_with_a_message_When_examined_Then_the_message_is_not_shown(int type)
    {
        // Arrange / Act
        var lines = ExamineOf(w => Make(w, "rock", o => { o.Type = (byte)type; o.Weight = 0; o.Value = 0; o.Msg = "Hidden."; }));

        // Assert
        Assert.DoesNotContain("Hidden.", lines);
    }

    [Fact]
    public void Examine_Given_a_message_on_an_object_that_is_walked_on_When_examined_Then_the_message_is_not_shown()
    {
        // Arrange / Act
        var lines = ExamineOf(w => Make(w, "rock", o => { o.SetFlag(Define.FLAG_WALK_ON); o.Weight = 0; o.Value = 0; o.Msg = "Hidden."; }));

        // Assert
        Assert.DoesNotContain("Hidden.", lines);
    }

    [Fact]
    public void Examine_Given_a_book_When_examined_Then_something_is_written_in_it_and_the_text_is_not_shown()
    {
        // Arrange / Act
        var lines = ExamineOf(w => Make(w, "rock", o => { o.Type = Define.BOOK; o.Weight = 0; o.Value = 0; o.Msg = "Chapter one."; }));
        var empty = ExamineOf(w => Make(w, "rock", o => { o.Type = Define.BOOK; o.Weight = 0; o.Value = 0; }));

        // Assert: a book is an object that needs identifying, so its name is all long_desc has to say
        Assert.Equal(new[] { "That is rock", "Something is written in it.", " " }, lines);
        Assert.Equal(new[] { "That is rock", " " }, empty);
    }

    [Fact]
    public void Examine_Given_containers_When_examined_Then_what_they_hold_and_their_weight_limit_in_kilograms()
    {
        // Arrange / Act: weight_limit / (10 * (100 - Str)): 5000 / (10 * 50) = 10.0
        string[] Of(string? race, int limit, int str) => ExamineOf(w => Make(w, "rock", o => { o.Type = Define.CONTAINER; o.Weight = 0; o.Value = 0; o.Race = race; o.WeightLimit = limit; o.Stats.Str = (sbyte)str; })).Skip(1).Take(1).ToArray();

        // Assert
        Assert.Equal(new[] { "It can hold only gold and its weight limit is 10.0 kg." }, Of("gold", 5000, 50));
        Assert.Equal(new[] { "It can hold only gold." }, Of("gold", 0, 50));
        Assert.Equal(new[] { "It can hold only gold." }, Of("gold", 5000, 100));
        Assert.Equal(new[] { "Its weight limit is 12.5 kg." }, Of(null, 5000, 60));
        Assert.Equal(new[] { " " }, Of(null, 0, 50));
        Assert.Equal(new[] { " " }, Of(null, 5000, 100));
    }

    [Fact]
    public void Examine_Given_a_wand_When_examined_Then_the_charges_only_when_it_is_identified()
    {
        // Arrange / Act
        var known = ExamineOf(w => Make(w, "rock", o => { o.Type = Define.WAND; o.Weight = 0; o.Value = 0; o.Stats.Food = 7; o.SetFlag(Define.FLAG_IDENTIFIED); }));
        var unknown = ExamineOf(w => Make(w, "rock", o => { o.Type = Define.WAND; o.Weight = 0; o.Value = 0; o.Stats.Food = 7; }));

        // Assert
        Assert.Equal("It has 7 charges left.", known[1]);
        Assert.DoesNotContain(unknown, l => l.Contains("charges", StringComparison.Ordinal));
    }

    [Fact]
    public void Examine_Given_a_spell_book_When_examined_Then_the_level_of_its_spell_or_prayer_only_when_it_is_identified()
    {
        // Arrange / Act
        CfObject? Book(CfWorld w, string arch, bool identified, int spell) => Make(w, arch, o => { o.Weight = 0; o.Value = 0; o.Stats.Sp = (short)spell; if (identified) { o.SetFlag(Define.FLAG_IDENTIFIED); } });
        var spell = ExamineOf(w => Book(w, "cleric_book", true, 7)!.Also(b => { b.Arch = w.FindArchetype("rock"); }));
        var prayer = ExamineOf(w => Book(w, "cleric_book", true, 7));
        var unknown = ExamineOf(w => Book(w, "cleric_book", false, 7));
        var none = ExamineOf(w => Book(w, "cleric_book", true, -1));

        // Assert
        Assert.Contains("firebolt is a 4 level spell.", spell);
        Assert.Contains("firebolt is a 4 level prayer.", prayer);
        Assert.DoesNotContain(unknown, l => l.Contains("level", StringComparison.Ordinal));
        Assert.DoesNotContain(none, l => l.Contains("level", StringComparison.Ordinal));
    }

    [Fact]
    public void Examine_Given_an_object_with_no_pretext_When_examined_Then_the_name_alone_without_that_is()
    {
        // Arrange / Act
        var lines = ExamineOf(w => Make(w, "rock", o => { o.SetFlag(Define.FLAG_NO_PRETEXT); o.Weight = 0; o.Value = 0; }));

        // Assert
        Assert.Equal(new[] { "rock", " " }, lines);
    }

    [Fact]
    public void Examine_Given_nothing_or_a_closing_container_When_examined_Then_no_line_at_all()
    {
        // Arrange / Act / Assert
        Assert.Empty(ExamineOf(_ => null));
        Assert.Empty(ExamineOf(w => Make(w, "rock", o => o.Type = Define.CLOSE_CON)));
    }

    [Fact]
    public void Examine_Given_a_name_longer_than_the_buffer_When_examined_Then_the_line_is_cut_at_1023_bytes()
    {
        // Arrange
        string longName = new('x', 3000);

        // Act
        var pretext = ExamineOf(w => Make(w, "rock", o => { o.Name = longName; o.NamePl = longName; o.Weight = 0; o.Value = 0; }));
        var plain = ExamineOf(w => Make(w, "rock", o => { o.Name = longName; o.NamePl = longName; o.Weight = 0; o.Value = 0; o.SetFlag(Define.FLAG_NO_PRETEXT); }));

        // Assert
        Assert.Equal("That is ".Length + 1015, pretext[0].Length);
        Assert.StartsWith("That is xxx", pretext[0], StringComparison.Ordinal);
        Assert.Equal(1023, plain[0].Length);
    }

    [Fact]
    public void LongDesc_Given_a_null_or_an_object_whose_type_has_no_description_When_asked_Then_the_empty_text_or_the_name()
    {
        // Arrange
        var (w, _, _) = Make();

        // Act / Assert
        Assert.Equal("", Examine.LongDesc(w, null));
        Assert.Equal("rock", Examine.LongDesc(w, Make(w, "rock")));
    }

    [Fact]
    public void LongDesc_Given_food_that_does_something_When_asked_Then_the_name_and_describe_item()
    {
        // Arrange
        var (w, _, _) = Make();
        CfObject food = Make(w, "rock", o => { o.Type = Define.FOOD; o.Stats.Food = 30; o.SetFlag(Define.FLAG_IDENTIFIED); });

        // Act
        string text = Examine.LongDesc(w, food);

        // Assert
        Assert.Equal("rock " + w.DescribeItem(food), text);
        Assert.NotEqual("rock ", text);
    }

    // ------------------------------------------------------------------ monsters

    private static CfObject Monster(CfWorld w, int level, int hp, int maxhp, Action<CfObject>? tweak = null) =>
        Make(w, "rock", o =>
        {
            o.SetFlag(Define.FLAG_MONSTER);
            o.Weight = 0;
            o.Value = 0;
            o.Level = (short)level;
            o.Stats.Hp = (short)hp;
            o.Stats.Maxhp = (short)maxhp;
            tweak?.Invoke(o);
        });

    [Theory]
    [InlineData(3, "It is likely less powerful than you.")]
    [InlineData(5, "It is probably as powerful as you.")]
    [InlineData(9, "It is likely more powerful than you.")]
    public void ExamineMonster_Given_a_monster_of_a_level_When_examined_Then_it_is_compared_with_the_player(int level, string expected)
    {
        // Arrange / Act
        var lines = ExamineOf(w => Monster(w, level, 10, 10));

        // Assert: the player's level is 5
        Assert.Equal(new[] { "That is rock", expected, "It is in excellent shape.", " " }, lines);
    }

    [Theory]
    [InlineData(1, 10, "")]
    [InlineData(4, 10, "It is in a bad shape.")]
    [InlineData(6, 10, "It is hurt.")]
    [InlineData(8, 10, "It is somewhat hurt.")]
    [InlineData(10, 10, "It is in excellent shape.")]
    public void ExamineMonster_Given_a_monster_with_less_life_When_examined_Then_the_quarter_it_is_in(int hp, int maxhp, string expected)
    {
        // Arrange / Act: (hp + 1) * 4 / (maxhp + 1) is 1 to 4 (0 is a line C does not have)
        var lines = ExamineOf(w => Monster(w, 5, hp, maxhp));

        // Assert
        Assert.Equal(expected == "" ? new[] { "That is rock", "It is probably as powerful as you.", " " } : new[] { "That is rock", "It is probably as powerful as you.", expected, " " }, lines);
    }

    [Fact]
    public void ExamineMonster_Given_a_monster_with_no_life_range_When_examined_Then_no_life_line_and_no_crash()
    {
        // Arrange / Act: maxhp -1 makes C's division fail
        var lines = ExamineOf(w => Monster(w, 5, 0, -1));

        // Assert
        Assert.Equal(new[] { "That is rock", "It is probably as powerful as you.", " " }, lines);
    }

    [Fact]
    public void ExamineMonster_Given_an_undead_acid_poisoned_monster_When_examined_Then_each_line_in_the_order_of_C()
    {
        // Arrange / Act
        var lines = ExamineOf(w => Monster(w, 5, 10, 10, o =>
        {
            o.SetFlag(Define.FLAG_UNDEAD);
            o.Attacktype |= Define.AT_ACID;
            CfObject poison = Make(w, "rock", p => p.Type = Define.POISONING);
            w.InsertObInOb(poison, o);
        }));

        // Assert
        Assert.Equal(new[]
        {
            "That is rock", "It is an undead force.", "It is probably as powerful as you.", "You seem to smell an acrid odor.",
            "It is in excellent shape.", "It looks very ill.", " ",
        }, lines);
    }

    [Fact]
    public void ExamineMonster_Given_the_part_of_a_larger_monster_When_examined_Then_the_head_is_judged()
    {
        // Arrange
        var (w, host, pl) = Make();
        CfObject head = Monster(w, 9, 10, 10);
        CfObject part = Monster(w, 1, 1, 10, o => o.Head = head);

        // Act
        Examine.ExamineMonster(w, pl, part);

        // Assert
        Assert.Equal(new[] { "It is likely more powerful than you.", "It is in excellent shape." }, host.Lines);
    }

    // ------------------------------------------------------------------ flesh and dragons

    [Fact]
    public void Examine_Given_flesh_with_a_resistance_When_a_dragon_and_a_man_examine_it_Then_only_the_dragon_is_told_and_the_object_is_left_as_it_was()
    {
        // Arrange
        var (w, host, _) = Make();
        CfObject dragon = Make(w, "dragon_player");
        CfObject man = new() { Type = Define.PLAYER, Name = "bob", Level = 5 };
        CfObject flesh = Make(w, "rock", o => { o.Type = Define.FLESH; o.Weight = 0; o.Value = 0; o.Resist[Define.ATNR_FIRE] = 30; o.SetFlag(Define.FLAG_IDENTIFIED); });

        // Act
        Examine.ExamineObject(w, man, flesh);
        string[] forMan = host.Lines.ToArray();
        host.Lines.Clear();
        Examine.ExamineObject(w, dragon, flesh);
        string[] forDragon = host.Lines.ToArray();

        // Assert
        Assert.DoesNotContain("resist", forMan[0], StringComparison.Ordinal);
        Assert.Contains("resist", forDragon[0], StringComparison.Ordinal);
        Assert.False(flesh.QueryFlag(Define.FLAG_SEE_INVISIBLE));
    }

    // ------------------------------------------------------------------ command_examine

    [Fact]
    public void CommandExamine_Given_parameters_that_match_an_item_When_called_Then_the_item_is_examined_and_the_result_is_0()
    {
        // Arrange
        var (w, host, pl) = Make();
        w.InsertObInOb(Make(w, "rock", o => { o.Weight = 0; o.Value = 0; }), pl);
        pl.Contr = null;

        // Act
        int result = Examine.CommandExamine(w, pl, "rock");

        // Assert
        Assert.Equal(0, result);
        Assert.Equal(new[] { "That is rock", " " }, host.Lines);
    }

    [Fact]
    public void CommandExamine_Given_parameters_that_match_nothing_When_called_Then_the_line_that_C_writes()
    {
        // Arrange
        var (w, host, pl) = Make();

        // Act
        int result = Examine.CommandExamine(w, pl, "sword of doom");

        // Assert
        Assert.Equal(0, result);
        Assert.Equal(new[] { "Could not find an object that matches sword of doom" }, host.Lines);
    }

    [Fact]
    public void CommandExamine_Given_no_parameters_When_called_Then_the_first_visible_object_below_the_player_that_is_not_a_player()
    {
        // Arrange: a player stands in no level here; the objects below him are a linked list
        var (w, host, pl) = Make();
        CfObject hidden = Make(w, "rock", o => { o.Invisible = 1; o.Name = "hidden"; });
        CfObject other = new() { Type = Define.PLAYER, Name = "other" };
        CfObject seen = Make(w, "rock", o => { o.Weight = 0; o.Value = 0; o.Name = "seen"; o.NamePl = "seen"; });
        pl.Below = other;
        other.Below = hidden;
        hidden.Below = seen;

        // Act
        Examine.CommandExamine(w, pl, null);
        host.Lines.Add("--");
        pl.Below = null;
        Examine.CommandExamine(w, pl, null);

        // Assert
        Assert.Equal(new[] { "That is seen", " ", "--" }, host.Lines);
    }
}

internal static class ExamineExtensions
{
    public static T Also<T>(this T value, Action<T> action)
    {
        action(value);
        return value;
    }
}

public sealed class ExamineTests
{
    private static ExtensionOptions Options() => new()
    {
        Writer = new FileWriterOptions { Quiet = TimeSpan.FromMilliseconds(20), MaxDelay = TimeSpan.FromMilliseconds(100) },
        ExtraManifest = ItemWorld.Register,
    };

    private static DirectExt Start(TempFolder world, long protoMinor = 1) => new(world, Options(), 1, true, 16_777_216, protoMinor);

    private static ulong Login(DirectExt ext, string account = "bob") => Ok(ext.Call(5, new[] { S(account) }, player: account));

    private static List<ulong> Items(DirectExt ext, ulong player) =>
        ext.Call(12, new[] { H(player), Val.Int(Ply("muditems")), Val.List() }).Result.Items.Select(row => row.Items[0].AsHandle).ToList();

    private static ulong ItemOf(DirectExt ext, ulong player, string archname) => Items(ext, player).Single(h => Get(ext, h, Obj("archname")).AsString == archname);

    private static List<string> EventLines(ReplyFrame r) => r.Events.Where(e => e.Get("name")!.AsString == "onRPGMessage").Select(e => e.Get("args")!.Items[0].AsString).ToList();

    private static int ItemMethod(string name) =>
        (int)Manifest.Build(false, 1).Get("types")!.Items.Single(t => t.Get("name")!.AsString == "TMudObject").Get("methods")!.Items.Single(m => m.Get("name")!.AsString == name).Get("id")!.AsInt;

    private const string BananaStack = "arch banana\nnrof 4\nidentified 1\nend\n";

    [Fact]
    public void MudExamineInvObject_Given_an_item_of_the_player_When_called_Then_one_event_per_line_with_the_text_fallback_and_true()
    {
        // Arrange
        using DirectExt ext = Start(MoneyWorld.Make("bob", BananaStack));
        ulong bob = Login(ext);
        long id = (uint)ItemOf(ext, bob, "banana");

        // Act
        ReplyFrame r = ext.Call(Manifest.OpMudExamineInvObject, new[] { Val.Int(id) }, player: "bob");

        // Assert
        Assert.Equal(0UL, r.Status);
        Assert.True(r.Result.AsBool);
        List<string> lines = EventLines(r);
        Assert.Equal(r.Events.Count, lines.Count);
        Assert.StartsWith("That is bananas", lines[0], StringComparison.Ordinal);
        Assert.Equal("They weigh 0.400 kg.", lines[1]);
        Assert.StartsWith("You would get ", lines[2], StringComparison.Ordinal);
        Assert.EndsWith(" for the them.", lines[2], StringComparison.Ordinal);
        Assert.Equal(" ", lines[^1]);
        Assert.Equal(4, lines.Count);
        Assert.All(r.Events, e =>
        {
            Assert.Equal(new[] { "name", "target", "player", "args", "fallback" }, e.Pairs.Select(p => p.Key));
            Assert.Equal("client.text", e.Get("fallback")!.Get("k")!.AsString);
            Assert.Equal(e.Get("args")!.Items[0].AsString, e.Get("fallback")!.Get("text")!.AsString);
            Assert.Equal("bob", e.Get("player")!.AsString);
        });
        Assert.Empty(Effects(r, "client.text"));
    }

    [Fact]
    public void MudExamineInvObject_Given_a_1_0_link_When_called_Then_the_client_text_effects_and_no_event()
    {
        // Arrange
        using DirectExt ext = Start(MoneyWorld.Make("bob", BananaStack), protoMinor: 0);
        ulong bob = Login(ext);
        long id = (uint)ItemOf(ext, bob, "banana");

        // Act
        ReplyFrame r = ext.Call(Manifest.OpMudExamineInvObject, new[] { Val.Int(id) }, player: "bob");

        // Assert
        Assert.Empty(r.Events);
        Assert.Equal(4, Effects(r, "client.text").Count);
        Assert.Equal(" ", Effects(r, "client.text")[^1].Get("text")!.AsString);
    }

    [Fact]
    public void MudExamineInvObject_Given_an_item_in_a_sack_When_called_Then_it_is_found_by_its_id_in_the_players_tree()
    {
        // Arrange
        using DirectExt ext = Start(MoneyWorld.Make("bob", "arch sack\narch banana\nnrof 1\nidentified 1\nend\nend\n"));
        ulong bob = Login(ext);
        ulong sack = ItemOf(ext, bob, "sack");
        ulong inside = ext.Call(12, new[] { H(sack), Val.Int(Obj("muditems")), Val.List() }).Result.Items[0].Items[0].AsHandle;

        // Act
        ReplyFrame r = ext.Call(Manifest.OpMudExamineInvObject, new[] { Val.Int((uint)inside) }, player: "bob");

        // Assert
        Assert.True(r.Result.AsBool);
        Assert.StartsWith("That is banana", EventLines(r)[0], StringComparison.Ordinal);
    }

    [Fact]
    public void MudExamineInvObject_Given_an_id_that_is_not_an_item_of_the_players_tree_When_called_Then_false_and_no_line()
    {
        // Arrange
        TempFolder world = MoneyWorld.Make("bob", BananaStack, hall: true);
        ItemWorld.Account(world, "alice", "", "arch sword\nnrof 1\nend\n");
        using DirectExt ext = Start(world);
        Login(ext, "bob");
        ulong alice = Login(ext, "alice");
        long alices = (uint)ItemOf(ext, alice, "sword");
        var (places, _) = LoadAll(ext, "hall.nw");
        long levelObject = (uint)places[0].Get("handle")!.AsHandle;

        // Act / Assert
        foreach (long id in new[] { alices, levelObject, 0, -5, 123_456, 4_294_967_296 })
        {
            ReplyFrame r = ext.Call(Manifest.OpMudExamineInvObject, new[] { Val.Int(id) }, player: "bob");
            Assert.Equal(0UL, r.Status);
            Assert.False(r.Result.AsBool);
            Assert.Empty(r.Events);
        }
        Assert.Equal(0L, ext.Call(15).Result.Get("examine_lines")!.AsInt);
    }

    [Fact]
    public void MudExamineInvObject_Given_no_player_in_the_context_or_a_bad_argument_When_called_Then_bad_args()
    {
        // Arrange
        using DirectExt ext = Start(MoneyWorld.Make("bob", BananaStack));
        Login(ext);

        // Act / Assert
        Assert.Equal((ulong)Status.BadArgs, ext.Call(Manifest.OpMudExamineInvObject, new[] { Val.Int(1) }).Status);
        Assert.Equal((ulong)Status.BadArgs, ext.Call(Manifest.OpMudExamineInvObject, new[] { S("x") }, player: "bob").Status);
        Assert.Equal((ulong)Status.BadArgs, ext.Call(Manifest.OpMudExamineInvObject, Array.Empty<Val>(), player: "bob").Status);
    }

    [Fact]
    public void Examine_Given_the_methods_examine_and_mudexamineobject_on_an_item_When_called_Then_the_same_lines_as_mudexamineinvobject()
    {
        // Arrange
        using DirectExt ext = Start(MoneyWorld.Make("bob", BananaStack));
        ulong bob = Login(ext);
        ulong banana = ItemOf(ext, bob, "banana");
        List<string> expected = EventLines(ext.Call(Manifest.OpMudExamineInvObject, new[] { Val.Int((uint)banana) }, player: "bob"));

        // Act
        ReplyFrame viaExamine = ext.Call(13, new[] { H(banana), Val.Int(ItemMethod("examine")), Val.List() }, player: "bob");
        ReplyFrame viaMud = ext.Call(13, new[] { H(banana), Val.Int(ItemMethod("mudexamineobject")), Val.List() }, player: "bob");

        // Assert
        Assert.Equal(expected, EventLines(viaExamine));
        Assert.Equal(expected, EventLines(viaMud));
        Assert.Equal(12L, ext.Call(15).Result.Get("examine_lines")!.AsInt);
    }

    [Fact]
    public void Examine_Given_an_object_of_a_level_When_the_player_of_the_context_examines_it_Then_the_lines_go_to_that_player_with_the_unpaid_price()
    {
        // Arrange: the seed hall's sword is unpaid, value 50, with a two line message
        using DirectExt ext = Start(MoneyWorld.Make("bob", "", hall: true));
        Login(ext);
        var (places, _) = LoadAll(ext, "hall.nw");
        ulong sword = places[1].Get("handle")!.AsHandle;

        // Act
        ReplyFrame r = ext.Call(13, new[] { H(sword), Val.Int(ItemMethod("examine")), Val.List() }, player: "bob");

        // Assert
        List<string> lines = EventLines(r);
        Assert.StartsWith("That is sword", lines[0], StringComparison.Ordinal);
        Assert.Contains("It weighs 3.000 kg.", lines);
        Assert.Contains(lines, l => l.StartsWith("It would cost you ", StringComparison.Ordinal));
        Assert.Contains("A fine sword.\nSecond line.\n", lines);
        Assert.All(r.Events, e => Assert.Equal("bob", e.Get("player")!.AsString));
    }

    [Fact]
    public void Examine_Given_no_player_in_the_context_or_an_argument_When_called_Then_bad_args_and_no_line()
    {
        // Arrange
        using DirectExt ext = Start(MoneyWorld.Make("bob", BananaStack));
        ulong bob = Login(ext);
        ulong banana = ItemOf(ext, bob, "banana");

        // Act
        ReplyFrame none = ext.Call(13, new[] { H(banana), Val.Int(ItemMethod("examine")), Val.List() });
        ReplyFrame arg = ext.Call(13, new[] { H(banana), Val.Int(ItemMethod("examine")), Val.List(Val.Int(1)) }, player: "bob");

        // Assert
        Assert.Equal((ulong)Status.BadArgs, none.Status);
        Assert.Equal((ulong)Status.BadArgs, arg.Status);
        Assert.Empty(none.Events);
        Assert.Empty(arg.Events);
    }

    [Fact]
    public void Examine_Given_lines_of_examine_and_other_lines_When_stats_are_read_Then_only_the_lines_of_examine_are_in_examine_lines()
    {
        // Arrange
        using DirectExt ext = Start(MoneyWorld.Make("bob", BananaStack));
        ulong bob = Login(ext);
        long id = (uint)ItemOf(ext, bob, "banana");
        ext.Call(ItemWorld.OpDo, new[] { S("draw"), S("bob"), S("hello"), Val.Nil }, player: "bob");

        // Act
        ext.Call(Manifest.OpMudExamineInvObject, new[] { Val.Int(id) }, player: "bob");
        ext.Call(Manifest.OpMudExamineInvObject, new[] { Val.Int(id) }, player: "bob");
        ext.Call(ItemWorld.OpDo, new[] { S("draw"), S("bob"), S("again"), Val.Nil }, player: "bob");

        // Assert
        Assert.Equal(8L, ext.Call(15).Result.Get("examine_lines")!.AsInt);
    }

    [Fact]
    public void Examine_Given_the_extension_is_busy_When_called_Then_it_runs_because_it_changes_nothing()
    {
        // Arrange
        using DirectExt ext = Start(MoneyWorld.Make("bob", BananaStack));
        ulong bob = Login(ext);
        long id = (uint)ItemOf(ext, bob, "banana");
        ext.Dispatcher.Busy = "saving";

        // Act
        ReplyFrame r = ext.Call(Manifest.OpMudExamineInvObject, new[] { Val.Int(id) }, player: "bob");

        // Assert
        Assert.Equal(0UL, r.Status);
        Assert.True(r.Result.AsBool);
    }

    [Fact]
    public void Examine_Given_an_unidentified_potion_When_examined_before_and_after_mudidentify_Then_the_name_and_the_story_line_change()
    {
        // Arrange
        using DirectExt ext = Start(MoneyWorld.Make("bob", "arch potion\nname mystery brew\nnrof 1\nmsg\nSmells of pine.\nendmsg\nend\n"));
        ulong bob = Login(ext);
        ulong potion = ItemOf(ext, bob, "potion");
        long id = (uint)potion;

        // Act
        List<string> before = EventLines(ext.Call(Manifest.OpMudExamineInvObject, new[] { Val.Int(id) }, player: "bob"));
        ext.Call(13, new[] { H(potion), Val.Int(ItemMethod("mudidentify")), Val.List() }, player: "bob");
        List<string> after = EventLines(ext.Call(Manifest.OpMudExamineInvObject, new[] { Val.Int(id) }, player: "bob"));

        // Assert
        Assert.Equal("That is mystery brew", before[0]);
        Assert.DoesNotContain("The object has a story:", before);
        Assert.Equal("That is potion", after[0]);
        Assert.Contains("The object has a story:", after);
    }
}
