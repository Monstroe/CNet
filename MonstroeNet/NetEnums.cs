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

    public enum DisconnectCode
    {
        ConnectionClosed = -1,
        ConnectionClosedForcefully = -8,
        ConnectionClosedWithMessage = -2,
        ConnectionRejected = -3,
        PacketOverBufferSize = -4,
        PacketOverMaxSize = -5,
        InvalidPacket = -6,
        SocketError = -7,
    }
}
