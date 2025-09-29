using System.Net.Sockets;

namespace CNet
{
    public interface IEventNetClient
    {
        /// <summary>
        /// Called when the client is connected to the server.
        /// </summary>
        /// <param name="remoteEP">The server's endpoint.</param>
        public void OnConnected(NetEndPoint remoteEP);

        /// <summary>
        /// Called when the client is disconnected from the server.
        /// </summary>
        /// <param name="remoteEP">The server's endpoint.</param>
        /// <param name="disconnect">The reason for the disconnection.</param>
        public void OnDisconnected(NetEndPoint remoteEP, NetDisconnect disconnect);

        /// <summary>
        /// Called when a packet is received from the server.
        /// </summary>
        /// <param name="remoteEP">The server's endpoint.</param>
        /// <param name="packet">The packet received.</param>
        /// <param name="protocol">The protocol used to send the packet.</param>
        public void OnPacketReceived(NetEndPoint remoteEP, NetPacket packet, TransportProtocol protocol);

        /// <summary>
        /// Called when a network error occurs.
        /// </summary>
        /// <param name="remoteEP">The server's endpoint. Null if the error is not related to a specific endpoint.</param>
        /// <param name="socketException">The socket exception that occurred.</param>
        public void OnNetworkError(NetEndPoint? remoteEP, SocketError error);
    }
}
