// Not ported from a Crossfire file: the layout of a gmap (docs/protocol.md section 7.1, protocol 1.1) and the arithmetic that resolves
// a position in the gmap's tiles to a segment and a position in it. Crossfire has no gmaps: this is the extension's own rule.
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
namespace Crossfire.Extension.Gk;

/// <summary>A place resolved from a gmap: the segment's name (a level key) and the position in the segment's own tiles.</summary>
internal readonly record struct SegmentPlace(string Level, double X, double Y, int Column, int Row);

/// <summary>
/// A gmap's layout as <c>gmap.layout</c> told it: <see cref="Width"/> by <see cref="Height"/> segments, row by row, each a level key
/// (lower case) or the empty string for a cell without a level, each <see cref="Size"/> tiles wide.
/// </summary>
internal sealed class GmapLayout
{
    /// <summary>The most segments, and the most in a row or a column (protocol section 7.1).</summary>
    public const int Limit = 4096;

    private readonly string[] segments;
    private readonly Dictionary<string, int> cellOf = new(StringComparer.Ordinal);

    public GmapLayout(string name, int width, int height, IReadOnlyList<string> segments, int size)
    {
        Name = name;
        Width = width;
        Height = height;
        Size = size;
        this.segments = segments.ToArray();
        for (int i = 0; i < this.segments.Length; i++)
        {
            if (this.segments[i].Length > 0)
            {
                cellOf[this.segments[i]] = i;
            }
        }
    }

    /// <summary>The gmap's name (a level key).</summary>
    public string Name { get; }

    public int Width { get; }

    public int Height { get; }

    /// <summary>The side of a segment in tiles.</summary>
    public int Size { get; }

    /// <summary>The level key at a cell, "" for a cell without a level.</summary>
    public string SegmentAt(int column, int row) => segments[column + row * Width];

    /// <summary>The cell of a segment (a level key) in this gmap.</summary>
    public bool TryFindSegment(string key, out int column, out int row)
    {
        if (cellOf.TryGetValue(key, out int cell))
        {
            column = cell % Width;
            row = cell / Width;
            return true;
        }
        column = row = 0;
        return false;
    }

    /// <summary>The segments with a name, in cell order (row by row).</summary>
    public IEnumerable<(string Level, int Column, int Row)> Cells()
    {
        for (int i = 0; i < segments.Length; i++)
        {
            if (segments[i].Length > 0)
            {
                yield return (segments[i], i % Width, i / Width);
            }
        }
    }

    /// <summary>
    /// The segment that holds a position of the gmap and the position in it: the column is <c>floor(x / size)</c>, the row
    /// <c>floor(y / size)</c>. False for a position outside the grid, with a component that is NaN or infinite, or in a cell without a level.
    /// </summary>
    public bool TryResolve(double x, double y, out SegmentPlace place)
    {
        place = default;
        if (!double.IsFinite(x) || !double.IsFinite(y))
        {
            return false;
        }
        double column = Math.Floor(x / Size);
        double row = Math.Floor(y / Size);
        if (column < 0 || row < 0 || column >= Width || row >= Height)
        {
            return false;
        }
        int c = (int)column;
        int r = (int)row;
        string level = segments[c + r * Width];
        if (level.Length == 0)
        {
            return false;
        }
        place = new SegmentPlace(level, x - c * (double)Size, y - r * (double)Size, c, r);
        return true;
    }

    /// <summary>The position in the gmap's tiles of a position in the tiles of the segment at a cell.</summary>
    public (double X, double Y) ToGmap(int column, int row, double x, double y) => (column * (double)Size + x, row * (double)Size + y);
}
