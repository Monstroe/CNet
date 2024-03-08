using System;

namespace CNet
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
        ConnectionClosedWithMessage = -2,
        ConnectionClosedForcefully = -3,
        ConnectionRejected = -4,
        PacketOverBufferSize = -5,
        PacketOverMaxSize = -6,
        InvalidPacket = -7,
        SocketError = -8,
    }
}
