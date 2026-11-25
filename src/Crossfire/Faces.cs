// Port to C# of Crossfire's server (commit 8b7ee7f90, 2002-06-27): common/image.c, common/anim.c, include/face.h, include/global.h.
//
//     CrossFire, A Multiplayer game for X-windows
//
//     common/image.c:
//         Copyright (C) 2002 Mark Wedel & Crossfire Development Team
//         Copyright (C) 1992 Frank Tore Johansen
//     common/anim.c:
//         Copyright (C) 2000 Mark Wedel
//         Copyright (C) 1992 Frank Tore Johansen
//     include/face.h:
//         Copyright (C) 1994 Mark Wedel
//         Copyright (C) 1992 Frank Tore Johansen
//     include/global.h:
//         Copyright (C) 2002 Mark Wedel & Crossfire Development Team
//         Copyright (C) 1992 Frank Tore Johansen
//
//    This program is free software; you can redistribute it and/or modify
//    it under the terms of the GNU General Public License as published by
//    the Free Software Foundation; either version 2 of the License, or
//    (at your option) any later version.
//
//    This program is distributed in the hope that it will be useful,
//    but WITHOUT ANY WARRANTY; without even the implied warranty of
//    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//    GNU General Public License for more details.
//
//    You should have received a copy of the GNU General Public License
//    along with this program; if not, write to the Free Software
//    Foundation, Inc., 675 Mass Ave, Cambridge, MA 02139, USA.
// ---- end of notices ----
namespace Crossfire;

/// <summary>logger.h: the levels of LOG().</summary>
public enum LogLevel
{
    /// <summary>llevError.</summary>
    Error = 0,
    /// <summary>llevInfo.</summary>
    Info = 1,
    /// <summary>llevDebug.</summary>
    Debug = 2,
    /// <summary>llevMonster.</summary>
    Monster = 3,
}

/// <summary>The C <c>LOG(level, ...)</c> macro: where the messages of the ported code go.</summary>
public delegate void CfLog(LogLevel level, string message);

/// <summary>
/// include/face.h <c>New_Face</c>: a picture of the client of Crossfire as the server knows it, by name and number. The
/// pixel data belongs to the client and is not ported.
/// </summary>
public sealed class NewFace
{
    /// <summary>face.h: name of the picture (as in the bmaps file).</summary>
    public string Name { get; }

    /// <summary>face.h: number, the index in <c>new_faces</c>.</summary>
    public int Number { get; }

    /// <summary>face.h: visibility (read from a faces file; not used by the object model).</summary>
    public byte Visibility { get; set; }

    /// <summary>face.h: magicmap colour (read from a faces file; not used by the object model).</summary>
    public byte Magicmap { get; set; } = 255;

    /// <summary>Creates a face.</summary>
    public NewFace(string name, int number)
    {
        Name = name;
        Number = number;
    }
}

/// <summary>
/// common/image.c: <c>new_faces</c>, <c>FindFace</c> and the special faces. The C code reads the names from the bmaps file that
/// collect.pl writes; the port takes them from <see cref="Register"/> and, with <see cref="AutoRegister"/> (the default), from the
/// archetypes themselves, so that no face name of a world is lost (docs/differences.md).
/// </summary>
public sealed class FaceTable
{
    private readonly List<NewFace> faces = new();
    private readonly Dictionary<string, int> byName = new(StringComparer.Ordinal);
    private readonly CfLog log;

    /// <summary>image.c: blank_face, the face an object has until it is given one.</summary>
    public NewFace BlankFace { get; }

    /// <summary>image.c: empty_face.</summary>
    public NewFace EmptyFace { get; }

    /// <summary>
    /// When true, <see cref="FindFace"/> gives a new number to a name it does not know instead of returning the error value
    /// (what C does after a bmaps file is read). Not in C.
    /// </summary>
    public bool AutoRegister { get; set; } = true;

    /// <summary>The number of faces.</summary>
    public int Count => faces.Count;

    /// <summary>
    /// Creates the table with the picture number 0, <c>bug.111</c> (what collect.pl puts first in bmaps), and the special faces of
    /// image.c's init_faces: blank, empty.
    /// </summary>
    public FaceTable(CfLog log)
    {
        this.log = log;
        Register("bug.111");
        BlankFace = faces[Register(Define_BLANK_FACE_NAME)];
        EmptyFace = faces[Register(Define_EMPTY_FACE_NAME)];
    }

    /// <summary>define.h BLANK_FACE_NAME.</summary>
    public const string Define_BLANK_FACE_NAME = "blank.111";

    /// <summary>define.h EMPTY_FACE_NAME.</summary>
    public const string Define_EMPTY_FACE_NAME = "empty.111";

    /// <summary>Adds a face (or returns the number of the one of that name).</summary>
    public int Register(string name)
    {
        if (byName.TryGetValue(name, out int n))
        {
            return n;
        }
        n = faces.Count;
        faces.Add(new NewFace(name, n));
        byName[name] = n;
        return n;
    }

    /// <summary>The C <c>&amp;new_faces[i]</c>. A number the table does not have gives face 0 (C reads outside the array).</summary>
    public NewFace this[int i] => i >= 0 && i < faces.Count ? faces[i] : faces[0];

