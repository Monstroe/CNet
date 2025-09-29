using System.Net.Sockets;
using CNet;

namespace CNet_Tests;

class ListenerDisconnectWithMessageTest : ListenerNetworkTest
{
    public override void OnConnectionRequest(NetRequest request)
    {
        request.AcceptIfKey(connectionKey);
    }

    public override void OnClientConnected(NetEndPoint remoteEP)
    {
        using (NetPacket packet = new NetPacket(Listener!.System, TransportProtocol.TCP))
        {
            packet.Write("Hello, Client!");
            Listener!.Send(remoteEP, packet, TransportProtocol.TCP);
        }
    }

    public override void OnClientDisconnected(NetEndPoint remoteEP, NetDisconnect disconnect)
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
        if (message != "Hello, Server!")
        {
            Stop(false, "Received unexpected message from client: " + message);
        }
    }

    public override void OnNetworkError(NetEndPoint? remoteEP, SocketError error)
    {
        Stop(false, "Network error: " + error.ToString());
    }
}