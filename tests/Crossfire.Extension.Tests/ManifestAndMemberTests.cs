// Not ported from a Crossfire file: tests of the manifest (docs/protocol.md section 6), the members of TMudArch (docs/extension.md), the
// extension.json beside the executable, a real child process and, when CROSSFIRE_GK_ARCH names Graal Kingdoms' Arch folder, the
// archetypes of that world.
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using System.Diagnostics;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.RegularExpressions;
using Crossfire.Extension.Gk;
using Crossfire.Extension.Protocol;

namespace Crossfire.Extension.Tests;

/// <summary>Runs a test only when CROSSFIRE_GK_ARCH names Graal Kingdoms' Arch folder (not part of this repository).</summary>
public sealed class GkFactAttribute : FactAttribute
{
    public const string Variable = "CROSSFIRE_GK_ARCH";

    public static string ArchFolder => Environment.GetEnvironmentVariable(Variable) ?? "";

    public GkFactAttribute()
    {
        if (ArchFolder.Length == 0 || !Directory.Exists(ArchFolder))
        {
            Skip = "Set " + Variable + " to the Arch folder of Graal Kingdoms' world to run this test.";
        }
    }
}

public class ManifestAndMemberTests
{
    private static readonly Regex Name = new("^[A-Za-z_][A-Za-z0-9_]*$");

    private static string RepoFile(string relative)
    {
        for (string? dir = AppContext.BaseDirectory; dir != null; dir = Path.GetDirectoryName(dir))
        {
            string file = Path.Combine(dir, relative);
            if (File.Exists(file))
            {
                return file;
            }
        }
        throw new FileNotFoundException(relative);
    }

    // ------------------------------------------------------------------ the manifest (section 6)

    [Theory]
    [InlineData("TMudObject", "mudgraaltype", "npctype")]
    [InlineData("TMudObject", "mudgraalparam", "npcparam")]
    [InlineData("TMudObject", "mudcharm", "mudcharisma")]
    [InlineData("TMudPlayer", "mudcharm", "mudcharisma")]
    public void Build_Given_two_writable_members_of_one_field_When_listed_Then_at_most_one_is_stable(string type, string a, string b)
    {
        // Arrange
        Val t = Manifest.Build().Get("types")!.Items.Single(x => x.Get("name")!.AsString == type);

        // Act
        Val ma = t.Get("members")!.Items.Single(m => m.Get("name")!.AsString == a);
        Val mb = t.Get("members")!.Items.Single(m => m.Get("name")!.AsString == b);

        // Assert (protocol 6.1: a stable member changes only through a set of that same member)
        Assert.False(ma.Get("stable")!.AsBool && mb.Get("stable")!.AsBool);
    }


