using MonstroeNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace TestServer
{
    internal class Server : IEventNetListener
    {
        private static Server instance;
        public static Server Instance
        {
            get
            {
                if (instance == null)
                    instance = new Server();
                return instance;
            }
        }

        public int Port
        {
            get { return listener.Port; }
            set { listener.Port = value; }
        }

        private NetListener listener;

        private Server()
        {
            listener = new NetListener();
            listener.RegisterInterface(this);
        }

        public void Start(int port)
        {
            Console.WriteLine("Server Starting...");
            listener.Port = port;

            listener.Listen();
            Console.WriteLine("Server initialized");
            Console.WriteLine("Waiting for connections...");
            int counter = 0;
            while (true)
            {
                listener.Update();
                Thread.Sleep(100);
                counter = UDPPacketLoop(counter);
            }
        }

        private int UDPPacketLoop(int counter)
        {
            // UDP Packet Test
            if (listener.RemoteEndPoints.Count > 0)
            {
                NetPacket packet = new NetPacket();
                packet.Write("UDP: ");
                packet.Write(counter);
                counter++;
                foreach (NetEndPoint remoteEP in listener.RemoteEndPoints)
                    remoteEP.Send(packet, PacketProtocol.UDP);
            }

            return counter;
        }

        public void OnConnectionRequest(NetRequest request)
        {
            Console.WriteLine("Connection Request from " + request.ClientEndPoint.ToString());
            request.Accept();
        }

        public void OnClientConnected(NetEndPoint remoteEndPoint)
        {
            Console.WriteLine("Client " + remoteEndPoint.TCPEndPoint.ToString() + " Connected");
            NetPacket packet = new NetPacket();
            packet.Write("Hello Client!");
            remoteEndPoint.Send(packet, PacketProtocol.TCP);
            packet.Clear();
            packet.Write("I am a TCP Packet");
            remoteEndPoint.Send(packet, PacketProtocol.TCP);
        }

        public void OnClientDisconnected(NetEndPoint remoteEndPoint, NetDisconnect disconnect)
        {
            Console.WriteLine("Disconnected from " + remoteEndPoint.TCPEndPoint.ToString() + ": " + disconnect.DisconnectCode.ToString() + (disconnect.DisconnectData != null ? ". Message: " + disconnect.DisconnectData.ReadString() : ""));
        }

        int udpRecvCounter = 1;

        public void OnPacketReceived(NetEndPoint remoteEndPoint, NetPacket packet, PacketProtocol protocol)
        {
            string message = packet.ReadString();
            if (protocol == PacketProtocol.UDP)
            {
                message += packet.ReadInt(false);
                int counter = packet.ReadInt(false);
                if (counter != udpRecvCounter)
                    Console.WriteLine("UDP Packet Loss: " + (counter - udpRecvCounter) + " packets");
                udpRecvCounter = counter + 1;
            }
            else
            {
                Console.WriteLine("Packet Received from " + remoteEndPoint.TCPEndPoint.ToString() + ": " + message);
                NetPacket responsePacket = new NetPacket();
                responsePacket.Write("TCP Packet Response");
                //remoteEndPoint.Send(responsePacket, PacketProtocol.TCP);
            }

            /*if (protocol == PacketProtocol.UDP) 
                message += packet.ReadInt();

            Console.WriteLine("Packet Received from " + remoteEndPoint.TCPEndPoint.ToString() + ": " + message);

            NetPacket responsePacket = new NetPacket();
            responsePacket.Write("TCP Packet Response");*/
            //remoteEndPoint.Send(responsePacket, PacketProtocol.TCP);
        }

        public void OnNetworkError(SocketException socketException)
        {
            Console.WriteLine("Network Error: " + socketException.SocketErrorCode.ToString());
        }

        static void Main(string[] args)
        {
            Server.Instance.Start(7777);
        }
    }
}
