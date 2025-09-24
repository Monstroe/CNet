using System;

namespace CNet
{
    /// <summary>
    /// Represents the protocol settings for network communication.
    /// </summary>
    public class ProtocolSettings
    {
        private int maxPacketSize;

        /// <summary>
        /// Gets or sets the heartbeat interval in milliseconds.
        /// </summary>
        public int HEARTBEAT_INTERVAL { get; set; }
        /// <summary>
        /// Gets or sets the connection timeout in milliseconds.
        /// </summary>
        public int CONNECTION_TIMEOUT { get; set; }

        /// <summary>
        /// Gets or sets the internal socket buffer size (Socket.ReceiveBufferSize) for network communication.
        /// </summary>
        /// <remarks>
        /// When set to 0, the default buffer size of the operating system is used.
        /// </remarks>
        public int SOCKET_RECEIVE_BUFFER_SIZE { get; set; }

        /// <summary>
        /// Gets or sets the internal socket buffer size (Socket.SendBufferSize) for network communication.
        /// </summary>
        /// <remarks>
        /// When set to 0, the default buffer size of the operating system is used.
        /// </remarks>
        public int SOCKET_SEND_BUFFER_SIZE { get; set; }

        /// <summary>
        /// Gets or sets the maximum packet size for network communication.
        /// </summary>
        /// <exception cref="System.Exception">Thrown when the specified value is greater than the maximum allowed packet size or less than 1.</exception>
        public int MAX_PACKET_SIZE
        {
            get { return maxPacketSize; }
            set
            {
                if (value > int.MaxValue)
                {
                    throw new Exception("MaxPacketSize cannot be greater than " + int.MaxValue + ".");
                }
                else if (value < 1)
                {
                    throw new Exception("MaxPacketSize cannot be less than 1.");
                }

                maxPacketSize = value;
            }
        }
    }

    /// <summary>
    /// Represents the connection settings for network communication
    /// </summary>
    public class ConnectionSettings
    {
        /// <summary>
        /// Gets or sets the maximum number of pending connections that can be queued.
        /// </summary>
        /// <remarks>
        /// This property is only used when the system is a listener.
        /// </remarks>
        public int MAX_PENDING_CONNECTIONS { get; set; }

        /// <summary>
        /// Gets or sets the receive timeout when one system is waiting for connection data from another system.
        /// </summary>
        /// <remarks>
        /// This property is in milliseconds.
        /// </remarks>
        public int DATA_RECEIVE_TIMEOUT { get; set; }

        /// <summary>
        /// Gets or sets the rate at which invalid connection tokens are removed from the system.
        /// </summary>
        /// <remarks>
        /// This property is in iterations per second.
        /// </remarks>
        public int INVALID_TOKEN_REMOVAL_RATE { get; set; }

        /// <summary>
        /// Gets or sets the UDP packet send rate when the client is sending UDP connection data to the listener.
        /// </summary>
        /// <remarks>
        /// This property is in packets per second.
        /// </remarks>
        public int UDP_SEND_RATE { get; set; }
    }

    /// <summary>
    /// Represents the packet protocol for network communication.
    /// </summary>
    public enum TransportProtocol
    {
        TCP,
        UDP
    }

    /// <summary>
    /// Represents the system mode for network communication.
    /// </summary>
    public enum SystemMode
    {
        None,
        Client,
        Listener
    }

    /// <summary>
    /// Represents the connection codes for network communication.
    /// </summary>
    internal enum ConnectionCode
    {
        Heartbeat = 0,
        ConnectionKeyRequest = -1,
        ConnectionKey = -2,
        ConnectionTokenRequest = -3,
        ConnectionToken = -4,
        ConnectionAccepted = -5,
    }

    /// <summary>
    /// Represents the disconnection codes for network communication.
    /// </summary>
    public enum DisconnectionCode
    {
        ConnectionClosed = -6,
        ConnectionClosedWithMessage = -7,
        ConnectionClosedForcefully = -8,
        ConnectionDenied = -9,
        ConnectionLost = -10,
        ConnectionTimedOut = -11,
        PacketOverMaxSize = -12,
        InvalidPacket = -13,
        SocketError = -14,
    }
}
