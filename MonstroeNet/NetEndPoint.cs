using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MonstroeNet
{
    public class NetEndPoint
    {
        public IPEndPoint TCPEndPoint { get; }
        public IPEndPoint UDPEndPoint { get; internal set; }

        public string Address
        {
            get { return TCPEndPoint.Address.ToString(); }
            set { TCPEndPoint.Address = IPAddress.Parse(value); }
        }

        public int TCPPort
        {
            get { return TCPEndPoint.Port; }
            set { TCPEndPoint.Port = value; }
        }

        public int UDPPort 
        {
            get { return UDPEndPoint.Port; }
            set { UDPEndPoint.Port = value; }
        }

        internal Socket tcpSocket;
        internal CancellationTokenSource cancellationTokenSource;
        private NetSystem netSystem;

        internal NetEndPoint(NetSystem netSystem)
        {
            this.netSystem = netSystem;
            cancellationTokenSource = new CancellationTokenSource();
        }

        internal NetEndPoint(IPEndPoint ipEndPoint, Socket tcpSocket, NetSystem netSystem) : this(netSystem)
        {
            TCPEndPoint = ipEndPoint;
            this.tcpSocket = tcpSocket;
        }

        public void Send(NetPacket packet, PacketProtocol protocol)
        {
            netSystem.Send(this, packet, protocol);
        }

        public void Disconnect()
        {
            netSystem.Disconnect(this);
        }

        public void Disconnect(NetPacket disconnectPacket) 
        {
            netSystem.Disconnect(this, disconnectPacket);
        }

        public void DisconnectForcefully()
        {
            netSystem.DisconnectForcefully(this);
        }
    }
}
