// Not ported from a Crossfire file: tests of the extension against a scripted host (the game server's side of docs/protocol.md) over a
// Unix socket, in this process.
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using Crossfire.Extension.Gk;
using Crossfire.Extension.Protocol;

namespace Crossfire.Extension.Tests;

public class SessionTests
{
    private static Val S(string s) => Val.Str(s);

    private static int Id(string member) => TMudArchMembers.Members.Single(m => m.Name == member).Id;

    private static ulong Handle(uint epoch, uint tag) => ((ulong)epoch << 32) | tag;

    private static ulong Find(ScriptedHost host, string name, ulong op = Manifest.OpFindArchetype)
    {
        ReplyFrame r = host.Call(op, new[] { S(name) });
        Assert.Equal(0UL, r.Status);
        return r.Result.AsHandle;
    }

    private static ReplyFrame Get(ScriptedHost host, ulong handle, string member) =>
        host.Call(9, new[] { Val.Handle(handle), Val.Int(Id(member)) });

    private static (ScriptedHost Host, TempFolder World) Started(ExtensionOptions? options = null, long epoch = 1, long maxFrame = 16_777_216)
    {
        TempFolder world = TestWorld.Make();
        ScriptedHost host = ScriptedHost.Handshake(world.Path, out _, options, epoch, maxFrame);
        Assert.Equal(0UL, host.Init().Status);
        return (host, world);
    }

    private static void AssertStatus(int status, ReplyFrame reply)
    {
        Assert.Equal((ulong)status, reply.Status);
        if (status != 0)
        {
            Assert.Equal(ValKind.String, reply.Result.Kind);
            Assert.Empty(reply.Effects);
            Assert.Empty(reply.Events);
        }
    }

    // ------------------------------------------------------------------ the handshake

    [Fact]
    public void Handshake_Given_a_host_hello_When_the_extension_answers_Then_its_hello_has_the_keys_of_section_4_1_and_the_manifest()
    {
        // Arrange
        using var world = TestWorld.Make();
        using ScriptedHost host = ScriptedHost.Start(ScriptedHost.HostHello(world.Path));

        // Act
        Val hello = ((HelloFrame)host.Read()!).Hello;

        // Assert
        Assert.Equal(new[] { "proto_major", "proto_minor", "role", "name", "version", "features", "manifest" }, hello.Pairs.Select(p => p.Key));
        Assert.Equal(1L, hello.Get("proto_major")!.AsInt);
        Assert.Equal("extension", hello.Get("role")!.AsString);
        Assert.Equal("crossfire", hello.Get("name")!.AsString);
        Assert.True(Val.Same(Manifest.Build(), hello.Get("manifest")!));
    }

    [Fact]
    public void Handshake_Given_a_higher_minor_version_and_unknown_keys_When_connected_Then_they_are_ignored()
    {
        // Arrange
        using var world = TestWorld.Make();
        Val hello = ScriptedHost.HostHello(world.Path);
        Val changed = Val.Map(hello.Pairs.Select(p => p.Key == "proto_minor" ? new KeyValuePair<string, Val>(p.Key, Val.Int(7)) : p)
            .Append(new KeyValuePair<string, Val>("future_key", Val.True)));
        using ScriptedHost host = ScriptedHost.Start(changed);

        // Act
        Frame? answer = host.Read();

        // Assert
        Assert.IsType<HelloFrame>(answer);
    }

    [Fact]
    public void Handshake_Given_a_proto_major_other_than_1_When_connected_Then_no_hello_comes_and_the_exit_is_not_zero()
    {
        // Arrange
        using var world = TestWorld.Make();
        using ScriptedHost host = ScriptedHost.Start(ScriptedHost.HostHello(world.Path, protoMajor: 2));

        // Act
        int exit = host.WaitExit();
        Frame? answer = host.Read();

        // Assert
        Assert.Equal(ExtensionRunner.ExitVersion, exit);
        Assert.Null(answer);
    }

    [Theory]
    [InlineData("proto_major")]
    [InlineData("epoch")]
    [InlineData("world_dir")]
    [InlineData("data_dir")]
    [InlineData("max_frame")]
    [InlineData("call_timeout_ms")]
    [InlineData("server_name")]
    public void Handshake_Given_a_host_hello_without_a_required_key_When_connected_Then_no_hello_and_a_protocol_exit(string key)
    {
        // Arrange
        using var world = TestWorld.Make();
        using ScriptedHost host = ScriptedHost.Start(ScriptedHost.HostHello(world.Path, without: key));

        // Act
        int exit = host.WaitExit();

        // Assert
        Assert.Equal(ExtensionRunner.ExitProtocol, exit);
        Assert.Null(host.Read());
    }

    [Theory]
    [InlineData(0L, 16_777_216L)]
    [InlineData(4_294_967_296L, 16_777_216L)]
    [InlineData(1L, 65_535L)]
    public void Handshake_Given_an_epoch_or_max_frame_out_of_range_When_connected_Then_a_protocol_exit(long epoch, long maxFrame)
    {
        // Arrange
        using var world = TestWorld.Make();
        using ScriptedHost host = ScriptedHost.Start(ScriptedHost.HostHello(world.Path, epoch, 1, maxFrame));

        // Act
        int exit = host.WaitExit();

        // Assert
        Assert.Equal(ExtensionRunner.ExitProtocol, exit);
    }

