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
        public IPEndPoint EndPoint { get; }

        public string Address
        {
            get { return EndPoint.Address.ToString(); }
            set { EndPoint.Address = IPAddress.Parse(value); }
        }

        public int Port
        {
            get { return EndPoint.Port; }
            set { EndPoint.Port = value; }
        }

        internal Socket tcpSocket;
        internal NetPacket receivedPacket;
        internal CancellationTokenSource cancellationTokenSource;
        private NetSystem netSystem;

        internal NetEndPoint(NetSystem netSystem)
        {
            this.netSystem = netSystem;
            cancellationTokenSource = new CancellationTokenSource();
        }

        internal NetEndPoint(IPEndPoint ipEndPoint, Socket tcpSocket, NetSystem netSystem) : this(netSystem)
        {
            EndPoint = ipEndPoint;
            this.tcpSocket = tcpSocket;
            receivedPacket = new NetPacket();
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