    [Fact]
    public void Build_Given_the_manifest_When_checked_against_section_6_Then_it_is_valid()
    {
        // Arrange
        Val manifest = Manifest.Build();

        // Act
        var functions = manifest.Get("functions")!.Items;
        var types = manifest.Get("types")!.Items;
        var typeNames = types.Select(t => t.Get("name")!.AsString).ToList();
        var ids = new List<long>();

        // Assert
        Assert.Equal(functions.Select(f => f.Get("id")!.AsInt).Distinct().Count(), functions.Count);
        foreach (Val f in functions)
        {
            Assert.InRange(f.Get("id")!.AsInt, 64, int.MaxValue);
            AssertName(f.Get("name")!.AsString);
            foreach (Val p in f.Get("params")!.Items)
            {
                AssertTypeName(p.AsString, typeNames, isReturn: false);
            }
            AssertTypeName(f.Get("ret")!.AsString, typeNames, isReturn: true);
        }
        Assert.Equal(typeNames.Distinct(StringComparer.OrdinalIgnoreCase).Count(), typeNames.Count);
        foreach (Val t in types)
        {
            Assert.InRange(t.Get("id")!.AsInt, 1, int.MaxValue);
            AssertName(t.Get("name")!.AsString);
            Assert.Contains(t.Get("host")!.AsString, new[] { "object", "npc", "player" });
            var memberNames = new List<string>();
            foreach (Val m in t.Get("members")!.Items)
            {
                ids.Add(m.Get("id")!.AsInt);
                memberNames.Add(m.Get("name")!.AsString);
                AssertName(m.Get("name")!.AsString);
                AssertTypeName(m.Get("type")!.AsString, typeNames, isReturn: false);
                Assert.Contains(m.Get("access")!.AsString, new[] { "r", "rw" });
                Assert.Contains(m.Get("stable")!.Kind, new[] { ValKind.True, ValKind.False });
                if (t.Get("name")!.AsString == "TMudArch")
                {
                    Assert.Equal(ValKind.True, m.Get("stable")!.Kind);
                    Assert.Equal("r", m.Get("access")!.AsString);
                }
            }
            Assert.Equal(memberNames.Distinct(StringComparer.OrdinalIgnoreCase).Count(), memberNames.Count);
            var methodNames = new List<string>();
            foreach (Val m in t.Get("methods")!.Items)
            {
                ids.Add(m.Get("id")!.AsInt);
                methodNames.Add(m.Get("name")!.AsString);
                AssertName(m.Get("name")!.AsString);
                foreach (Val p in m.Get("params")!.Items)
                {
                    AssertTypeName(p.AsString, typeNames, isReturn: false);
                }
                AssertTypeName(m.Get("ret")!.AsString, typeNames, isReturn: true);
            }
            Assert.Equal(memberNames.Concat(methodNames).Distinct(StringComparer.OrdinalIgnoreCase).Count(), memberNames.Count + methodNames.Count);
        }
        Assert.All(ids, id => Assert.InRange(id, 1, int.MaxValue));
        Assert.Equal(ids.Distinct().Count(), ids.Count);
        Assert.Equal(new[] { "TMudArch", "TMudObject", "TMudPlayer" }, typeNames);
        Assert.Equal(new[] { "object", "npc", "player" }, types.Select(t => t.Get("host")!.AsString));
        var events = manifest.Get("events")!.Items;
        Assert.Equal(events.Select(e => e.Get("name")!.AsString).Distinct().Count(), events.Count);
        Assert.All(events, e => AssertName(e.Get("name")!.AsString));
        Assert.All(events, e => Assert.Contains(e.Get("target")!.AsString, new[] { "object", "player", "global" }));
        Assert.All(events, e => Assert.Contains(e.Get("mode")!.AsString, new[] { "sync", "deferred" }));
        Assert.Equal(256L, manifest.Get("limits")!.Get("max_list")!.AsInt);
        Assert.Equal(ValKind.False, manifest.Get("wants_tick")!.Kind);
    }

    private static void AssertName(string name)
    {
        Assert.Matches(Name, name);
        Assert.True(name.Length <= 64, name);
    }

    private static void AssertTypeName(string type, List<string> typeNames, bool isReturn)
    {
        if (type.StartsWith("handle:", StringComparison.Ordinal))
        {
            Assert.Contains(type.Substring(7), typeNames);
        }
        else if (type.StartsWith("list:", StringComparison.Ordinal))
        {
            AssertTypeName(type.Substring(5), typeNames, isReturn: false);
        }
        else
        {
            Assert.Contains(type, new[] { "int", "float", "bool", "string", "any", "list" }.Concat(isReturn ? new[] { "void" } : Array.Empty<string>()));
        }
    }

    [Fact]
    public void Build_Given_the_manifest_When_encoded_in_a_hello_and_decoded_Then_it_is_equal_as_section_6_compares()
    {
        // Arrange
        Val hello = Manifest.Hello(Manifest.Build());
        using var stream = new MemoryStream(FrameCodec.Encode(new HelloFrame(hello)));

        // Act
        var back = (HelloFrame)FrameCodec.Read(stream, FrameCodec.InitialMaxFrame)!;

        // Assert
        Assert.True(Val.Same(Manifest.Build(), back.Hello.Get("manifest")!));
        Assert.True(Val.Same(hello, back.Hello));
    }

