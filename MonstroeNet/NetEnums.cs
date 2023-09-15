using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MonstroeNet
{
    public enum PacketProtocol
    {
        TCP,
        UDP
    }

    public enum SystemMode
    {
        Client,
        Listener
    }

    public enum DisconnectReason
    {
        ConnectionRejected,
        ConnectionClosedSafely,
        ConnectionClosedForcefully,
        SocketError
    }
}