    /// <summary>
    /// image.c <c>FindFace</c>: the number of the face <paramref name="name"/>. An integer name (atoi not 0) is the number itself,
    /// with a warning; the text after a newline is cut; a name that is not known gives <paramref name="error"/> (see
    /// <see cref="AutoRegister"/>).
    /// </summary>
    public int FindFace(string name, int error)
    {
        int i = CStr.Atoi(name);
        if (i != 0)
        {
            log(LogLevel.Error, "Warning: Integer face name used: " + name + "\n");
            return i;
        }
        int nl = name.IndexOf('\n');
        if (nl >= 0)
        {
            name = name.Substring(0, nl);
        }
        if (byName.TryGetValue(name, out int n))
        {
            return n;
        }
        return AutoRegister ? Register(name) : error;
    }
}

/// <summary>global.h <c>Animations</c>: one animation of common/anim.c.</summary>
public sealed class Animation
{
    /// <summary>global.h: name.</summary>
    public string Name = "";
    /// <summary>global.h: num, the index in the table (used by bsearch).</summary>
    public int Num;
    /// <summary>global.h: num_animations, the number of frames.</summary>
    public int NumAnimations;
    /// <summary>global.h: facings.</summary>
    public int Facings;
    /// <summary>global.h: faces, the face number of each frame.</summary>
    public int[] Faces = Array.Empty<int>();
}

/// <summary>common/anim.c: the animations table, <c>init_anim</c> and <c>find_animation</c>.</summary>
public sealed class AnimationTable
{
    private readonly List<Animation> animations = new();
    private readonly FaceTable faces;
    private readonly CfLog log;

    /// <summary>anim.c: num_animations, the highest index in use (animation 0 is the placeholder).</summary>
    public int NumAnimations => animations.Count - 1;

    /// <summary>One animation by number (global.h <c>animations[i]</c>).</summary>
    public Animation this[int i] => animations[i];

    /// <summary>
    /// The start of <c>init_anim</c>: animation 0 is named <c>###none</c> (so that it sorts first) with one frame, the face 0, as
    /// the default of an object that is animated but has no animation id.
    /// </summary>
    public AnimationTable(FaceTable faces, CfLog log)
    {
        this.faces = faces;
        this.log = log;
        animations.Add(new Animation { Name = "###none", NumAnimations = 1, Faces = new[] { 0 }, Facings = 0 });
    }

    /// <summary>
    /// anim.c <c>init_anim</c> on the text of the animations file that collect.pl writes: <c>anim name</c>, an optional
    /// <c>facings n</c>, one face name per frame, <c>mina</c>. Lines beginning with # are skipped.
    /// </summary>
    public void Load(string text)
    {
        int numFrames = 0;
        var frames = new int[Define.MAX_ANIMATIONS];
        foreach (string raw in SplitLines(text))
        {
            string buf = raw;
            if (buf.Length > 0 && buf[0] == '#')
            {
                continue;
            }
            if (buf.StartsWith("anim ", StringComparison.Ordinal))
            {
                if (numFrames != 0)
                {
                    log(LogLevel.Error, "Didn't get a mina before " + buf + "\n");
                    numFrames = 0;
                }
                animations.Add(new Animation { Name = buf.Substring(5), Num = animations.Count, Facings = 1 });
            }
            else if (buf.StartsWith("mina", StringComparison.Ordinal))
            {
                Animation a = animations[^1];
                a.Faces = new int[numFrames];
                Array.Copy(frames, a.Faces, numFrames);
                a.NumAnimations = numFrames;
                if (a.Facings != 0 && numFrames % a.Facings != 0)
                {
                    log(LogLevel.Debug, "Animation " + a.Name + " frame numbers (" + numFrames + ") is not a multiple of facings (" + a.Facings + ")\n");
                }
                numFrames = 0;
            }
            else if (buf.StartsWith("facings", StringComparison.Ordinal))
            {
                Animation a = animations[^1];
                a.Facings = CStr.Atoi(buf.Substring(7));
                if (a.Facings == 0)
                {
                    log(LogLevel.Debug, "Animation " + a.Name + " has 0 facings, line=" + buf + "\n");
                    a.Facings = 1;
                }
            }
            else
            {
                int f = faces.FindFace(buf, 0);
                frames[numFrames++] = f;
                if (f == 0)
                {
                    log(LogLevel.Debug, "Could not find face " + buf + " for animation " + animations[^1].Name + "\n");
                }
            }
        }
        log(LogLevel.Debug, "done. got (" + NumAnimations + ")\n");
    }

    private static IEnumerable<string> SplitLines(string text)
    {
        int pos = 0;
        while (pos < text.Length)
        {
            int nl = text.IndexOf('\n', pos);
            if (nl < 0)
            {
                // fgets gives the last line without a newline and the C code then cuts its last character.
                yield return text.Substring(pos, Math.Max(0, text.Length - pos - 1));
                yield break;
            }
            yield return text.Substring(pos, nl - pos);
            pos = nl + 1;
        }
    }

    /// <summary>
    /// anim.c <c>find_animation</c>: the number of the animation called <paramref name="name"/> (a binary search like the
    /// C <c>bsearch</c>, over a table that collect.pl sorts by name); 0 with an error message when there is none.
    /// </summary>
    public int FindAnimation(string name)
    {
        int lo = 0;
        int hi = animations.Count;
        while (lo < hi)
        {
            int mid = (int)((uint)(lo + hi) / 2);
            int c = CStr.Strcmp(name, animations[mid].Name);
            if (c < 0)
            {
                hi = mid;
            }
            else if (c > 0)
            {
                lo = mid + 1;
            }
            else
            {
                return animations[mid].Num;
            }
        }
        log(LogLevel.Error, "Unable to find animation " + name + "\n");
        return 0;
    }
}