    [Fact]
    public void Build_Given_the_four_functions_When_read_Then_the_ids_names_and_types_of_the_task()
    {
        // Arrange / Act
        var fs = Manifest.Build().Get("functions")!.Items;

        // Assert
        Assert.Equal(new[] { 64L, 65L, 66L, 67L }, fs.Select(f => f.Get("id")!.AsInt));
        Assert.Equal(new[] { "findarchetype", "findarchetypebyrealname", "mudaddobject", "mudaddspecialobject" }, fs.Select(f => f.Get("name")!.AsString));
        Assert.All(fs.Take(2), f => Assert.Equal("handle:TMudArch", f.Get("ret")!.AsString));
        Assert.All(fs.Take(2), f => Assert.Equal(new[] { "string" }, f.Get("params")!.Items.Select(p => p.AsString)));
        Assert.All(fs.Skip(2), f => Assert.Equal("handle:TMudObject", f.Get("ret")!.AsString));
        Assert.Equal(new[] { "float", "float", "string" }, fs[2].Get("params")!.Items.Select(p => p.AsString));
        Assert.Equal(new[] { "float", "float", "int", "string", "string" }, fs[3].Get("params")!.Items.Select(p => p.AsString));
    }

    [Fact]
    public void Parse_Given_a_hello_with_a_float_epoch_and_proto_major_When_parsed_Then_numbers_are_converted()
    {
        // Arrange
        Val hello = Val.Map(ScriptedHost.HostHello("/w").Pairs.Select(p => p.Key is "epoch" or "proto_major" ? new KeyValuePair<string, Val>(p.Key, Val.Float(p.Key == "epoch" ? 9.9 : 1.0)) : p));

        // Act
        HostHello parsed = HostHello.Parse(hello);

        // Assert
        Assert.Equal(9L, parsed.Epoch);
        Assert.Equal(1L, HostHello.ProtoMajor(hello));
    }

    [Fact]
    public void Parse_Given_a_hello_whose_string_key_has_another_type_When_parsed_Then_a_protocol_error()
    {
        // Arrange
        Val hello = Val.Map(ScriptedHost.HostHello("/w").Pairs.Select(p => p.Key == "world_dir" ? new KeyValuePair<string, Val>(p.Key, Val.Int(3)) : p));

        // Act / Assert
        Assert.Throws<ProtocolException>(() => HostHello.Parse(hello));
    }

    // ------------------------------------------------------------------ the members

    private static CfWorld World(string arc)
    {
        var w = new CfWorld(new GkHost(), (_, _) => { });
        w.InitArchetypes(arc);
        return w;
    }

    private static Val Read(CfWorld w, string archetype, string member)
    {
        Archetype at = w.FindArchetype(archetype)!;
        return TMudArchMembers.Members.Single(m => m.Name == member).Read(w, at);
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("blank.111", "")]
    [InlineData("empty.111", "")]
    [InlineData("banana", "mud_banana.png")]
    [InlineData("pl_dragon.131", "mud_pl_dragon.png")]
    [InlineData("store_weap.211", "mud_store_weap.png")]
    [InlineData("sword.", "mud_sword..png")]
    [InlineData("sword.a1", "mud_sword.a1.png")]
    [InlineData(".5", "mud_.5.png")]
    [InlineData("khat9.png", "khat9.png")]
    [InlineData("a.1.2", "mud_a.1.png")]
    public void Icon_Given_a_face_name_When_derived_Then_the_rule_of_section_4_5(string? face, string expected)
    {
        // Arrange / Act
        string icon = TMudArchMembers.Icon(face);

        // Assert
        Assert.Equal(expected, icon);
    }