    [Fact]
    public void Handshake_Given_a_ping_as_the_first_frame_When_connected_Then_a_protocol_exit()
    {
        // Arrange
        using ScriptedHost other = ScriptedHost.StartWithoutHello();
        other.Send(new PingFrame(1));

        // Act
        int exit = other.WaitExit();

        // Assert
        Assert.Equal(ExtensionRunner.ExitProtocol, exit);
    }

    [Fact]
    public void Handshake_Given_a_host_that_closes_without_a_hello_When_connected_Then_a_protocol_exit()
    {
        // Arrange
        using ScriptedHost host = ScriptedHost.StartWithoutHello();

        // Act
        host.CloseHostSide();

        // Assert
        Assert.Equal(ExtensionRunner.ExitProtocol, host.WaitExit());
    }

    // ------------------------------------------------------------------ session.init

    [Fact]
    public void SessionInit_Given_a_world_with_archetypes_When_called_Then_the_count_of_blocks_and_a_summary_log_come_back()
    {
        // Arrange
        using var world = TestWorld.Make();
        using ScriptedHost host = ScriptedHost.Handshake(world.Path, out _);

        // Act
        ReplyFrame reply = host.Init();

        // Assert
        AssertStatus(0, reply);
        Assert.Equal((long)TestWorld.Blocks, reply.Result.Get("archetypes")!.AsInt);
        LogFrame log = Assert.Single(host.Logs);
        Assert.Equal(2UL, log.Level);
        Assert.Contains("loaded " + TestWorld.Blocks + " archetypes (1 of them parts)", log.Text);
    }

    [Fact]
    public void SessionInit_Given_a_folder_named_arch_in_lower_case_When_called_Then_it_is_read()
    {
        // Arrange
        using var world = TestWorld.Make("arch");
        using ScriptedHost host = ScriptedHost.Handshake(world.Path, out _);

        // Act
        ReplyFrame reply = host.Init();

        // Assert
        AssertStatus(0, reply);
        Assert.Equal((long)TestWorld.Blocks, reply.Result.Get("archetypes")!.AsInt);
    }

    [Fact]
    public void SessionInit_Given_a_world_without_an_arch_folder_When_called_Then_internal_and_a_second_try_may_follow()
    {
        // Arrange
        using var world = new TempFolder();
        using ScriptedHost host = ScriptedHost.Handshake(world.Path, out _);

        // Act
        ReplyFrame first = host.Init();
        Directory.CreateDirectory(Path.Combine(world.Path, "Arch"));
        ReplyFrame second = host.Init();

        // Assert
        AssertStatus(Status.Internal, first);
        AssertStatus(0, second);
    }

    [Fact]
    public void SessionInit_Given_a_second_init_When_called_Then_bad_args()
    {
        // Arrange
        (ScriptedHost host, TempFolder world) = Started();
        using (host)
        using (world)
        {
            // Act
            ReplyFrame again = host.Init();

            // Assert
            AssertStatus(Status.BadArgs, again);
        }
    }

    [Fact]
    public void SessionInit_Given_arguments_When_called_Then_bad_args()
    {
        // Arrange
        using var world = TestWorld.Make();
        using ScriptedHost host = ScriptedHost.Handshake(world.Path, out _);

        // Act
        ReplyFrame reply = host.Call(1, new[] { Val.Nil });

        // Assert
        AssertStatus(Status.BadArgs, reply);
    }

    [Theory]
    [InlineData(3UL)]
    [InlineData(4UL)]
    [InlineData(9UL)]
    [InlineData(15UL)]
    [InlineData(64UL)]
    [InlineData(65UL)]
    [InlineData(5UL)]
    [InlineData(1000UL)]
    public void Request_Given_any_op_but_init_before_the_reply_to_init_When_sent_Then_unavailable(ulong op)
    {
        // Arrange
        using var world = TestWorld.Make();
        using ScriptedHost host = ScriptedHost.Handshake(world.Path, out _);

        // Act
        ReplyFrame reply = host.Call(op);

        // Assert
        AssertStatus(Status.Unavailable, reply);
    }

    // ------------------------------------------------------------------ findarchetype

    [Fact]
    public void FindArchetype_Given_a_name_that_exists_When_called_Then_a_handle_of_the_epoch_with_a_tag_and_the_same_handle_every_time()
    {
        // Arrange
        (ScriptedHost host, TempFolder world) = Started(epoch: 77);
        using (host)
        using (world)
        {
            // Act
            ulong sword = Find(host, "sword");
            ulong again = Find(host, "sword");
            ulong house = Find(host, "house");

            // Assert
            Assert.Equal(77UL, sword >> 32);
            Assert.NotEqual(0UL, sword & 0xffffffff);
            Assert.Equal(sword, again);
            Assert.NotEqual(sword, house);
            Assert.Equal(77UL, house >> 32);
            Assert.NotEqual(0UL, house & 0xffffffff);
        }
    }

    [Fact]
    public void FindArchetype_Given_a_name_that_does_not_exist_When_called_Then_nil_and_status_ok()
    {
        // Arrange
        (ScriptedHost host, TempFolder world) = Started();
        using (host)
        using (world)
        {
            // Act
            ReplyFrame reply = host.Call(Manifest.OpFindArchetype, new[] { S("nothing") });

            // Assert
            AssertStatus(0, reply);
            Assert.True(reply.Result.IsNil);
        }
    }

