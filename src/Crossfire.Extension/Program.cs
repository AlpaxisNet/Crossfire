// Not ported from a Crossfire file: the entry point of the Crossfire extension. The game server starts it with exactly two arguments,
// --connect <absolute socket path>, with the extension's folder as the working directory (docs/protocol.md section 1).
//
// This file is part of the Crossfire extension and is under the GNU General Public License, version 2 or (at your option) any later
// version (see LICENSE).
using System.Net.Sockets;

namespace Crossfire.Extension;

internal static class Program
{
    private const int ExitUsage = 64;
    private const int ExitNoConnection = 69;

    private static int Main(string[] args)
    {
        if (args.Length != 2 || args[0] != "--connect" || args[1].Length == 0)
        {
            Console.Error.WriteLine("usage: Crossfire.Extension --connect <absolute socket path>");
            return ExitUsage;
        }
        var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        try
        {
            socket.Connect(new UnixDomainSocketEndPoint(args[1]));
        }
        catch (Exception e) when (e is SocketException or ArgumentException)
        {
            Console.Error.WriteLine("cannot connect to " + args[1] + ": " + e.Message);
            socket.Dispose();
            return ExitNoConnection;
        }
        using var stream = new NetworkStream(socket, ownsSocket: true);
        return ExtensionRunner.Run(stream);
    }
}
