using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
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
        private NetSystem netSystem;

        internal NetEndPoint(IPEndPoint ipEndPoint, Socket tcpSocket, NetSystem netSystem)
        {
            EndPoint = ipEndPoint;
            this.tcpSocket = tcpSocket;
            this.netSystem = netSystem;
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
    }
}
