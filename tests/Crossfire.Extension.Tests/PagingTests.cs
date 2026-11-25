// Not ported from a Crossfire file: tests of the paging helpers with synthetic items.
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using Crossfire.Extension.Protocol;

namespace Crossfire.Extension.Tests;

public class PagingTests
{
    private static List<int> Items(int n) => Enumerable.Range(0, n).ToList();

    [Fact]
    public void LevelPage_Given_150_items_When_paged_from_nil_Then_pages_of_64_64_22_with_int_cursors_and_no_cursor_at_the_end()
    {
        // Arrange
        List<int> items = Items(150);

        // Act
        Page<int> first = Paging.LevelPage(items, Val.Nil);
        Page<int> second = Paging.LevelPage(items, first.Cursor);
        Page<int> third = Paging.LevelPage(items, second.Cursor);

        // Assert
        Assert.Equal(Items(64), first.Items);
        Assert.True(first.More);
        Assert.True(Val.Same(Val.Int(64), first.Cursor));
        Assert.Equal(Enumerable.Range(64, 64), second.Items);
        Assert.True(second.More);
        Assert.True(Val.Same(Val.Int(128), second.Cursor));
        Assert.Equal(Enumerable.Range(128, 22), third.Items);
        Assert.False(third.More);
        Assert.True(third.Cursor.IsNil);
    }

    [Fact]
    public void LevelPage_Given_exactly_64_items_When_paged_Then_one_page_and_no_more()
    {
        // Arrange
        List<int> items = Items(64);

        // Act
        Page<int> page = Paging.LevelPage(items, Val.Nil);

        // Assert
        Assert.Equal(64, page.Items.Count);
        Assert.False(page.More);
        Assert.True(page.Cursor.IsNil);
    }

