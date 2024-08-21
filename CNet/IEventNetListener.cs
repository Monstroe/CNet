using System.Net.Sockets;

namespace CNet
{
    public interface IEventNetListener
    {
        public void OnConnectionRequest(NetRequest request);

        public void OnClientConnected(NetEndPoint remoteEndPoint);

        public void OnClientDisconnected(NetEndPoint remoteEndPoint, NetDisconnect disconnect);

        public void OnPacketReceived(NetEndPoint remoteEndPoint, NetPacket packet, PacketProtocol protocol);

        public void OnNetworkError(NetEndPoint remoteEndPoint, SocketException socketException);
    }
}
