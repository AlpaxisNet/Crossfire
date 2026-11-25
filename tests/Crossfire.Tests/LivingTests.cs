namespace Crossfire.Tests;

/// <summary>Tests of include/living.h's struct and of the extra-keys list. Not ported from a Crossfire file.</summary>
public class LivingTests
{
    public static IEnumerable<object[]> Fields()
    {
        yield return new object[] { "Str", (Action<Living>)(l => l.Str = 1) };
        yield return new object[] { "Dex", (Action<Living>)(l => l.Dex = 1) };
        yield return new object[] { "Con", (Action<Living>)(l => l.Con = 1) };
        yield return new object[] { "Wis", (Action<Living>)(l => l.Wis = 1) };
        yield return new object[] { "Cha", (Action<Living>)(l => l.Cha = 1) };
        yield return new object[] { "Int", (Action<Living>)(l => l.Int = 1) };
        yield return new object[] { "Pow", (Action<Living>)(l => l.Pow = 1) };
        yield return new object[] { "Wc", (Action<Living>)(l => l.Wc = 1) };
        yield return new object[] { "Ac", (Action<Living>)(l => l.Ac = 1) };
        yield return new object[] { "Hp", (Action<Living>)(l => l.Hp = 1) };
        yield return new object[] { "Maxhp", (Action<Living>)(l => l.Maxhp = 1) };
        yield return new object[] { "Sp", (Action<Living>)(l => l.Sp = 1) };
        yield return new object[] { "Maxsp", (Action<Living>)(l => l.Maxsp = 1) };
        yield return new object[] { "Grace", (Action<Living>)(l => l.Grace = 1) };
        yield return new object[] { "Maxgrace", (Action<Living>)(l => l.Maxgrace = 1) };
        yield return new object[] { "Exp", (Action<Living>)(l => l.Exp = 1) };
        yield return new object[] { "Food", (Action<Living>)(l => l.Food = 1) };
        yield return new object[] { "Dam", (Action<Living>)(l => l.Dam = 1) };
        yield return new object[] { "Luck", (Action<Living>)(l => l.Luck = 1) };
    }

    [Theory]
    [MemberData(nameof(Fields))]
    public void SameAs_Given_one_field_that_differs_When_compared_Then_false_and_copy_makes_them_the_same(string field, Action<Living> change)
    {
        // Arrange
        var a = new Living();
        var b = new Living();
        change(b);

        // Act
        bool same = a.SameAs(b);
        a.CopyFrom(b);
        bool afterCopy = a.SameAs(b);
        b.Clear();

        // Assert
        Assert.False(same, field);
        Assert.True(afterCopy, field);
        Assert.False(a.SameAs(b), field);
        Assert.True(b.SameAs(new Living()), field);
    }

    [Fact]
    public void ExtraKeys_Given_lines_When_asked_Then_the_last_value_wins_for_get_and_all_come_in_order()
    {
        // Arrange
        var e = new ExtraKeys();
        e.Add("Flag", "a");
        e.Add("other", "x");
        e.Add("flag", "b");

        // Act
        string? last = e.Get("FLAG");
        string[] all = e.GetAll("flag").ToArray();
        ExtraKeys copy = e.Copy();
        e.Add("flag", "c");

        // Assert
        Assert.Equal("b", last);
        Assert.Equal(new[] { "a", "b" }, all);
        Assert.Equal(3, copy.Count);
        Assert.Equal(4, e.Count);
        Assert.True(e.Contains("Other"));
        Assert.False(e.Contains("none"));
        Assert.Null(e.Get("none"));
    }
}
