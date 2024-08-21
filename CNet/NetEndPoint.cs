using System.Net;
using System.Net.Sockets;
using System.Threading;

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
        public IPEndPoint UDPEndPoint { get; internal set; }

        /// <summary>
        /// Gets the address of both endpoints.
        /// </summary>
        public string Address
        {
            get { return TCPEndPoint.Address.ToString(); }
        }

        /// <summary>
        /// Gets the TCP port.
        /// </summary>
        public int TCPPort
        {
            get { return TCPEndPoint.Port; }
        }

        /// <summary>
        /// Gets the UDP port.
        /// </summary>
        public int UDPPort
        {
            get { return UDPEndPoint.Port; }
        }

        internal Socket tcpSocket;
        internal CancellationTokenSource tcpCancelTokenSource;
        internal float tcpConnectionTimeoutTime; // seconds
        internal float tcpHearbeatInterval; // seconds
        internal float udpConnectionTimeoutTime; // seconds
        internal float udpHearbeatInterval; // seconds

        private NetSystem netSystem;

        internal NetEndPoint(NetSystem netSystem)
        {
            this.netSystem = netSystem;
            tcpCancelTokenSource = new CancellationTokenSource();
        }

        internal NetEndPoint(IPEndPoint ipEndPoint, Socket tcpSocket, NetSystem netSystem) : this(netSystem)
        {
            TCPEndPoint = ipEndPoint;
            this.tcpSocket = tcpSocket;
        }

        internal NetEndPoint(IPEndPoint tcpEncPoint, IPEndPoint udpEndPoint, Socket tcpSocket, NetSystem netSystem) : this(tcpEncPoint, tcpSocket, netSystem)
        {
            UDPEndPoint = udpEndPoint;
        }

        /// <summary>
        /// Sends a network packet using the specified protocol.
        /// </summary>
        /// <param name="packet">The network packet to send.</param>
        /// <param name="protocol">The protocol to use for sending the packet.</param>
        public void Send(NetPacket packet, PacketProtocol protocol)
        {
            netSystem.Send(this, packet, protocol);
        }

        /// <summary>
        /// Disconnects from the network.
        /// </summary>
        public void Disconnect()
        {
            netSystem.Disconnect(this);
        }

        /// <summary>
        /// Disconnects from the network with a specified disconnect packet.
        /// </summary>
        /// <param name="disconnectPacket">The disconnect packet to send.</param>
        public void Disconnect(NetPacket disconnectPacket)
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