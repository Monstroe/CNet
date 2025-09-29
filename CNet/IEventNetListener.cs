using System.Net.Sockets;

namespace CNet
{
    public interface IEventNetListener
    {
        /// <summary>
        /// Called when a connection request is received.
        /// </summary>
        /// <param name="request">The connection request.</param>
        public void OnConnectionRequest(NetRequest request);

        /// <summary>
        /// Called when a client is connected to the server.
        /// </summary>
        /// <param name="remoteEP">The client's endpoint.</param>
        public void OnClientConnected(NetEndPoint remoteEP);

        /// <summary>
        /// Called when a client is disconnected from the server.
        /// </summary>
        /// <param name="remoteEP">The client's endpoint.</param>
        /// <param name="disconnect">The reason for the disconnection.</param>
        public void OnClientDisconnected(NetEndPoint remoteEP, NetDisconnect disconnect);

        /// <summary>
        /// Called when a packet is received from a client.
        /// </summary>
        /// <param name="remoteEP">The client's endpoint.</param>
        /// <param name="packet">The packet received.</param>
        /// <param name="protocol">The protocol used to send the packet.</param>
        public void OnPacketReceived(NetEndPoint remoteEP, NetPacket packet, TransportProtocol protocol);

        /// <summary>
        /// Called when a network error occurs.
        /// </summary>
        /// <param name="remoteEP">The client's endpoint. Null if the error is not related to a specific endpoint.</param>
        /// <param name="socketException">The socket exception that occurred.</param>
        public void OnNetworkError(NetEndPoint? remoteEP, SocketError error);
    }
}
