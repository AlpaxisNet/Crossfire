// Not ported from a Crossfire file: tests of the rules of docs/protocol.md sections 2, 3 and 9 that the golden vectors do not show.
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using Crossfire.Extension.Protocol;

namespace Crossfire.Extension.Tests;

public class CodecTests
{
    private static byte[] B(params byte[] bytes) => bytes;

    private static Frame? ReadFrame(byte[] bytes, int max = FrameCodec.InitialMaxFrame)
    {
        using var s = new MemoryStream(bytes);
        return FrameCodec.Read(s, max);
    }

    private static byte[] Framed(params byte[] body) => new byte[] { (byte)body.Length, 0, 0, 0 }.Concat(body).ToArray();

    [Fact]
    public void Decode_Given_a_varint_that_is_not_the_shortest_form_When_decoded_Then_it_is_accepted()
    {
        // Arrange
        byte[] bytes = B(3, 0x80, 0x00); // int 0 written in two bytes

        // Act
        Val v = ValueCodec.Decode(bytes);

        // Assert
        Assert.Equal(0L, v.AsInt);
    }

    [Fact]
    public void Decode_Given_a_varint_of_ten_bytes_that_fits_When_decoded_Then_the_largest_value_comes_out()
    {
        // Arrange
        byte[] bytes = B(3, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0x01); // zigzag of long.MinValue

        // Act
        Val v = ValueCodec.Decode(bytes);

        // Assert
        Assert.Equal(long.MinValue, v.AsInt);
    }

    [Fact]
    public void Decode_Given_a_varint_that_overflows_64_bits_When_decoded_Then_a_protocol_error()
    {
        // Arrange
        byte[] bytes = B(3, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0x02);

        // Act
        ProtocolException e = Assert.Throws<ProtocolException>(() => ValueCodec.Decode(bytes));

        // Assert
        Assert.Contains("64 bits", e.Message);
    }

    [Fact]
    public void Decode_Given_a_varint_of_eleven_bytes_When_decoded_Then_a_protocol_error()
    {
        // Arrange
        byte[] bytes = B(3, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x00);

        // Act
        ProtocolException e = Assert.Throws<ProtocolException>(() => ValueCodec.Decode(bytes));

        // Assert
        Assert.Contains("10 bytes", e.Message);
    }

    [Fact]
    public void Decode_Given_an_unknown_tag_When_decoded_Then_a_protocol_error()
    {
        // Arrange / Act / Assert
        Assert.Throws<ProtocolException>(() => ValueCodec.Decode(B(9)));
    }

    [Fact]
    public void Decode_Given_a_body_that_ends_inside_a_value_When_decoded_Then_a_protocol_error()
    {
        // Arrange / Act / Assert
        Assert.Throws<ProtocolException>(() => ValueCodec.Decode(B(4, 0, 0, 0)));
        Assert.Throws<ProtocolException>(() => ValueCodec.Decode(B(3)));
    }

    [Fact]
    public void Decode_Given_bytes_after_the_value_When_decoded_Then_a_protocol_error()
    {
        // Arrange / Act / Assert
        Assert.Throws<ProtocolException>(() => ValueCodec.Decode(B(0, 0)));
    }

    [Fact]
    public void Decode_Given_a_string_longer_than_the_body_When_decoded_Then_a_protocol_error()
    {
        // Arrange / Act / Assert
        Assert.Throws<ProtocolException>(() => ValueCodec.Decode(B(5, 4, 0x61)));
    }

    [Fact]
    public void Decode_Given_a_list_with_more_elements_than_bytes_left_When_decoded_Then_a_protocol_error()
    {
        // Arrange / Act / Assert
        Assert.Throws<ProtocolException>(() => ValueCodec.Decode(B(7, 3, 0, 0)));
        Assert.Throws<ProtocolException>(() => ValueCodec.Decode(B(8, 2, 1, 0x61, 0)));
    }

    [Fact]
    public void Decode_Given_a_map_with_a_duplicate_key_When_decoded_Then_a_protocol_error()
    {
        // Arrange
        byte[] bytes = B(8, 2, 1, 0x61, 0, 1, 0x61, 0);

        // Act
        ProtocolException e = Assert.Throws<ProtocolException>(() => ValueCodec.Decode(bytes));

        // Assert
        Assert.Contains("duplicate", e.Message);
    }