    [Fact]
    public void FindArchetype_Given_a_nil_argument_When_called_Then_it_reads_as_the_empty_string_and_finds_nothing()
    {
        // Arrange
        (ScriptedHost host, TempFolder world) = Started();
        using (host)
        using (world)
        {
            // Act
            ReplyFrame reply = host.Call(Manifest.OpFindArchetype, new[] { Val.Nil });

            // Assert
            AssertStatus(0, reply);
            Assert.True(reply.Result.IsNil);
        }
    }

    [Fact]
    public void FindArchetype_Given_the_case_of_another_spelling_When_called_Then_nil_because_the_match_is_exact()
    {
        // Arrange
        (ScriptedHost host, TempFolder world) = Started();
        using (host)
        using (world)
        {
            // Act
            ReplyFrame reply = host.Call(Manifest.OpFindArchetype, new[] { S("SWORD") });

            // Assert
            Assert.True(reply.Result.IsNil);
        }
    }

    [Theory]
    [InlineData(64UL)]
    [InlineData(65UL)]
    public void FindArchetype_Given_the_wrong_number_or_type_of_arguments_When_called_Then_bad_args(ulong op)
    {
        // Arrange
        (ScriptedHost host, TempFolder world) = Started();
        using (host)
        using (world)
        {
            // Act
            ReplyFrame none = host.Call(op);
            ReplyFrame two = host.Call(op, new[] { S("a"), S("b") });
            ReplyFrame number = host.Call(op, new[] { Val.Int(3) });

            // Assert
            AssertStatus(Status.BadArgs, none);
            AssertStatus(Status.BadArgs, two);
            AssertStatus(Status.BadArgs, number);
        }
    }

    [Fact]
    public void FindArchetypeByRealName_Given_the_display_name_of_an_archetype_When_called_Then_the_handle_of_that_archetype()
    {
        // Arrange
        (ScriptedHost host, TempFolder world) = Started();
        using (host)
        using (world)
        {
            // Act
            ulong byRealName = Find(host, "Goblin Chief", Manifest.OpFindArchetypeByRealName);
            ulong byKey = Find(host, "goblin");

            // Assert
            Assert.Equal(byKey, byRealName);
            Assert.Equal("goblin", Get(host, byRealName, "mudname").Result.AsString);
        }
    }

    [Fact]
    public void FindArchetypeByRealName_Given_the_key_of_an_archetype_that_is_not_its_display_name_When_called_Then_nil()
    {
        // Arrange
        (ScriptedHost host, TempFolder world) = Started();
        using (host)
        using (world)
        {
            // Act
            ReplyFrame byKey = host.Call(Manifest.OpFindArchetypeByRealName, new[] { S("goblin") });

            // Assert
            AssertStatus(0, byKey);
            Assert.True(byKey.Result.IsNil);
        }
    }

    [Fact]
    public void FindArchetype_Given_the_tags_are_used_up_When_a_new_archetype_is_asked_for_Then_internal_but_known_ones_still_answer()
    {
        // Arrange
        (ScriptedHost host, TempFolder world) = Started(new ExtensionOptions { TagLimit = 1 });
        using (host)
        using (world)
        {
            ulong sword = Find(host, "sword");

            // Act
            ReplyFrame house = host.Call(Manifest.OpFindArchetype, new[] { S("house") });
            ulong swordAgain = Find(host, "sword");

            // Assert
            AssertStatus(Status.Internal, house);
            Assert.Equal(sword, swordAgain);
            Assert.Equal(1UL, sword & 0xffffffff);
        }
    }

    // ------------------------------------------------------------------ get, get_many

    [Fact]
    public void Get_Given_a_live_handle_and_members_When_read_Then_the_values_of_the_archetype_come_back()
    {
        // Arrange
        (ScriptedHost host, TempFolder world) = Started();
        using (host)
        using (world)
        {
            ulong sword = Find(host, "sword");
            ulong house = Find(host, "house");

            // Act / Assert
            Assert.Equal("sword", Get(host, sword, "mudname").Result.AsString);
            Assert.Equal("sword", Get(host, sword, "archname").Result.AsString);
            Assert.Equal("sword", Get(host, sword, "mudrealname").Result.AsString);
            // query_short_name gives the plural for nrof 0 when the object has a weight: Crossfire's rule, kept
            Assert.Equal("swords", Get(host, sword, "fullname").Result.AsString);
            Assert.Equal("swords", Get(host, sword, "mudfullname").Result.AsString);
            Assert.Equal("house", Get(host, house, "mudfullname").Result.AsString);
            Assert.Equal("mud_sword.png", Get(host, sword, "icon").Result.AsString);
            Assert.Equal(15L, Get(host, sword, "mudtype").Result.AsInt);
            Assert.Equal(3000L, Get(host, sword, "mudweight").Result.AsInt);
            Assert.Equal(8L, Get(host, sword, "muddamage").Result.AsInt);
            Assert.Equal("50", Get(host, sword, "value").Result.AsString);
            Assert.Equal(5L, Get(host, house, "mudhp").Result.AsInt);
            Assert.Equal(9L, Get(host, house, "mudmaxhp").Result.AsInt);
            Assert.Equal(3L, Get(host, house, "mudlevel").Result.AsInt);
            Assert.Equal(ValKind.Float, Get(host, house, "mudspeed").Result.Kind);
            Assert.Equal(0.1, Get(host, house, "mudspeed").Result.AsFloat);
            Assert.Equal(new[] { "It is a house.", "Welcome." }, Get(host, house, "mudmessage").Result.Items.Select(i => i.AsString));
        }
    }

