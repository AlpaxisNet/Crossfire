// Not ported from a Crossfire file: the file names of the Crossfire extension's level and account files.
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using System.Globalization;
using System.Text;

namespace Crossfire.Extension;

/// <summary>
/// The names of the files the extension writes for a level or an account. A name can hold any byte (an account is compared as bytes),
/// a file name cannot: <see cref="Escape"/> keeps the ASCII letters, digits, <c>_</c>, <c>-</c> and <c>.</c> (not at the start) and
/// writes every other byte of the name's UTF-8 text as <c>%</c> and two capital hexadecimal digits. It is reversible
/// (<see cref="TryUnescape"/>), keeps different names apart (the percent sign is escaped too) and never makes a name of a folder
/// (a slash is escaped) or a hidden file (a leading dot is escaped).
/// </summary>
internal static class FileNames
{
    /// <summary>The longest escaped name this extension makes a file of (most file systems allow 255 bytes, and the file has a suffix).</summary>
    public const int MaxEscapedLength = 200;

    public const string LevelFolder = "levels";
    public const string AccountFolder = "accounts";
    public const string LevelSuffix = ".map";
    public const string AccountSuffix = ".txt";

    private static bool Plain(byte b) => b is >= (byte)'a' and <= (byte)'z' or >= (byte)'A' and <= (byte)'Z' or >= (byte)'0' and <= (byte)'9' or (byte)'_' or (byte)'-';

    /// <summary>The escaped form of a name.</summary>
    public static string Escape(string name)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(name);
        var sb = new StringBuilder(bytes.Length);
        for (int i = 0; i < bytes.Length; i++)
        {
            byte b = bytes[i];
            if (Plain(b) || (b == (byte)'.' && i > 0))
            {
                sb.Append((char)b);
            }
            else
            {
                sb.Append('%').Append(b.ToString("X2", CultureInfo.InvariantCulture));
            }
        }
        return sb.ToString();
    }

    /// <summary>The name an escaped form stands for; false when the text is not one <see cref="Escape"/> makes.</summary>
    public static bool TryUnescape(string escaped, out string name)
    {
        var bytes = new List<byte>(escaped.Length);
        for (int i = 0; i < escaped.Length; i++)
        {
            char c = escaped[i];
            if (c == '%')
            {
                if (i + 2 >= escaped.Length || !Uri.IsHexDigit(escaped[i + 1]) || !Uri.IsHexDigit(escaped[i + 2])
                    || char.IsLower(escaped[i + 1]) || char.IsLower(escaped[i + 2]))
                {
                    name = "";
                    return false;
                }
                bytes.Add(Convert.ToByte(escaped.Substring(i + 1, 2), 16));
                i += 2;
            }
            else if (c < 128 && (Plain((byte)c) || (c == '.' && i > 0)))
            {
                bytes.Add((byte)c);
            }
            else
            {
                name = "";
                return false;
            }
        }
        name = Encoding.UTF8.GetString(bytes.ToArray());
        return true;
    }

    /// <summary>The file of a level, relative to the data folder, or null when the name is empty or its escaped form is too long.</summary>
    public static string? LevelFile(string level) => FileOf(LevelFolder, level, LevelSuffix);

    /// <summary>The file of an account, relative to the data folder, or null when the name is empty or its escaped form is too long.</summary>
    public static string? AccountFile(string account) => FileOf(AccountFolder, account, AccountSuffix);

    private static string? FileOf(string folder, string name, string suffix)
    {
        if (name.Length == 0)
        {
            return null;
        }
        string e = Escape(name);
        return e.Length > MaxEscapedLength ? null : folder + "/" + e + suffix;
    }
}