    [Fact]
    public void Decode_Given_two_keys_that_differ_in_bytes_but_not_after_replacement_When_decoded_Then_they_are_not_duplicates()
    {
        // Arrange: ff and fe are both ill-formed and both become U+FFFD
        byte[] bytes = B(8, 2, 1, 0xff, 0, 1, 0xfe, 0);

        // Act
        Val v = ValueCodec.Decode(bytes);

        // Assert
        Assert.Equal(2, v.Pairs.Count);
        Assert.Equal("�", v.Pairs[0].Key);
    }

    [Fact]
    public void Decode_Given_a_string_with_an_ill_formed_sequence_When_decoded_Then_it_becomes_u_fffd()
    {
        // Arrange / Act
        Val v = ValueCodec.Decode(B(5, 2, 0x61, 0xff));

        // Assert
        Assert.Equal("a�", v.AsString);
    }

    private static byte[] Nested(int depth)
    {
        // depth-1 lists of one element round a nil: the nil is at depth `depth`
        var bytes = new List<byte>();
        for (int i = 1; i < depth; i++)
        {
            bytes.Add(7);
            bytes.Add(1);
        }
        bytes.Add(0);
        return bytes.ToArray();
    }

    [Fact]
    public void Decode_Given_a_value_of_depth_16_When_decoded_Then_it_is_accepted()
    {
        // Arrange / Act
        Val v = ValueCodec.Decode(Nested(16));

        // Assert
        Assert.Equal(ValKind.List, v.Kind);
    }

    [Fact]
    public void Decode_Given_a_value_of_depth_17_When_decoded_Then_a_protocol_error()
    {
        // Arrange / Act
        ProtocolException e = Assert.Throws<ProtocolException>(() => ValueCodec.Decode(Nested(17)));

        // Assert
        Assert.Contains("deeper", e.Message);
    }

    [Fact]
    public void Roundtrip_Given_nan_and_infinity_When_encoded_and_decoded_Then_they_survive()
    {
        // Arrange
        Val v = Val.List(Val.Float(double.NaN), Val.Float(double.PositiveInfinity), Val.Float(double.NegativeInfinity));

        // Act
        Val back = ValueCodec.Decode(ValueCodec.Encode(v));

        // Assert
        Assert.True(double.IsNaN(back.Items[0].AsFloat));
        Assert.True(double.IsPositiveInfinity(back.Items[1].AsFloat));
        Assert.True(double.IsNegativeInfinity(back.Items[2].AsFloat));
    }

    [Theory]
    [InlineData(2.9, 2L)]
    [InlineData(-2.9, -2L)]
    [InlineData(1e30, long.MaxValue)]
    [InlineData(-1e30, long.MinValue)]
    [InlineData(double.NaN, 0L)]
    public void AsNumberToInt_Given_a_float_When_converted_Then_it_truncates_toward_zero_and_clamps(double input, long expected)
    {
        // Arrange / Act
        long? n = Val.Float(input).AsNumberToInt();

        // Assert
        Assert.Equal(expected, n);
    }

    [Fact]
    public void AsNumberToInt_Given_a_string_or_a_bool_or_a_handle_When_converted_Then_null()
    {
        // Arrange / Act / Assert
        Assert.Null(Val.Str("1").AsNumberToInt());
        Assert.Null(Val.True.AsNumberToInt());
        Assert.Null(Val.Handle(1).AsNumberToInt());
        Assert.Null(Val.Nil.AsNumberToInt());
    }

    [Fact]
    public void Same_Given_maps_in_another_key_order_When_compared_Then_equal_but_lists_in_another_order_differ()
    {
        // Arrange
        Val a = Val.Map(("x", Val.Int(1)), ("y", Val.List(Val.Int(1), Val.Int(2))));
        Val b = Val.Map(("y", Val.List(Val.Int(1), Val.Int(2))), ("x", Val.Int(1)));
        Val c = Val.Map(("x", Val.Int(1)), ("y", Val.List(Val.Int(2), Val.Int(1))));

        // Act / Assert
        Assert.True(Val.Same(a, b));
        Assert.False(Val.Same(a, c));
        Assert.False(Val.Same(Val.Int(1), Val.Float(1)));
        Assert.False(Val.Same(Val.Map(("x", Val.Int(1))), Val.Map(("z", Val.Int(1)))));
    }

