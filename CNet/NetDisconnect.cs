using System.Net.Sockets;

namespace CNet
{
    /// <summary>
    /// Represents a network disconnection event.
    /// </summary>
    public class NetDisconnect
    {
        /// /// {
        /// <summary>
        /// Gets the reason for the disconnection.
        /// </summary>
        public DisconnectionCode DisconnectCode { get; }

        /// <summary>
        /// Gets the data associated with the disconnection.
        /// </summary>
        public NetPacket? DisconnectData { get; }

        /// <summary>
        /// Gets the socket error associated with the disconnection.
        /// </summary>
        public SocketError? SocketError { get; }

        internal NetDisconnect(DisconnectionCode reason, NetPacket? data, SocketError? error)
        {
            DisconnectCode = reason;
            DisconnectData = data;
            SocketError = error;
        }
    }
}
