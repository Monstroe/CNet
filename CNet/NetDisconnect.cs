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
        public DisconnectCode DisconnectCode { get; }

        /// <summary>
        /// Gets the data associated with the disconnection.
        /// </summary>
        public NetPacket DisconnectData { get; }

        /// <summary>
        /// Gets the socket error associated with the disconnection.
        /// </summary>
        public SocketError SocketError { get; }

        internal NetDisconnect(DisconnectCode reason)
        {
            DisconnectCode = reason;
        }

        internal NetDisconnect(DisconnectCode reason, NetPacket data) : this(reason)
        {
            DisconnectData = data;
        }

        internal NetDisconnect(DisconnectCode reason, SocketError error) : this(reason)
        {
            SocketError = error;
        }

        internal NetDisconnect(DisconnectCode reason, NetPacket data, SocketError error) : this(reason, data)
        {
            SocketError = error;
        }
    }
}
