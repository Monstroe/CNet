using System.Net;
using System.Net.Sockets;

namespace CNet
{
    /// <summary>
    /// Represents a network endpoint.
    /// </summary>
    public class NetEndPoint
    {
        /// <summary>
        /// Gets the TCP endpoint.
        /// </summary>
        public IPEndPoint TCPEndPoint { get; }

        /// <summary>
        /// Gets the UDP endpoint.
        /// </summary>
        public IPEndPoint? UDPEndPoint { get; internal set; }

        /// <summary>
        /// Gets the unique identifier of the endpoint.
        /// </summary>
        public uint ID { get; internal set; }

        internal Socket TCPSocket { get; }
        internal float TCPConnectionTimeoutTime { get; set; } // milliseconds
        internal float TCPHeartbeatInterval { get; set; } // milliseconds
        internal float UDPConnectionTimeoutTime { get; set; } // milliseconds
        internal float UDPHeartbeatInterval { get; set; } // milliseconds

        private readonly NetSystem netSystem;

        internal NetEndPoint(IPEndPoint tcpEndPoint, IPEndPoint? udpEndPoint, Socket tcpSocket, uint id, NetSystem netSystem)
        {
            TCPEndPoint = tcpEndPoint;
            UDPEndPoint = udpEndPoint;
            this.TCPSocket = tcpSocket;
            this.netSystem = netSystem;
            this.ID = id;
        }

        /// <summary>
        /// Sends a network packet using the specified protocol.
        /// </summary>
        /// <param name="packet">The network packet to send.</param>
        /// <param name="protocol">The protocol to use for sending the packet.</param>
        public void Send(NetPacket packet, TransportProtocol protocol)
        {
            netSystem.Send(this, packet, protocol);
        }

        /// <summary>
        /// Disconnects from the network with a specified disconnect packet.
        /// </summary>
        /// <param name="disconnectPacket">The disconnect packet to send.</param>
        public void Disconnect(NetPacket? disconnectPacket = null)
        {
            netSystem.Disconnect(this, disconnectPacket);
        }

        /// <summary>
        /// Disconnects from the network forcefully.
        /// </summary>
        public void DisconnectForcefully()
        {
            netSystem.DisconnectForcefully(this);
        }
    }
}