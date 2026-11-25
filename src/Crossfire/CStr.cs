// Not ported from a Crossfire file: the behaviour of the C runtime (atoi, atol, atof, printf %f) that the ported code relies on.
// Part of the Crossfire for Alpaxis port; licensed under the GNU General Public License, version 2 or (at your option) any later
// version, as the rest of the port (see the LICENSE file).
using System.Globalization;
using System.Text;

namespace Crossfire;

/// <summary>
/// The C library functions the Crossfire code relies on, with their results: atoi, atol, atof, strcmp order and the sign of
/// <c>char</c>. Not ported from a Crossfire file (C runtime behaviour that the ported code depends on).
/// </summary>
public static class CStr
{
    private static bool IsSpace(char c) => c == ' ' || (c >= '\t' && c <= '\r');

    /// <summary>
    /// C <c>atol</c> on a machine whose <c>long</c> has 32 bits (see docs/differences.md): leading white space, an optional sign,
    /// digits, saturating at the range of the type like <c>strtol</c>; everything after the digits is ignored.
    /// </summary>
    public static int Atol(string? s) => (int)Strtol(s, int.MinValue, int.MaxValue);

    /// <summary>
    /// C <c>atoi</c> as glibc has it: <c>(int)strtol(s, NULL, 10)</c> with a <c>long</c> of 32 bits, so the same as <see cref="Atol"/>.
    /// </summary>
    public static int Atoi(string? s) => Atol(s);

    private static long Strtol(string? s, long min, long max)
    {
        if (s == null)
        {
            return 0;
        }
        int i = 0;
        while (i < s.Length && IsSpace(s[i]))
        {
            i++;
        }
        bool neg = false;
        if (i < s.Length && (s[i] == '+' || s[i] == '-'))
        {
            neg = s[i] == '-';
            i++;
        }
        long v = 0;
        bool over = false;
        while (i < s.Length && s[i] >= '0' && s[i] <= '9')
        {
            int d = s[i] - '0';
            if (!over)
            {
                if (v > (long.MaxValue - d) / 10)
                {
                    over = true;
                }
                else
                {
                    v = v * 10 + d;
                }
            }
            i++;
        }
        if (over)
        {
            return neg ? min : max;
        }
        if (neg)
        {
            v = -v;
        }
        if (v < min)
        {
            return min;
        }
        if (v > max)
        {
            return max;
        }
        return v;
    }

    /// <summary>C <c>atof</c>: the longest decimal prefix (white space, sign, digits, a point, an exponent); 0 when there is none.</summary>
    public static double Atof(string? s)
    {
        if (s == null)
        {
            return 0;
        }
        int i = 0;
        while (i < s.Length && IsSpace(s[i]))
        {
            i++;
        }
        int start = i;
        if (i < s.Length && (s[i] == '+' || s[i] == '-'))
        {
            i++;
        }
        int digits = 0;
        while (i < s.Length && s[i] >= '0' && s[i] <= '9')
        {
            i++;
            digits++;
        }
        if (i < s.Length && s[i] == '.')
        {
            i++;
            while (i < s.Length && s[i] >= '0' && s[i] <= '9')
            {
                i++;
                digits++;
            }
        }
        if (digits == 0)
        {
            return 0;
        }
        if (i < s.Length && (s[i] == 'e' || s[i] == 'E'))
        {
            int j = i + 1;
            if (j < s.Length && (s[j] == '+' || s[j] == '-'))
            {
                j++;
            }
            int expDigits = 0;
            while (j < s.Length && s[j] >= '0' && s[j] <= '9')
            {
                j++;
                expDigits++;
            }
            if (expDigits > 0)
            {
                i = j;
            }
        }
        return double.Parse(s.AsSpan(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture);
    }

    /// <summary>The C conversion of a floating value to an integer type (truncation, wrap-around instead of undefined behaviour).</summary>
    public static int ToInt(double d)
    {
        if (double.IsNaN(d))
        {
            return int.MinValue;
        }
        if (d >= 2147483648.0 || d <= -2147483649.0)
        {
            return unchecked((int)(long)Math.Clamp(d, long.MinValue, long.MaxValue));
        }
        return (int)d;
    }

    /// <summary>The C conversion of a double to <c>uint32</c> (through a 64-bit integer, as the machine does).</summary>
    public static uint ToUInt(double d) => unchecked((uint)(long)Math.Clamp(d, long.MinValue, long.MaxValue));

    /// <summary>C <c>strcmp</c> on bytes (the strings hold one char per byte).</summary>
    public static int Strcmp(string a, string b) => string.CompareOrdinal(a, b);

    /// <summary>The Latin-1 decoding that keeps every byte of a file as one char (the C code works on bytes).</summary>
    public static readonly Encoding Latin1 = Encoding.Latin1;

    /// <summary>C <c>%f</c> of a double.</summary>
    public static string Fmt(double d) => d.ToString("F6", CultureInfo.InvariantCulture);
}