    [Theory]
    [InlineData(null, 0)]
    [InlineData("", 0)]
    [InlineData("one", 1)]
    [InlineData("one\n", 1)]
    [InlineData("one\ntwo\n", 2)]
    [InlineData("one\n\nthree", 3)]
    public void Lines_Given_a_message_When_split_Then_the_empty_piece_after_the_last_newline_is_dropped(string? msg, int count)
    {
        // Arrange / Act
        Val lines = TMudArchMembers.Lines(msg);

        // Assert
        Assert.Equal(count, lines.Items.Count);
    }

    [Theory]
    [InlineData("nopick", "no_pick")]
    [InlineData("nodamage", "no_damage")]
    [InlineData("applied", "applied")]
    [InlineData("undead", "undead")]
    [InlineData("scared", "scared")]
    [InlineData("flying", "flying")]
    [InlineData("blind", "is_blind")]
    [InlineData("confused", "confused")]
    [InlineData("cursed", "cursed")]
    [InlineData("damned", "damned")]
    [InlineData("identified", "identified")]
    [InlineData("unpaid", "unpaid")]
    [InlineData("startequip", "startequip")]
    [InlineData("alive", "alive")]
    [InlineData("ismudmonster", "monster")]
    public void Read_Given_an_archetype_with_one_flag_When_read_Then_only_that_flag_member_is_true(string member, string key)
    {
        // Arrange
        string[] flagMembers = { "nopick", "nodamage", "applied", "undead", "scared", "flying", "blind", "confused", "cursed", "damned", "identified", "unpaid", "startequip", "alive", "ismudmonster" };
        CfWorld w = World("Object one\n" + key + " 1\nend\n");

        // Act
        string[] trueOnes = flagMembers.Where(f => Read(w, "one", f).AsBool).ToArray();

        // Assert
        Assert.Equal(new[] { member }, trueOnes);
    }

    [Fact]
    public void Read_Given_an_archetype_whose_object_key_differs_from_its_name_When_names_read_Then_archname_and_mudname_are_the_key_and_mudrealname_the_name()
    {
        // Arrange
        CfWorld w = World("Object goblin_x\nname Goblin Chief\nend\n");

        // Act / Assert
        Assert.Equal("goblin_x", Read(w, "goblin_x", "archname").AsString);
        Assert.Equal("goblin_x", Read(w, "goblin_x", "mudname").AsString);
        Assert.Equal("Goblin Chief", Read(w, "goblin_x", "mudrealname").AsString);
    }

    [Fact]
    public void Read_Given_an_applied_archetype_When_the_full_names_are_read_Then_mudfullname_has_no_state_words_and_fullname_has_them()
    {
        // Arrange
        CfWorld w = World("Object force\nname force\napplied 1\nend\n");

        // Act
        string mudfullname = Read(w, "force", "mudfullname").AsString;
        string fullname = Read(w, "force", "fullname").AsString;

        // Assert
        Assert.Equal("force", mudfullname);
        Assert.Equal("force (applied)", fullname);
    }