    [Fact]
    public void Get_Given_an_archetype_with_graal_kingdoms_keys_When_read_Then_the_extra_lines_are_the_members()
    {
        // Arrange
        (ScriptedHost host, TempFolder world) = Started();
        using (host)
        using (world)
        {
            ulong goblin = Find(host, "goblin");
            ulong sword = Find(host, "sword");

            // Act / Assert
            Assert.Equal(1L, Get(host, goblin, "mudgraaltype").Result.AsInt);
            Assert.Equal(1L, Get(host, goblin, "npctype").Result.AsInt);
            Assert.Equal("gob.png", Get(host, goblin, "mudgraalparam").Result.AsString);
            Assert.Equal("gob.png", Get(host, goblin, "npcparam").Result.AsString);
            Assert.Equal(4L, Get(host, goblin, "mudhpbuff").Result.AsInt);
            Assert.True(Get(host, goblin, "nopeaceful").Result.AsBool);
            Assert.True(Get(host, goblin, "ismudmonster").Result.AsBool);
            Assert.True(Get(host, goblin, "alive").Result.AsBool);
            Assert.True(Get(host, goblin, "ismudobject").Result.AsBool);
            Assert.Equal("mud_gobchief.png", Get(host, goblin, "icon").Result.AsString);
            Assert.Equal("Goblin Chief", Get(host, goblin, "mudrealname").Result.AsString);
            Assert.Equal(-1L, Get(host, sword, "mudgraaltype").Result.AsInt);
            Assert.Equal("", Get(host, sword, "mudgraalparam").Result.AsString);
            Assert.False(Get(host, sword, "nopeaceful").Result.AsBool);
            Assert.False(Get(host, sword, "ismudmonster").Result.AsBool);
        }
    }

    [Fact]
    public void Get_Given_a_member_id_sent_as_a_float_When_read_Then_it_is_converted_as_section_3_2_says()
    {
        // Arrange
        (ScriptedHost host, TempFolder world) = Started();
        using (host)
        using (world)
        {
            ulong sword = Find(host, "sword");

            // Act
            ReplyFrame reply = host.Call(9, new[] { Val.Handle(sword), Val.Float(Id("mudname") + 0.5) });

            // Assert
            Assert.Equal("sword", reply.Result.AsString);
        }
    }

    [Fact]
    public void GetMany_Given_several_members_When_read_Then_a_list_of_the_values_in_order()
    {
        // Arrange
        (ScriptedHost host, TempFolder world) = Started();
        using (host)
        using (world)
        {
            ulong sword = Find(host, "sword");

            // Act
            ReplyFrame reply = host.Call(11, new[] { Val.Handle(sword), Val.List(Val.Int(Id("mudtype")), Val.Int(Id("mudname")), Val.Int(Id("mudtype"))) });

            // Assert
            AssertStatus(0, reply);
            Assert.Equal(3, reply.Result.Items.Count);
            Assert.Equal(15L, reply.Result.Items[0].AsInt);
            Assert.Equal("sword", reply.Result.Items[1].AsString);
            Assert.Equal(15L, reply.Result.Items[2].AsInt);
        }
    }

    [Fact]
    public void GetMany_Given_an_empty_list_When_read_Then_an_empty_list()
    {
        // Arrange
        (ScriptedHost host, TempFolder world) = Started();
        using (host)
        using (world)
        {
            ulong sword = Find(host, "sword");

            // Act
            ReplyFrame reply = host.Call(11, new[] { Val.Handle(sword), Val.List() });

            // Assert
            AssertStatus(0, reply);
            Assert.Empty(reply.Result.Items);
        }
    }

    [Fact]
    public void GetMany_Given_one_bad_member_id_When_read_Then_the_whole_call_fails_with_bad_args()
    {
        // Arrange
        (ScriptedHost host, TempFolder world) = Started();
        using (host)
        using (world)
        {
            ulong sword = Find(host, "sword");

            // Act
            ReplyFrame unknown = host.Call(11, new[] { Val.Handle(sword), Val.List(Val.Int(Id("mudname")), Val.Int(99)) });
            ReplyFrame notANumber = host.Call(11, new[] { Val.Handle(sword), Val.List(Val.Str("x")) });

            // Assert
            AssertStatus(Status.BadArgs, unknown);
            AssertStatus(Status.BadArgs, notANumber);
        }
    }

