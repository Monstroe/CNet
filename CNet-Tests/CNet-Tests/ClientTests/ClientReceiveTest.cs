using System.Net.Sockets;
using CNet;
using FluentAssertions;

namespace CNet_Tests;

class ClientReceiveTest : ClientNetworkTest
{
    public override void OnConnected(NetEndPoint remoteEP)
    {
        Task.Run(async () =>
        {
            for (int i = 0; i < 30; i++)
            {
                using (NetPacket packet = new NetPacket(Client!.System, TransportProtocol.TCP))
                {
                    NetClass netClass = new NetClass();
                    packet.Write(418); // Write length of serialized class for verification
                    packet.SerializeClass(netClass);
                    remoteEP.Send(packet, TransportProtocol.TCP);
                }

                await Task.Delay(50);
            }
        });
    }

    public override void OnDisconnected(NetEndPoint remoteEP, NetDisconnect disconnect)
    {
        if (disconnect.DisconnectCode == DisconnectionCode.ConnectionClosedForcefully && disconnect.DisconnectData == null && disconnect.SocketError == null)
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

        NetStruct originalStruct = new NetStruct();
        int length = packet.ReadInt();
        NetStruct receivedStruct = packet.DeserializeStruct<NetStruct>();

        if (length != packet.Length - 4)
        {
            Stop(false, "Received packet length does not match expected length. Expected: " + (length + 4) + ", Actual: " + packet.Length);
            return;
        }

        try
        {
            receivedStruct.Should().BeEquivalentTo(originalStruct);
        }
        catch (Exception ex)
        {
            Stop(false, "Received struct does not match original: " + ex.Message);
            return;
        }
    }

    public override void OnNetworkError(NetEndPoint? remoteEP, SocketError error)
    {
        Stop(false, "Network error: " + error.ToString());
    }
}