    [Fact]
    public void Read_Given_an_archetype_with_attack_and_flag_fields_When_read_Then_the_clones_values()
    {
        // Arrange
        CfWorld w = World("Object ring\nname ring\ntitle of luck\nrace gold\nslaying key1\nmaterial 2\nmagic 3\nluck 2\nattacktype 9\nStr 1\nDex 2\nCon 3\nWis 4\nInt 5\nPow 6\nCha 7\n"
            + "exp 11\nfood 12\nsp 13\nmaxsp 14\ngrace 15\nmaxgrace 16\nac 4\nwc -1\nnrof 5\nno_pick 1\nno_damage 1\napplied 1\nundead 1\nscared 1\nflying 1\nis_blind 1\nconfused 1\n"
            + "cursed 1\ndamned 1\nidentified 1\nunpaid 1\nstartequip 1\nother_arch sword\ncastingtime 3\nrestrictaccess guild:Staff\nspbuff 2\ngracebuff 3\nend\nObject sword\nend\n");

        // Act / Assert
        Assert.Equal("of luck", Read(w, "ring", "mudtitle").AsString);
        Assert.Equal("gold", Read(w, "ring", "mudrace").AsString);
        Assert.Equal("key1", Read(w, "ring", "mudslaying").AsString);
        Assert.Equal(2L, Read(w, "ring", "mudmaterial").AsInt);
        Assert.Equal(3L, Read(w, "ring", "mudmagic").AsInt);
        Assert.Equal(2L, Read(w, "ring", "mudluck").AsInt);
        Assert.Equal(9L, Read(w, "ring", "mudattacktype").AsInt);
        Assert.Equal(new long[] { 1, 2, 3, 4, 5, 6, 7 }, new[] { "mudstrength", "muddexterity", "mudconstitution", "mudwisdom", "mudintelligence", "mudpower", "mudcharm" }.Select(n => Read(w, "ring", n).AsInt));
        Assert.Equal(7L, Read(w, "ring", "mudcharisma").AsInt);
        Assert.Equal(new long[] { 11, 12, 13, 14, 15, 16, 4, -1, 5, 5 }, new[] { "mudexp", "mudfood", "mudsp", "mudmaxsp", "mudgrace", "mudmaxgrace", "mudac", "mudwc", "mudquantity", "mudnrof" }.Select(n => Read(w, "ring", n).AsInt));
        foreach (string flag in new[] { "nopick", "nodamage", "applied", "undead", "scared", "flying", "blind", "confused", "cursed", "damned", "identified", "unpaid", "startequip" })
        {
            Assert.True(Read(w, "ring", flag).AsBool, flag);
            Assert.False(Read(w, "sword", flag).AsBool, flag);
        }
        Assert.Equal("sword", Read(w, "ring", "otherarchname").AsString);
        Assert.Equal("", Read(w, "sword", "otherarchname").AsString);
        Assert.Equal(3L, Read(w, "ring", "castingtime").AsInt);
        Assert.Equal("guild:Staff", Read(w, "ring", "restrictaccess").AsString);
        Assert.Equal(2L, Read(w, "ring", "mudspbuff").AsInt);
        Assert.Equal(3L, Read(w, "ring", "mudgracebuff").AsInt);
        Assert.Equal(13L, Read(w, "ring", "mudsp").AsInt);
        Assert.Equal(0L, Read(w, "sword", "mudcarrying").AsInt);
    }

    [Fact]
    public void Read_Given_an_archetype_without_a_face_a_name_or_a_message_When_read_Then_empty_values()
    {
        // Arrange
        CfWorld w = World("Object bare\nend\n");

        // Act / Assert
        Assert.Equal("", Read(w, "bare", "icon").AsString);
        Assert.Equal("bare", Read(w, "bare", "mudrealname").AsString);
        Assert.Empty(Read(w, "bare", "mudmessage").Items);
        Assert.Equal("bare", Read(w, "bare", "mudname").AsString);
    }

    [Fact]
    public void Read_Given_a_nopeaceful_line_of_zero_When_read_Then_false()
    {
        // Arrange
        CfWorld w = World("Object calm\nnopeaceful 0\nend\n");

        // Act / Assert
        Assert.False(Read(w, "calm", "nopeaceful").AsBool);
    }

    [Fact]
    public void Find_Given_an_id_outside_the_table_or_outside_int_When_looked_up_Then_null()
    {
        // Arrange / Act / Assert
        Assert.Null(TMudArchMembers.Find(99));
        Assert.Null(TMudArchMembers.Find(100L + TMudArchMembers.Members.Count));
        Assert.Null(TMudArchMembers.Find(long.MaxValue));
        Assert.Null(TMudArchMembers.Find(long.MinValue));
        Assert.Null(TMudArchMembers.Find(4_294_967_296L + 100));
        Assert.Null(TMudArchMembers.Find(-4_294_967_296L + 100));
        Assert.NotNull(TMudArchMembers.Find(100));
        Assert.NotNull(TMudArchMembers.Find(99L + TMudArchMembers.Members.Count));
    }