    [Fact]
    public void Get_Given_a_dead_nil_or_foreign_handle_When_read_Then_bad_handle()
    {
        // Arrange
        (ScriptedHost host, TempFolder world) = Started(epoch: 5);
        using (host)
        using (world)
        {
            ulong sword = Find(host, "sword");
            int id = Id("mudname");

            // Act
            ReplyFrame nil = host.Call(9, new[] { Val.Nil, Val.Int(id) });
            ReplyFrame otherEpoch = host.Call(9, new[] { Val.Handle(Handle(6, (uint)sword)), Val.Int(id) });
            ReplyFrame tagZero = host.Call(9, new[] { Val.Handle(Handle(5, 0)), Val.Int(id) });
            ReplyFrame neverIssued = host.Call(9, new[] { Val.Handle(Handle(5, 2)), Val.Int(id) });
            ReplyFrame manyNeverIssued = host.Call(11, new[] { Val.Handle(Handle(5, 9)), Val.List(Val.Int(id)) });

            // Assert
            AssertStatus(Status.BadHandle, nil);
            AssertStatus(Status.BadHandle, otherEpoch);
            AssertStatus(Status.BadHandle, tagZero);
            AssertStatus(Status.BadHandle, neverIssued);
            AssertStatus(Status.BadHandle, manyNeverIssued);
            Assert.Equal("sword", Get(host, sword, "mudname").Result.AsString);
        }
    }

    [Fact]
    public void Get_Given_the_last_issued_tag_When_read_Then_it_is_live_and_the_next_tag_is_not()
    {
        // Arrange
        (ScriptedHost host, TempFolder world) = Started(epoch: 5);
        using (host)
        using (world)
        {
            ulong sword = Find(host, "sword");
            ulong house = Find(host, "house");

            // Act
            ReplyFrame last = host.Call(9, new[] { Val.Handle(house), Val.Int(Id("mudname")) });
            ReplyFrame next = host.Call(9, new[] { Val.Handle((house & ~0xffffffffUL) | ((house & 0xffffffff) + 1)), Val.Int(Id("mudname")) });

            // Assert
            Assert.Equal("house", last.Result.AsString);
            AssertStatus(Status.BadHandle, next);
            Assert.NotEqual(sword, house);
        }
    }

    [Fact]
    public void Get_Given_the_wrong_number_or_types_of_arguments_When_read_Then_bad_args()
    {
        // Arrange
        (ScriptedHost host, TempFolder world) = Started();
        using (host)
        using (world)
        {
            ulong sword = Find(host, "sword");

            // Act / Assert
            AssertStatus(Status.BadArgs, host.Call(9));
            AssertStatus(Status.BadArgs, host.Call(9, new[] { Val.Handle(sword) }));
            AssertStatus(Status.BadArgs, host.Call(9, new[] { Val.Handle(sword), Val.Int(Id("mudname")), Val.Nil }));
            AssertStatus(Status.BadArgs, host.Call(9, new[] { S("handle"), Val.Int(Id("mudname")) }));
            AssertStatus(Status.BadArgs, host.Call(9, new[] { Val.Handle(sword), S("mudname") }));
            AssertStatus(Status.BadArgs, host.Call(9, new[] { Val.Handle(sword), Val.Int(3) }));
            AssertStatus(Status.BadArgs, host.Call(9, new[] { Val.Handle(sword), Val.Int(long.MaxValue) }));
            AssertStatus(Status.BadArgs, host.Call(11, new[] { Val.Handle(sword), Val.Int(1) }));
            AssertStatus(Status.BadArgs, host.Call(11, new[] { Val.Handle(sword) }));
        }
    }

    [Fact]
    public void Releases_Given_dead_unknown_and_live_handles_When_listed_Then_they_are_ignored_and_the_archetype_stays()
    {
        // Arrange
        (ScriptedHost host, TempFolder world) = Started();
        using (host)
        using (world)
        {
            ulong sword = Find(host, "sword");

            // Act
            ReplyFrame reply = host.Call(9, new[] { Val.Handle(sword), Val.Int(Id("mudname")) }, new[] { sword, Handle(9, 9), 0UL });
            ulong again = Find(host, "sword");

            // Assert
            Assert.Equal("sword", reply.Result.AsString);
            Assert.Equal(sword, again);
            Assert.Equal("sword", Get(host, sword, "mudname").Result.AsString);
        }
    }

    // ------------------------------------------------------------------ set, get_list, invoke, other ops

    [Fact]
    public void Set_Given_a_read_only_member_When_written_Then_bad_args_but_a_dead_handle_is_bad_handle()
    {
        // Arrange
        (ScriptedHost host, TempFolder world) = Started();
        using (host)
        using (world)
        {
            ulong sword = Find(host, "sword");

            // Act
            ReplyFrame readOnly = host.Call(10, new[] { Val.Handle(sword), Val.Int(Id("mudname")), S("x") });
            ReplyFrame unknown = host.Call(10, new[] { Val.Handle(sword), Val.Int(3), S("x") });
            ReplyFrame dead = host.Call(10, new[] { Val.Handle(Handle(1, 99)), Val.Int(Id("mudname")), S("x") });
            ReplyFrame shape = host.Call(10, new[] { Val.Handle(sword), Val.Int(Id("mudname")) });

            // Assert
            AssertStatus(Status.BadArgs, readOnly);
            Assert.Contains("read-only", readOnly.Result.AsString);
            AssertStatus(Status.BadArgs, unknown);
            Assert.Contains("unknown member", unknown.Result.AsString);
            AssertStatus(Status.BadHandle, dead);
            AssertStatus(Status.BadArgs, shape);
            Assert.Equal("sword", Get(host, sword, "mudname").Result.AsString);
        }
    }

