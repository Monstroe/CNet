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
        ConnectionRejected = -1,
        ConnectionClosedSafely = -2,
        ConnectionClosedForcefully = -3,
        ReceivedPacketOverBufferSize = -4,
        ReceivedPacketOverMaxSize = -5,
        ReceivedPacketUnderMinSize = -6,
        ReceivedInvalidPacket = -7,
        SocketError = -8,
    }
}