    [Fact]
    public void Members_Given_the_table_When_compared_with_docs_extension_md_Then_ids_names_and_types_are_the_same()
    {
        // Arrange
        string doc = File.ReadAllText(RepoFile(Path.Combine("docs", "extension.md")));
        var rows = Regex.Matches(doc, @"^\| (?<id>\d+) \| `(?<name>[a-z]+)` \| (?<type>[a-z:]+) \| .+ \| .+ \|$", RegexOptions.Multiline)
            .Select(m => (Id: int.Parse(m.Groups["id"].Value), Name: m.Groups["name"].Value, Type: m.Groups["type"].Value)).ToList();

        // Act
        var inCode = TMudArchMembers.Members.Select(m => (m.Id, m.Name, Type: m.TypeName)).ToList();

        // Assert
        Assert.Equal(inCode, rows);
        Assert.Equal(TMudArchMembers.FirstMemberId, inCode[0].Id);
    }

    [Fact]
    public void GkHost_Given_the_nine_keys_When_asked_Then_they_are_loader_keys_in_any_case_and_others_are_not()
    {
        // Arrange
        var host = new GkHost();

        // Act / Assert
        Assert.Equal(9, GkHost.OwnKeys.Count);
        Assert.All(GkHost.OwnKeys, k => Assert.True(host.IsLoaderKey(k)));
        Assert.True(host.IsLoaderKey("GraalNPCParam"));
        Assert.False(host.IsLoaderKey("weght"));
    }

    // ------------------------------------------------------------------ extension.json and a child process

    [Fact]
    public void ExtensionJson_Given_the_output_folder_When_read_Then_it_is_the_one_of_the_protocol_and_matches_the_hello()
    {
        // Arrange
        string file = Path.Combine(AppContext.BaseDirectory, "extension.json");

        // Act
        using JsonDocument json = JsonDocument.Parse(File.ReadAllText(file));
        JsonElement root = json.RootElement;

        // Assert
        Assert.Equal("crossfire", root.GetProperty("name").GetString());
        Assert.Equal(Manifest.ExtensionName, root.GetProperty("name").GetString());
        Assert.Equal(Manifest.ExtensionVersion, root.GetProperty("version").GetString());
        Assert.Equal(1, root.GetProperty("protocol").GetInt32());
        Assert.Equal("Crossfire.Extension", root.GetProperty("executable").GetString());
        Assert.Equal("GPL-2.0-or-later", root.GetProperty("licence").GetString());
        Assert.Equal("https://github.com/AlpaxisNet/Crossfire", root.GetProperty("source").GetString());
        Assert.True(File.Exists(Path.Combine(AppContext.BaseDirectory, root.GetProperty("executable").GetString()!)));
    }

    [Fact]
    public void Main_Given_wrong_arguments_When_started_Then_exit_64_and_nothing_on_standard_output()
    {
        // Arrange / Act
        (int exit, string output, string error) = RunExtension("--connect");

        // Assert
        Assert.Equal(64, exit);
        Assert.Equal("", output);
        Assert.Contains("usage", error);
    }

    [Fact]
    public void Main_Given_a_socket_path_longer_than_the_system_allows_When_started_Then_exit_69_without_a_stack_trace()
    {
        // Arrange
        string path = "/" + new string('a', 200);

        // Act
        (int exit, string output, string error) = RunExtension("--connect", path);

        // Assert
        Assert.Equal(69, exit);
        Assert.Equal("", output);
        Assert.Contains("cannot connect", error);
        Assert.DoesNotContain("   at ", error);
    }

    [Fact]
    public void Main_Given_a_socket_path_nobody_listens_on_When_started_Then_exit_69()
    {
        // Arrange
        using var folder = new TempFolder();

        // Act
        (int exit, string output, _) = RunExtension("--connect", Path.Combine(folder.Path, "none"));

        // Assert
        Assert.Equal(69, exit);
        Assert.Equal("", output);
    }