    [Fact]
    public void GetList_Given_a_member_that_is_not_a_list_of_handles_When_read_Then_bad_args()
    {
        // Arrange
        (ScriptedHost host, TempFolder world) = Started();
        using (host)
        using (world)
        {
            ulong sword = Find(host, "sword");

            // Act
            ReplyFrame notList = host.Call(12, new[] { Val.Handle(sword), Val.Int(Id("mudmessage")), Val.List() });
            ReplyFrame unknown = host.Call(12, new[] { Val.Handle(sword), Val.Int(3), Val.List() });
            ReplyFrame dead = host.Call(12, new[] { Val.Handle(Handle(1, 99)), Val.Int(Id("mudmessage")), Val.List() });
            ReplyFrame shape = host.Call(12, new[] { Val.Handle(sword), Val.Int(Id("mudmessage")), Val.Nil });

            // Assert
            AssertStatus(Status.BadArgs, notList);
            Assert.Contains("not a list of handles", notList.Result.AsString);
            AssertStatus(Status.BadArgs, unknown);
            Assert.Contains("unknown member", unknown.Result.AsString);
            AssertStatus(Status.BadHandle, dead);
            AssertStatus(Status.BadArgs, shape);
        }
    }

    [Fact]
    public void Invoke_Given_a_method_id_When_called_Then_bad_args_because_TMudArch_has_no_methods()
    {
        // Arrange
        (ScriptedHost host, TempFolder world) = Started();
        using (host)
        using (world)
        {
            ulong sword = Find(host, "sword");

            // Act
            ReplyFrame method = host.Call(13, new[] { Val.Handle(sword), Val.Int(5), Val.List() });
            ReplyFrame dead = host.Call(13, new[] { Val.Handle(Handle(1, 99)), Val.Int(5), Val.List() });
            ReplyFrame shape = host.Call(13, new[] { Val.Handle(sword), S("m"), Val.List() });
            ReplyFrame shape2 = host.Call(13, new[] { Val.Handle(sword), Val.Int(5), Val.Nil });

            // Assert
            AssertStatus(Status.BadArgs, method);
            AssertStatus(Status.BadHandle, dead);
            AssertStatus(Status.BadArgs, shape);
            AssertStatus(Status.BadArgs, shape2);
        }
    }

    [Theory]
    [InlineData(0UL)]
    [InlineData(2UL)]
    [InlineData(5UL)]
    [InlineData(6UL)]
    [InlineData(7UL)]
    [InlineData(8UL)]
    [InlineData(14UL)]
    [InlineData(16UL)]
    [InlineData(17UL)]
    [InlineData(63UL)]
    [InlineData(66UL)]
    [InlineData(2147483647UL)]
    public void Request_Given_an_op_the_extension_does_not_have_When_sent_Then_unknown_op(ulong op)
    {
        // Arrange
        (ScriptedHost host, TempFolder world) = Started();
        using (host)
        using (world)
        {
            // Act
            ReplyFrame reply = host.Call(op, new[] { S("x") });

            // Assert
            AssertStatus(Status.UnknownOp, reply);
        }
    }

    [Fact]
    public void Stats_Given_some_requests_When_asked_Then_a_map_of_counters_that_count_them()
    {
        // Arrange
        (ScriptedHost host, TempFolder world) = Started();
        using (host)
        using (world)
        {
            ulong sword = Find(host, "sword");
            host.Call(9, new[] { Val.Nil, Val.Int(3) });
            host.Send(new PingFrame(5));
            Assert.Equal(5UL, ((PongFrame)host.Read()!).N);

            // Act
            ReplyFrame reply = host.Call(15);

            // Assert
            AssertStatus(0, reply);
            Assert.Equal(4L, reply.Result.Get("requests")!.AsInt); // init, findarchetype, get, stats
            Assert.Equal(1L, reply.Result.Get("errors")!.AsInt);
            Assert.Equal(1L, reply.Result.Get("pings")!.AsInt);
            Assert.Equal((long)TestWorld.Blocks, reply.Result.Get("archetypes")!.AsInt);
            Assert.Equal(1L, reply.Result.Get("tags")!.AsInt);
            Assert.Equal(5L, reply.Result.Get("frames_in")!.AsInt); // init, find, get, ping, stats (the HELLO is read before the count starts)
            Assert.True(reply.Result.Get("frames_out")!.AsInt >= 5);
            Assert.NotEqual(0UL, sword);
        }
    }

    [Fact]
    public void Stats_Given_arguments_When_asked_Then_bad_args()
    {
        // Arrange
        (ScriptedHost host, TempFolder world) = Started();
        using (host)
        using (world)
        {
            // Act / Assert
            AssertStatus(Status.BadArgs, host.Call(15, new[] { Val.Nil }));
        }
    }

    [Fact]
    public void Flush_Given_nothing_to_save_When_called_Then_nil_and_arguments_are_bad_args()
    {
        // Arrange
        (ScriptedHost host, TempFolder world) = Started();
        using (host)
        using (world)
        {
            // Act
            ReplyFrame ok = host.Call(3);
            ReplyFrame bad = host.Call(3, new[] { Val.True });

            // Assert
            AssertStatus(0, ok);
            Assert.True(ok.Result.IsNil);
            AssertStatus(Status.BadArgs, bad);
        }
    }

