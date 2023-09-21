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
        ConnectionClosedSafely = -1,
        ConnectionClosedForcefully = -2,
        ConnectionRejected = -3,
        PacketOverBufferSize = -4,
        PacketOverMaxSize = -5,
        //PacketUnderMinSize = -6,
        HostClosed = -6,
        InvalidPacket = -7,
        SocketError = -8,
    }
}
