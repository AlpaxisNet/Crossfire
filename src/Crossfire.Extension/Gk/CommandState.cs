// Not ported from a Crossfire file: the part of Crossfire's player controller (include/player.h, <c>struct pl</c>) that the commands of
// lane 7A read and write beyond what lanes 4C and 5D hold: <c>own_title</c> of the command <c>title</c> (docs/extension.md, "Commands").
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
namespace Crossfire.Extension.Gk;

internal sealed partial class Player
{
    /// <summary>C <c>own_title</c>: the title the player gave himself with the command <c>title</c> ("" for none). Not saved (docs/differences.md, "Commands").</summary>
    public string OwnTitle { get; set; } = "";

    /// <summary>C <c>title</c>: the name of the player's archetype (login.c copies <c>arch-&gt;clone.name</c>), cut to <c>MAX_NAME</c> - 1 characters.</summary>
    public string Title
    {
        get
        {
            string name = State.Ob.Arch?.Clone.Name ?? "";
            return name.Length >= Commands.MaxName ? name[..(Commands.MaxName - 1)] : name;
        }
    }
}
