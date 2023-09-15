using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace MonstroeNet
{
    public interface IEventNetListener
    {
        void OnConnectionRequest(NetRequest request);

        void OnClientConnected(NetEndPoint remoteEndPoint);

        void OnClientDisconnected(NetDisconnect disconnect);

        void OnPacketReceive(NetEndPoint remoteEndPoint, NetPacket packet, PacketProtocol protocol);

        void OnNetworkError(SocketException socketException);

    }
}
