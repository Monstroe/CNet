using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace MonstroeNet
{
    public interface IEventNetClient
    {
        void OnConnected(NetEndPoint remoteEndPoint);

        void OnDisconnected(NetEndPoint remoteEndPoint, NetDisconnect disconnect);

        void OnPacketReceived(NetEndPoint remoteEndPoint, NetPacket packet, PacketProtocol protocol);

        void OnNetworkError(SocketException socketException);
    }
}
