using System.Net.Sockets;
using System.Reflection;
using CNet;
using FluentAssertions;

namespace CNet_Tests;

class ListenerReceiveTest : ListenerNetworkTest
{
    public override void OnConnectionRequest(NetRequest request)
    {
        request.AcceptIfKey(connectionKey);
    }

    public override void OnClientConnected(NetEndPoint remoteEP)
    {
        Listener!.Serializer.RegisterAssembly(Assembly.GetEntryAssembly()!);
        Task.Run(async () =>
        {
            for (int i = 0; i < 20; i++)
            {
                using (NetPacket packet = new NetPacket(Listener!.System, TransportProtocol.UDP))
                {
                    NetStruct netStruct = new NetStruct();
                    packet.Write(53); // Write length of serialized class for verification
                    packet.SerializeStruct(netStruct);
                    remoteEP.Send(packet, TransportProtocol.UDP);
                }

                await Task.Delay(50);
            }

            remoteEP.DisconnectForcefully();
        });
    }

    public override void OnClientDisconnected(NetEndPoint remoteEP, NetDisconnect disconnect)
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
        if (protocol != TransportProtocol.TCP)
        {
            Stop(false, "Received packet with unexpected protocol: " + protocol.ToString());
            return;
        }

        NetClass originalClass = new NetClass();
        int length = packet.ReadInt();
        NetClass receivedClass = packet.DeserializeClass<NetClass>();

        if (length != packet.Length - 4)
        {
            Stop(false, "Received packet length does not match expected length. Expected: " + (length + 4) + ", Actual: " + packet.Length);
            return;
        }

        try
        {
            receivedClass.Should().BeEquivalentTo(originalClass);
        }
        catch (Exception ex)
        {
            Stop(false, "Received class does not match original: " + ex.Message);
            return;
        }
    }

    public override void OnNetworkError(NetEndPoint? remoteEP, SocketError error)
    {
        Stop(false, "Network error: " + error.ToString());
    }
}