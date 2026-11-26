// Not ported from a Crossfire file: the part of Crossfire's player controller (include/player.h, <c>struct pl</c>) that the commands of
// lane 7A read and write beyond what lanes 4C and 5D hold: <c>own_title</c> of the command <c>title</c> (docs/extension.md, "Commands").
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
namespace Crossfire.Extension.Gk;

internal sealed partial class Player
{
    /// <summary>The account's line that holds <see cref="OwnTitle"/> (the line <c>title</c> is the object's title, which Graal Kingdoms reads as <c>mudtitle</c>).</summary>
    public const string OwnTitleKey = "mudowntitle";

    /// <summary>C <c>own_title</c>: the title the player gave himself with the command <c>title</c> ("" for none). Kept as the extra line <see cref="OwnTitleKey"/> of the player's object, so the account file saves it (login.c save_player writes <c>title</c>, check_login reads it) and an unload and load keeps it.</summary>
    public string OwnTitle
    {
        get => State.Ob.Extra?.Get(OwnTitleKey) ?? "";
        set
        {
            if (value.Length == 0)
            {
                State.Ob.Extra?.Remove(OwnTitleKey);
            }
            else
            {
                (State.Ob.Extra ??= new ExtraKeys()).Set(OwnTitleKey, value);
            }
            State.Dirty = true;
        }
    }

    /// <summary>C <c>title</c>: the name of the player's archetype (login.c copies <c>arch-&gt;clone.name</c>), cut to <c>MAX_NAME</c> - 1 characters.</summary>
    public string Title
    {
        get
        {
            return Commands.CutTitle(State.Ob.Arch?.Clone.Name ?? "");
        }
    }
}
