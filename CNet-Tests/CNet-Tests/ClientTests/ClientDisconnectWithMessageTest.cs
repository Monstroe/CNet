using System.Net.Sockets;
using CNet;

namespace CNet_Tests;

class ClientDisconnectWithMessageTest : ClientNetworkTest
{
    public override void OnConnected(NetEndPoint remoteEP)
    {
        using (NetPacket packet = new NetPacket(Client!.System, TransportProtocol.TCP))
        {
            packet.Write("Hello, Server!");
            Client!.Send(packet, TransportProtocol.TCP);
        }
    }

    public override void OnDisconnected(NetEndPoint remoteEP, NetDisconnect disconnect)
    {
        if (disconnect.DisconnectCode == DisconnectionCode.ConnectionClosedWithMessage && disconnect.DisconnectData != null && disconnect.SocketError == null)
        {
            string message = disconnect.DisconnectData.ReadString();
            if (message == "Goodbye, Server!")
            {
                Stop(true);
            }
            else
            {
                Stop(false, "Disconnected from listener with unexpected message: " + message);
            }
        }
        else
        {
            if (disconnect.SocketError != null)
            {
                Stop(false, "Disconnected from listener with socket error: " + disconnect.SocketError.ToString());
            }
            else
            {
                Stop(false, "Disconnected from listener with unexpected reason: " + disconnect.DisconnectCode.ToString());
            }
        }
    }

    public override void OnPacketReceived(NetEndPoint remoteEP, NetPacket packet, TransportProtocol protocol)
    {
        string message = packet.ReadString();
        if (message == "Hello, Client!")
        {
            using (NetPacket disconnectPacket = new NetPacket(Client!.System, TransportProtocol.TCP))
            {
                disconnectPacket.Write("Goodbye, Server!");
                remoteEP.Disconnect(disconnectPacket);
            }
        }
        else
        {
            Stop(false, "Received unexpected message from listener: " + message);
        }
    }

    public override void OnNetworkError(NetEndPoint? remoteEP, SocketError error)
    {
        Stop(false, "Network error: " + error.ToString());
    }
}
