// Not ported from a Crossfire file: tests of the manifest rules lane 4C needs (members of every lane on the core's types, the refs of
// a 1.0 link, the size of a list).
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using Crossfire.Extension.Protocol;

namespace Crossfire.Extension.Tests;

public sealed class ItemManifestTests
{
    private static Val TypeMap(int id, string name, int memberId, string memberType) => Val.Map(
        ("id", Val.Int(id)),
        ("name", Val.Str(name)),
        ("host", Val.Str("npc")),
        ("members", Val.List(Val.Map(("id", Val.Int(memberId)), ("name", Val.Str("m")), ("type", Val.Str(memberType)), ("access", Val.Str("r")), ("stable", Val.False)))),
        ("methods", Val.List()));

    [Fact]
    public void AddType_Given_a_type_of_the_core_When_it_holds_members_of_other_lanes_Then_each_in_a_lane_range_is_accepted_and_one_outside_all_is_refused()
    {
        // Arrange
        var b = new ManifestBuilder(1);

        // Act
        b.AddType(Lane.Core, new TypeDef(5, "TCoreOne", TypeMap(5, "TCoreOne", 1000, "int")));
        b.AddType(Lane.Core, new TypeDef(6, "TCoreTwo", TypeMap(6, "TCoreTwo", 7999, "int")));
        InvalidOperationException outside = Assert.Throws<InvalidOperationException>(() => b.AddType(Lane.Core, new TypeDef(7, "TCoreThree", TypeMap(7, "TCoreThree", 9999, "int"))));
        InvalidOperationException other = Assert.Throws<InvalidOperationException>(() => b.AddType(Lane.Items, new TypeDef(10, "TLaneOwn", TypeMap(10, "TLaneOwn", 2000, "int"))));

        // Assert
        Assert.Contains("outside", outside.Message);
        Assert.Contains("outside", other.Message);
    }

    [Fact]
    public void AddType_Given_a_ref_member_When_the_link_is_1_0_or_1_1_Then_a_core_type_takes_it_on_1_1_only_and_a_lane_type_needs_MinMinor_1()
    {
        // Arrange
        var v10 = new ManifestBuilder(0);
        var v11 = new ManifestBuilder(1);

        // Act / Assert
        Assert.Throws<InvalidOperationException>(() => v10.AddType(Lane.Core, new TypeDef(5, "TRefType", TypeMap(5, "TRefType", 1000, "ref"))));
        v11.AddType(Lane.Core, new TypeDef(5, "TRefType", TypeMap(5, "TRefType", 1000, "ref")));
        Assert.Throws<InvalidOperationException>(() => v11.AddType(Lane.Items, new TypeDef(10, "TRefOwn", TypeMap(10, "TRefOwn", 1001, "ref"))));
    }

    [Fact]
    public void MaxList_Given_the_link_minor_When_asked_Then_256_on_1_0_and_1024_on_1_1()
    {
        // Arrange / Act / Assert
        Assert.Equal(256, new ManifestBuilder(0).MaxList);
        Assert.Equal(1024, new ManifestBuilder(1).MaxList);
        Assert.Equal(1024, new ManifestBuilder(1).ToVal().Get("limits")!.Get("max_list")!.AsInt);
        Assert.Equal(256, new ManifestBuilder(0).ToVal().Get("limits")!.Get("max_list")!.AsInt);
    }
}
