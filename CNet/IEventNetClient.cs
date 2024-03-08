using System;
using System.Net.Sockets;

namespace CNet
{
    public interface IEventNetClient
    {
        public void OnConnected(NetEndPoint remoteEndPoint);

        public void OnDisconnected(NetEndPoint remoteEndPoint, NetDisconnect disconnect);

        public void OnPacketReceived(NetEndPoint remoteEndPoint, NetPacket packet, PacketProtocol protocol);

        public void OnNetworkError(SocketException socketException);
    }
}