    [Fact]
    public void Main_Given_a_listening_socket_When_started_as_a_child_process_Then_it_does_the_handshake_init_and_findarchetype_and_exits_on_eof()
    {
        // Arrange
        using var world = TestWorld.Make();
        using var folder = new TempFolder();
        string path = Path.Combine(folder.Path, "s");
        using var listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        listener.Bind(new UnixDomainSocketEndPoint(path));
        listener.Listen(1);
        using Process process = Start("--connect", path);

        // Act
        using Socket accepted = listener.Accept();
        accepted.ReceiveTimeout = 20_000;
        using var stream = new NetworkStream(accepted, ownsSocket: false);
        byte[] hello = FrameCodec.Encode(new HelloFrame(ScriptedHost.HostHello(world.Path)));
        stream.Write(hello);
        var extensionHello = (HelloFrame)FrameCodec.Read(stream, FrameCodec.InitialMaxFrame)!;
        stream.Write(FrameCodec.Encode(new ReqFrame(1, 1, Array.Empty<ulong>(), ScriptedHost.NoCtx(), Array.Empty<Val>())));
        Frame? initReply = ReadSkippingLogs(stream);
        stream.Write(FrameCodec.Encode(new ReqFrame(2, 64, Array.Empty<ulong>(), ScriptedHost.NoCtx(), new[] { Val.Str("sword") })));
        var found = (ReplyFrame)ReadSkippingLogs(stream)!;
        accepted.Shutdown(SocketShutdown.Both);
        bool exited = process.WaitForExit(5000);

        // Assert
        Assert.Equal("crossfire", extensionHello.Hello.Get("name")!.AsString);
        Assert.Equal((long)TestWorld.Blocks, ((ReplyFrame)initReply!).Result.Get("archetypes")!.AsInt);
        Assert.Equal(1UL, found.Result.AsHandle >> 32);
        Assert.True(exited);
        Assert.Equal(0, process.ExitCode);
        Assert.Equal("", process.StandardOutput.ReadToEnd());
    }

    private static Frame? ReadSkippingLogs(Stream stream)
    {
        Frame? f;
        do
        {
            f = FrameCodec.Read(stream, FrameCodec.InitialMaxFrame);
        }
        while (f is LogFrame);
        return f;
    }

    private static Process Start(params string[] args)
    {
        string dll = Path.Combine(AppContext.BaseDirectory, "Crossfire.Extension.dll");
        var info = new ProcessStartInfo(Environment.ProcessPath ?? "dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = AppContext.BaseDirectory,
        };
        info.ArgumentList.Add(dll);
        foreach (string a in args)
        {
            info.ArgumentList.Add(a);
        }
        return Process.Start(info)!;
    }

    private static (int Exit, string Output, string Error) RunExtension(params string[] args)
    {
        using Process p = Start(args);
        string error = p.StandardError.ReadToEnd();
        string output = p.StandardOutput.ReadToEnd();
        Assert.True(p.WaitForExit(20_000));
        return (p.ExitCode, output, error);
    }

    // ------------------------------------------------------------------ Graal Kingdoms' own archetypes

    [GkFact]
    public void SessionInit_Given_the_archetypes_of_graal_kingdoms_When_loaded_Then_3300_archetypes_and_their_members_read()
    {
        // Arrange
        using var world = new TempFolder();
        Directory.CreateSymbolicLink(Path.Combine(world.Path, "Arch"), GkFactAttribute.ArchFolder);
        using ScriptedHost host = ScriptedHost.Handshake(world.Path, out _);

        // Act
        ReplyFrame init = host.Init();
        ReplyFrame found = host.Call(Manifest.OpFindArchetype, new[] { Val.Str("sword") });

        // Assert
        Assert.Equal(0UL, init.Status);
        Assert.Equal(3300L, init.Result.Get("archetypes")!.AsInt);
        Assert.Contains(host.Logs, l => l.Text.Contains("loaded 3300 archetypes (288 of them parts)"));
        Assert.Equal(0UL, found.Status);
    }
}