    [Fact]
    public void LevelPage_Given_no_items_or_a_cursor_at_the_end_When_paged_Then_an_empty_last_page()
    {
        // Arrange / Act
        Page<int> none = Paging.LevelPage(new List<int>(), Val.Nil);
        Page<int> atEnd = Paging.LevelPage(Items(3), Val.Int(3));

        // Assert
        Assert.Empty(none.Items);
        Assert.False(none.More);
        Assert.Empty(atEnd.Items);
        Assert.False(atEnd.More);
        Assert.True(atEnd.Cursor.IsNil);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    public void LevelPage_Given_an_int_cursor_out_of_range_When_paged_Then_a_bad_cursor(long cursor)
    {
        // Arrange
        List<int> items = Items(3);

        // Act / Assert
        Assert.Throws<BadCursorException>(() => Paging.LevelPage(items, Val.Int(cursor)));
    }

    [Fact]
    public void LevelPage_Given_a_cursor_that_is_not_an_int_When_paged_Then_a_bad_cursor()
    {
        // Arrange / Act / Assert
        Assert.Throws<BadCursorException>(() => Paging.LevelPage(Items(3), Val.Str("1")));
        Assert.Throws<BadCursorException>(() => Paging.LevelPage(Items(3), Val.Float(1)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(65)]
    public void LevelPage_Given_a_page_size_outside_1_to_64_When_paged_Then_an_argument_error(int perPage)
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => Paging.LevelPage(Items(3), Val.Nil, perPage));
    }

    [Fact]
    public void LevelPage_Given_a_smaller_page_size_When_paged_Then_pages_of_that_size()
    {
        // Arrange
        List<int> items = Items(5);

        // Act
        Page<int> first = Paging.LevelPage(items, Val.Nil, 2);
        Page<int> second = Paging.LevelPage(items, first.Cursor, 2);
        Page<int> third = Paging.LevelPage(items, second.Cursor, 2);

        // Assert
        Assert.Equal(new[] { 0, 1 }, first.Items);
        Assert.Equal(new[] { 2, 3 }, second.Items);
        Assert.Equal(new[] { 4 }, third.Items);
        Assert.False(third.More);
    }

    [Fact]
    public void LevelPage_Given_a_byte_budget_When_paged_Then_each_page_stays_within_it_and_every_item_comes_once()
    {
        // Arrange
        List<int> items = Items(10); // each item is 10 bytes: 3 fit in a budget of 35

        // Act
        var seen = new List<int>();
        var pages = new List<Page<int>>();
        Val cursor = Val.Nil;
        do
        {
            Page<int> page = Paging.LevelPage(items, cursor, 64, _ => 10, 35);
            pages.Add(page);
            seen.AddRange(page.Items);
            cursor = page.Cursor;
            if (!page.More)
            {
                break;
            }
        }
        while (true);

        // Assert
        Assert.Equal(items, seen);
        Assert.Equal(new[] { 3, 3, 3, 1 }, pages.Select(p => p.Items.Count));
    }

    [Fact]
    public void LevelPage_Given_an_item_alone_over_the_budget_When_it_is_next_Then_item_too_large_and_a_budget_of_exactly_its_size_takes_it()
    {
        // Arrange
        List<int> items = new() { 5, 50, 5 };

        // Act
        Page<int> first = Paging.LevelPage(items, Val.Nil, 64, i => i, 20);
        Page<int> exact = Paging.LevelPage(items, first.Cursor, 64, i => i, 50);

        // Assert
        Assert.Equal(new[] { 5 }, first.Items);
        Assert.Throws<ItemTooLargeException>(() => Paging.LevelPage(items, first.Cursor, 64, i => i, 49));
        Assert.Equal(new[] { 50 }, exact.Items);
    }

    [Fact]
    public void Result_Given_a_page_When_made_into_the_result_of_level_load_Then_a_map_of_more_and_cursor()
    {
        // Arrange
        Page<int> page = Paging.LevelPage(Items(70), Val.Nil);

        // Act
        Val result = page.Result();

        // Assert
        Assert.True(Val.Same(Val.Map(("more", Val.True), ("cursor", Val.Int(64))), result));
        Assert.True(Val.Same(Val.Map(("more", Val.False), ("cursor", Val.Nil)), Paging.LevelPage(Items(2), Val.Nil).Result()));
    }

    [Fact]
    public void CursorFits_Given_a_cursor_of_1024_and_of_1025_encoded_bytes_When_checked_Then_the_first_fits_and_the_second_does_not()
    {
        // Arrange
        long baseSize = Paging.EncodedSize(Val.Str(new string('a', 1000)));
        Val fits = Val.Str(new string('a', (int)(1000 + 1024 - baseSize)));
        Val over = Val.Str(new string('a', (int)(1000 + 1025 - baseSize)));

        // Act / Assert
        Assert.Equal(1024, Paging.EncodedSize(fits));
        Assert.True(Paging.CursorFits(fits));
        Assert.False(Paging.CursorFits(over));
        Assert.True(Paging.CursorFits(Val.Int(64)));
    }

    [Fact]
    public void Split_Given_sizes_and_a_budget_When_split_Then_consecutive_parts_within_it_none_empty_and_no_part_could_take_the_next_item()
    {
        // Arrange
        int[] sizes = { 4, 4, 4, 9, 1, 10, 3, 3, 3, 3 };

        // Act
        List<List<int>> parts = Paging.Split(sizes, s => s, 10);

        // Assert
        Assert.Equal(sizes, parts.SelectMany(p => p));
        Assert.All(parts, p => Assert.InRange(p.Sum(), 1, 10));
        Assert.Equal(new[] { 8, 4, 10, 10, 9, 3 }, parts.Select(p => p.Sum()));
        for (int i = 0; i + 1 < parts.Count; i++)
        {
            Assert.True(parts[i].Sum() + parts[i + 1][0] > 10);
        }
    }

    [Fact]
    public void Split_Given_no_items_When_split_Then_no_parts_and_an_item_over_the_budget_is_too_large()
    {
        // Arrange / Act / Assert
        Assert.Empty(Paging.Split(Array.Empty<int>(), s => s, 10));
        Assert.Throws<ItemTooLargeException>(() => Paging.Split(new[] { 11 }, s => s, 10));
        Assert.Single(Paging.Split(new[] { 10 }, s => s, 10));
    }

    [Fact]
    public void Split_Given_effects_and_the_reply_budget_When_each_part_is_made_a_reply_Then_no_reply_passes_the_frame_limit()
    {
        // Arrange
        const int Limit = 65_536; // the smallest max_frame the protocol allows
        var effects = new List<Val>();
        for (int i = 0; i < 3000; i++)
        {
            effects.Add(Val.Map(("kind", Val.Str("npc.place")), ("handle", Val.Handle((ulong)i << 32 | 7)), ("props", Val.Map(("n", Val.Str(new string('x', i % 90)))))));
        }
        Val result = Val.Map(("more", Val.True), ("cursor", Val.Nil));
        long budget = Paging.ReplyBudget(Limit, result, Paging.MaxCursorBytes);

        // Act
        List<List<Val>> parts = Paging.Split(effects, budget);

        // Assert
        Assert.True(parts.Count > 3);
        foreach (List<Val> part in parts)
        {
            Val withCursor = Val.Map(("more", Val.True), ("cursor", Val.Str(new string('c', 1000))));
            byte[] frame = FrameCodec.Encode(new ReplyFrame(ulong.MaxValue, 0, withCursor, part, new[] { Val.Nil }));
            Assert.True(frame.Length - 4 <= Limit, "a reply of " + (frame.Length - 4) + " bytes");
        }
        Assert.Equal(effects.Count, parts.Sum(p => p.Count));
    }

    [Fact]
    public void ReplyBudget_Given_a_limit_and_a_result_When_computed_Then_it_is_the_limit_less_the_empty_frame_the_count_growth_and_the_reserve()
    {
        // Arrange
        Val result = Val.Str("abc");
        int empty = FrameCodec.Encode(new ReplyFrame(ulong.MaxValue, ulong.MaxValue, result, Array.Empty<Val>(), Array.Empty<Val>())).Length - 4;

        // Act
        long budget = Paging.ReplyBudget(100_000, result);
        long reserved = Paging.ReplyBudget(100_000, result, 1000);

        // Assert
        Assert.Equal(100_000 - empty - 18, budget);
        Assert.Equal(budget - 1000, reserved);
    }
}