    [Fact]
    public void Shutdown_Given_save_true_When_called_Then_nil_and_after_the_host_closes_the_exit_is_zero()
    {
        // Arrange
        (ScriptedHost host, TempFolder world) = Started();
        using (host)
        using (world)
        {
            // Act
            ReplyFrame reply = host.Call(4, new[] { Val.True });
            host.CloseHostSide();

            // Assert
            AssertStatus(0, reply);
            Assert.True(reply.Result.IsNil);
            Assert.Equal(ExtensionRunner.ExitOk, host.WaitExit());
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Shutdown_Given_save_false_or_nil_When_called_Then_nil(bool nil)
    {
        // Arrange
        (ScriptedHost host, TempFolder world) = Started();
        using (host)
        using (world)
        {
            // Act
            ReplyFrame reply = host.Call(4, new[] { nil ? Val.Nil : Val.False });

            // Assert
            AssertStatus(0, reply);
            Assert.True(reply.Result.IsNil);
        }
    }

    [Fact]
    public void Shutdown_Given_a_string_or_no_argument_When_called_Then_bad_args_and_the_session_goes_on()
    {
        // Arrange
        (ScriptedHost host, TempFolder world) = Started();
        using (host)
        using (world)
        {
            // Act
            ReplyFrame text = host.Call(4, new[] { S("yes") });
            ReplyFrame none = host.Call(4);
            ReplyFrame after = host.Call(15);

            // Assert
            AssertStatus(Status.BadArgs, text);
            AssertStatus(Status.BadArgs, none);
            AssertStatus(0, after);
        }
    }

    [Fact]
    public void Request_Given_a_req_after_the_reply_to_shutdown_When_sent_Then_a_protocol_error_and_a_protocol_exit()
    {
        // Arrange
        (ScriptedHost host, TempFolder world) = Started();
        using (host)
        using (world)
        {
            host.Call(4, new[] { Val.True });

            // Act
            host.SendReq(15);

            // Assert
            Assert.Equal(ExtensionRunner.ExitProtocol, host.WaitExit());
        }
    }

    [Fact]
    public void Request_Given_a_handler_that_throws_When_the_op_is_asked_Then_internal_and_the_next_request_is_answered()
    {
        // Arrange
        var options = new ExtensionOptions { BeforeOperation = op => { if (op == 15) { throw new InvalidOperationException("boom"); } } };
        (ScriptedHost host, TempFolder world) = Started(options);
        using (host)
        using (world)
        {
            // Act
            ReplyFrame failed = host.Call(15);
            ReplyFrame next = host.Call(3);

            // Assert
            AssertStatus(Status.Internal, failed);
            Assert.Equal("internal error", failed.Result.AsString);
            AssertStatus(0, next);
            Assert.Contains(host.Logs, l => l.Level == 0 && l.Text.Contains("boom"));
        }
    }

    [Fact]
    public void Reply_Given_an_answer_that_does_not_fit_the_links_maximum_When_made_Then_too_large_and_the_next_request_is_answered()
    {
        // Arrange
        (ScriptedHost host, TempFolder world) = Started(maxFrame: 65_536);
        using (host)
        using (world)
        {
            ulong sword = Find(host, "sword");
            Val[] many = Enumerable.Repeat(Val.Int(Id("mudfullname")), 14_000).ToArray();

            // Act
            ReplyFrame big = host.Call(11, new[] { Val.Handle(sword), Val.List(many) });
            ReplyFrame small = host.Call(11, new[] { Val.Handle(sword), Val.List(many.Take(100)) });

            // Assert
            AssertStatus(Status.TooLarge, big);
            AssertStatus(0, small);
            Assert.Equal(100, small.Result.Items.Count);
        }
    }

    // ------------------------------------------------------------------ the threads

    [Fact]
    public void Ping_Given_a_busy_worker_When_a_ping_is_sent_Then_the_pong_comes_before_the_reply()
    {
        // Arrange
        using var gate = new ManualResetEventSlim(false);
        using var entered = new ManualResetEventSlim(false);
        var options = new ExtensionOptions
        {
            BeforeOperation = op =>
            {
                if (op == Manifest.OpFindArchetype)
                {
                    entered.Set();
                    gate.Wait(TimeSpan.FromSeconds(10));
                }
            },
        };
        (ScriptedHost host, TempFolder world) = Started(options);
        using (host)
        using (world)
        {
            ulong id = host.SendReq(Manifest.OpFindArchetype, new[] { S("sword") });
            Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));

            // Act
            host.Send(new PingFrame(41));
            Frame? first = host.Read();
            gate.Set();
            ReplyFrame reply = host.ReadReply(id);

            // Assert
            Assert.Equal(41UL, Assert.IsType<PongFrame>(first).N);
            AssertStatus(0, reply);
        }
    }

    [Fact]
    public void Ping_Given_an_idle_extension_When_pinged_twice_Then_each_pong_carries_its_n()
    {
        // Arrange
        (ScriptedHost host, TempFolder world) = Started();
        using (host)
        using (world)
        {
            // Act
            host.Send(new PingFrame(1));
            host.Send(new PingFrame(300));

            // Assert
            Assert.Equal(1UL, ((PongFrame)host.Read()!).N);
            Assert.Equal(300UL, ((PongFrame)host.Read()!).N);
        }
    }

