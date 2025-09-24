using System.Net.Sockets;
using CNet;

namespace CNet_Tests;

class ClientServerClosedTest : ClientNetworkTest
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
        if (disconnect.DisconnectCode == DisconnectionCode.ConnectionClosed && disconnect.DisconnectData == null && disconnect.SocketError == null)
        {
            Stop(true);
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
        if (protocol != TransportProtocol.UDP)
        {
            Stop(false, "Received packet with unexpected protocol: " + protocol.ToString());
            return;
        }

        string message = packet.ReadString();
        if (message != "Hello, Client!")
        {
            Stop(false, "Received unexpected message from client: " + message);
        }
    }

    public override void OnNetworkError(NetEndPoint? remoteEP, SocketError error)
    {
        Stop(false, "Network error: " + error.ToString());
    }
}
