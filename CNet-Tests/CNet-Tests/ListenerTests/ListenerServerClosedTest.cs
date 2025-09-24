using System.Net.Sockets;
using CNet;

namespace CNet_Tests;

class ListenerServerClosedTest : ListenerNetworkTest
{
    public override void OnConnectionRequest(NetRequest request)
    {
        request.AcceptIfKey(connectionKey);
    }

    public override void OnClientConnected(NetEndPoint remoteEP)
    {
        using (NetPacket packet = new NetPacket(Listener!.System, TransportProtocol.UDP))
        {
            packet.Write("Hello, Client!");
            Listener!.Send(remoteEP, packet, TransportProtocol.UDP);
        }
    }

    public override void OnClientDisconnected(NetEndPoint remoteEP, NetDisconnect disconnect)
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
        if (protocol != TransportProtocol.TCP)
        {
            Stop(false, "Received packet with unexpected protocol: " + protocol.ToString());
            return;
        }

        string message = packet.ReadString();
        if (message == "Hello, Server!")
        {
            Listener!.Close(true);
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