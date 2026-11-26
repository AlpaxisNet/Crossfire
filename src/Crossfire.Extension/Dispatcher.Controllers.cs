// Not ported from a Crossfire file: the player controllers of the Crossfire extension (the C <c>struct pl</c> of a logged-in player)
// and the end of a request: the one flush of the controllers that asked for it, before the REPLY is built (docs/mud-phase3.md
// sections 3 and 5).
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using Crossfire.Extension.Gk;

namespace Crossfire.Extension;

internal sealed partial class Dispatcher
{
    // the controllers that asked for a flush in the running request, in the order they asked (a controller is in it once)
    private readonly List<Player> toFlush = new();

    // the controllers flushed by the running request (kept to forget their strings when the REPLY is replaced by an error)
    private readonly List<Player> flushed = new();

    /// <summary>Makes the controller of a player who has just logged in and sets it as the <c>Contr</c> of the player's object.</summary>
    private void AttachController(PlayerState player)
    {
        player.Ob.Contr = new Player(player, TouchController);
    }

    /// <summary>Takes the controller away (at logout); a flush that was asked for and not done is dropped with it.</summary>
    private void DetachController(PlayerState player)
    {
        if (player.Contr is { } contr)
        {
            toFlush.Remove(contr);
            flushed.Remove(contr);
            contr.NeedsFlush = false;
        }
        player.Ob.Contr = null;
    }

    private void TouchController(Player player) => toFlush.Add(player);

    /// <summary>The controller of a logged-in account, or null.</summary>
    internal Player? ControllerOf(string account) => players.TryGetValue(account, out PlayerState? ps) ? ps.Contr : null;

    /// <summary>
    /// The effects at the end of a request: for each controller that asked for it, in the order it asked, <c>FlushShown</c> (lane 4C's
    /// part: the item strings whose text changed, the ones that went) and then the test hook. A flush that adds effects adds them to
    /// the request's, after the effects the request made itself, so that a REPLY keeps the order of what happened. It runs for a
    /// request that ends with status 0 only: an error carries no effects (protocol section 4.2), so what was marked stays for the
    /// next request.
    /// </summary>
    private void FlushControllers()
    {
        // a flush may touch controllers again (a nested mark): go on until none is left, a few rounds at most
        for (int round = 0; round < 8 && toFlush.Count > 0; round++)
        {
            Player[] now = toFlush.ToArray();
            toFlush.Clear();
            foreach (Player contr in now)
            {
                contr.NeedsFlush = false;
                FlushShown(contr.State);
                options.FlushHook?.Invoke(this, contr.State);
                contr.NeedsResend = false;
                if (!flushed.Contains(contr))
                {
                    flushed.Add(contr);
                }
            }
        }
    }

    /// <summary>Lane 4C writes the flush of a player's <see cref="ShownTable"/> here (in a partial file of its own); unimplemented it does nothing.</summary>
    partial void FlushShown(PlayerState player);

    /// <summary>The REPLY that carried the flush's effects did not leave: what it set is not known to the client.</summary>
    private void ForgetFlushed()
    {
        foreach (Player contr in flushed)
        {
            contr.Shown.Forget();
        }
    }
}