    [Fact]
    public void Pong_Given_a_pong_the_extension_did_not_ask_for_When_sent_Then_it_is_ignored()
    {
        // Arrange
        (ScriptedHost host, TempFolder world) = Started();
        using (host)
        using (world)
        {
            // Act
            host.Send(new PongFrame(8));
            ReplyFrame reply = host.Call(3);

            // Assert
            AssertStatus(0, reply);
        }
    }

    [Fact]
    public void Eof_Given_an_idle_extension_When_the_host_closes_Then_it_exits_with_zero_within_5_s()
    {
        // Arrange
        (ScriptedHost host, TempFolder world) = Started();
        using (host)
        using (world)
        {
            // Act
            host.CloseHostSide();

            // Assert
            Assert.Equal(ExtensionRunner.ExitOk, host.WaitExit());
        }
    }

    [Fact]
    public void Eof_Given_a_worker_that_does_not_finish_When_the_host_closes_Then_the_exit_comes_after_the_grace_time()
    {
        // Arrange
        using var gate = new ManualResetEventSlim(false);
        using var entered = new ManualResetEventSlim(false);
        var options = new ExtensionOptions
        {
            BeforeOperation = op =>
            {
                if (op == 15)
                {
                    entered.Set();
                    gate.Wait(TimeSpan.FromSeconds(20));
                }
            },
            WorkerGrace = TimeSpan.FromMilliseconds(300),
        };
        (ScriptedHost host, TempFolder world) = Started(options);
        using (host)
        using (world)
        {
            host.SendReq(15);
            Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));

            // Act
            host.CloseHostSide();
            int exit = host.WaitExit();
            gate.Set();

            // Assert
            Assert.Equal(ExtensionRunner.ExitOk, exit);
        }
    }

    [Fact]
    public void Request_Given_a_second_req_while_one_is_outstanding_When_sent_Then_a_protocol_exit()
    {
        // Arrange
        using var gate = new ManualResetEventSlim(false);
        using var entered = new ManualResetEventSlim(false);
        var options = new ExtensionOptions { BeforeOperation = op => { if (op == 15) { entered.Set(); gate.Wait(TimeSpan.FromSeconds(10)); } } };
        (ScriptedHost host, TempFolder world) = Started(options);
        using (host)
        using (world)
        {
            host.SendReq(15);
            Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));

            // Act
            host.SendReq(3);
            int exit = host.WaitExit();
            gate.Set();

            // Assert
            Assert.Equal(ExtensionRunner.ExitProtocol, exit);
        }
    }

    [Fact]
    public void Request_Given_a_wrong_reqid_When_sent_Then_a_protocol_exit()
    {
        // Arrange
        (ScriptedHost host, TempFolder world) = Started();
        using (host)
        using (world)
        {
            // Act
            host.Send(new ReqFrame(5, 3, Array.Empty<ulong>(), ScriptedHost.NoCtx(), Array.Empty<Val>()));

            // Assert
            Assert.Equal(ExtensionRunner.ExitProtocol, host.WaitExit());
        }
    }

    [Theory]
    [MemberData(nameof(FramesAHostMayNotSend))]
    public void Frames_Given_a_kind_the_host_may_not_send_When_sent_Then_a_protocol_exit(int index)
    {
        // Arrange
        (ScriptedHost host, TempFolder world) = Started();
        using (host)
        using (world)
        {
            // Act
            host.Send(FramesToRefuse[index]);

            // Assert
            Assert.Equal(ExtensionRunner.ExitProtocol, host.WaitExit());
        }
    }

    private static readonly Frame[] FramesToRefuse =
    {
        new HelloFrame(Val.Map()),
        new QResultFrame(1, 0, Val.Nil),
        new QueryFrame(1, "x", Array.Empty<Val>()),
        new ReplyFrame(1, 0, Val.Nil, Array.Empty<Val>(), Array.Empty<Val>()),
        new LogFrame(1, "x"),
    };

    public static IEnumerable<object[]> FramesAHostMayNotSend() => Enumerable.Range(0, FramesToRefuse.Length).Select(i => new object[] { i });

    [Fact]
    public void Frames_Given_garbage_bytes_When_sent_Then_a_protocol_exit_and_a_log_line_of_level_0()
    {
        // Arrange
        (ScriptedHost host, TempFolder world) = Started();
        using (host)
        using (world)
        {
            // Act
            host.SendRaw(new byte[] { 1, 0, 0, 0, 99 });
            int exit = host.WaitExit();
            while (host.Read() != null)
            {
            }

            // Assert
            Assert.Equal(ExtensionRunner.ExitProtocol, exit);
            Assert.Contains(host.Logs, l => l.Level == 0 && l.Text.StartsWith("protocol error"));
        }
    }

    [Fact]
    public void Frames_Given_a_frame_longer_than_the_hellos_maximum_When_sent_Then_a_protocol_exit()
    {
        // Arrange: the host's HELLO gave max_frame 65,536
        (ScriptedHost host, TempFolder world) = Started(maxFrame: 65_536);
        using (host)
        using (world)
        {
            // Act
            host.SendRaw(new byte[] { 1, 0, 1, 0 });

            // Assert
            Assert.Equal(ExtensionRunner.ExitProtocol, host.WaitExit());
        }
    }
}
