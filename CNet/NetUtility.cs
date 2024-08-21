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
        /// Gets or sets the buffer size for network communication.
        /// </summary>
        public int BufferSize { get; set; }

        /// <summary>
        /// Gets or sets the maximum packet size for network communication.
        /// </summary>
        /// <exception cref="System.Exception">Thrown when the specified value is greater than the maximum allowed packet size or less than 1.</exception>
        public int MaxPacketSize
        {
            get { return maxPacketSize; }
            set
            {
                if (value > NetConstants.MAX_PACKET_SIZE)
                {
                    throw new Exception("MaxPacketSize cannot be greater than " + NetConstants.MAX_PACKET_SIZE + ".");
                }
                else if (value < 1)
                {
                    throw new Exception("MaxPacketSize cannot be less than 1.");
                }

                maxPacketSize = value;
            }
        }
    }

    internal static class NetConstants
    {
        internal const uint MAX_PACKET_SIZE = 2147483647;
        internal const int DATA_RECEIVE_TIMEOUT = 5000; // milliseconds
        internal const float TCP_HEARTBEAT_INTERVAL = 5.0f; // seconds
        internal const float UDP_HEARTBEAT_INTERVAL = 1.0f; // seconds
        internal const int TCP_CONNECTION_TIMEOUT = 15; // seconds
        internal const int UDP_CONNECTION_TIMEOUT = 30; // seconds

        internal static int HeartbeatInterval
        {
            get
            {
                return TCP_HEARTBEAT_INTERVAL > UDP_HEARTBEAT_INTERVAL ? (int)(UDP_HEARTBEAT_INTERVAL * 1000) : (int)(TCP_HEARTBEAT_INTERVAL * 1000);
            }
        }
    }

    /// <summary>
    /// Represents the packet protocol for network communication.
    /// </summary>
    public enum PacketProtocol
    {
        TCP,
        UDP
    }

    /// <summary>
    /// Represents the system mode for network communication.
    /// </summary>
    public enum SystemMode
    {
        Client,
        Listener
    }

    /// <summary>
    /// Represents the disconnect codes for network communication.
    /// </summary>
    public enum DisconnectCode
    {
        ConnectionClosed = -1,
        ConnectionClosedWithMessage = -2,
        ConnectionClosedForcefully = -3,
        ConnectionRejected = -4,
        ConnectionLost = -5,
        PacketOverBufferSize = -6,
        PacketOverMaxSize = -7,
        InvalidPacket = -8,
        SocketError = -9,
    }
}