    [Fact]
    public void Read_Given_a_length_of_zero_When_read_Then_a_protocol_error()
    {
        // Arrange / Act / Assert
        Assert.Throws<ProtocolException>(() => ReadFrame(B(0, 0, 0, 0)));
    }

    [Fact]
    public void Read_Given_a_length_above_the_maximum_When_read_Then_a_protocol_error_before_the_body_is_read()
    {
        // Arrange: the length says 65,537, the stream holds nothing more
        byte[] bytes = B(1, 0, 1, 0);

        // Act / Assert
        Assert.Throws<ProtocolException>(() => ReadFrame(bytes, 65_536));
    }

    [Fact]
    public void Read_Given_a_length_equal_to_the_maximum_When_the_body_is_complete_Then_it_is_read()
    {
        // Arrange: a LOG frame padded to exactly 65,536 bytes
        string text = new string('x', 65_536 - 1 - 1 - 1 - 3);
        byte[] bytes = FrameCodec.Encode(new LogFrame(1, text));

        // Act
        Frame? f = ReadFrame(bytes, 65_536);

        // Assert
        Assert.Equal(65_536 + 4, bytes.Length);
        Assert.IsType<LogFrame>(f);
    }

    [Fact]
    public void Read_Given_an_empty_stream_or_one_that_ends_inside_a_frame_When_read_Then_null()
    {
        // Arrange / Act / Assert
        Assert.Null(ReadFrame(B()));
        Assert.Null(ReadFrame(B(2, 0)));
        Assert.Null(ReadFrame(B(5, 0, 0, 0, 6)));
    }

    [Fact]
    public void Read_Given_an_unknown_kind_When_read_Then_a_protocol_error()
    {
        // Arrange / Act / Assert
        Assert.Throws<ProtocolException>(() => ReadFrame(Framed(9, 0)));
    }

    [Fact]
    public void Read_Given_bytes_after_the_last_field_When_read_Then_a_protocol_error()
    {
        // Arrange / Act / Assert
        Assert.Throws<ProtocolException>(() => ReadFrame(Framed(6, 9, 0)));
    }

    [Fact]
    public void Read_Given_a_req_whose_releases_hold_a_non_handle_When_read_Then_a_protocol_error()
    {
        // Arrange: REQ 1, op 1, releases [nil], ctx {}, args []
        byte[] bytes = Framed(2, 1, 1, 7, 1, 0, 8, 0, 7, 0);

        // Act / Assert
        Assert.Throws<ProtocolException>(() => ReadFrame(bytes));
    }

    [Fact]
    public void Read_Given_a_req_whose_ctx_is_not_a_map_When_read_Then_a_protocol_error()
    {
        // Arrange: REQ 1, op 1, releases [], ctx [], args []
        byte[] bytes = Framed(2, 1, 1, 7, 0, 7, 0, 7, 0);

        // Act / Assert
        Assert.Throws<ProtocolException>(() => ReadFrame(bytes));
    }

    [Fact]
    public void Read_Given_a_reply_whose_touched_is_not_nil_When_read_Then_a_protocol_error()
    {
        // Arrange: REPLY 1, status 0, result nil, effects [], events [], touched true
        byte[] bytes = Framed(3, 1, 0, 0, 7, 0, 7, 0, 2);

        // Act / Assert
        Assert.Throws<ProtocolException>(() => ReadFrame(bytes));
    }

    [Fact]
    public void Read_Given_a_hello_that_is_not_a_map_When_read_Then_a_protocol_error()
    {
        // Arrange / Act / Assert
        Assert.Throws<ProtocolException>(() => ReadFrame(Framed(1, 7, 0)));
    }

    [Fact]
    public void Read_Given_a_log_frame_When_read_Then_level_and_text_are_kept()
    {
        // Arrange / Act
        var log = (LogFrame)ReadFrame(FrameCodec.Encode(new LogFrame(7, "x")))!;

        // Assert
        Assert.Equal(7UL, log.Level);
        Assert.Equal("x", log.Text);
    }
}
