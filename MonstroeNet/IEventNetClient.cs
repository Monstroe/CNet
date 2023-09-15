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

        void OnDisconnected(NetDisconnect disconnect);

        void OnPacketReceive(NetEndPoint remoteEndPoint, NetPacket packet, PacketProtocol protocol);

        void OnNetworkError(SocketException socketException);
    }
}
