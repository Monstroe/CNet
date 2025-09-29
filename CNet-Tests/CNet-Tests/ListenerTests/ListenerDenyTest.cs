using System.Net.Sockets;
using CNet;

namespace CNet_Tests;

class ListenerDenyTest : ListenerNetworkTest
{
    public override void OnConnectionRequest(NetRequest request)
    {
        request.Deny();
    }

    public override void OnClientConnected(NetEndPoint remoteEP)
    {
        Stop(false, "Client connected, this should not happen.");
    }

    public override void OnClientDisconnected(NetEndPoint remoteEP, NetDisconnect disconnect)
    {
        if (disconnect.DisconnectCode == DisconnectionCode.ConnectionDenied && disconnect.DisconnectData == null && disconnect.SocketError == null)
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
        Stop(false, "Received packet from client when it should have been denied.");
    }

    public override void OnNetworkError(NetEndPoint? remoteEP, SocketError error)
    {
        Stop(false, "Network error: " + error.ToString());
    }
}