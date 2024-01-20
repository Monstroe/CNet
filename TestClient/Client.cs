using MonstroeNet;
using System;
using System.Net.Sockets;

namespace TestClient
{
    internal class Client : IEventNetClient
    {
        private static Client instance;
        public static Client Instance
        {
            get
            {
                if (instance == null)
                    instance = new Client();
                return instance;
            }
        }

        public string IPAddress
        {
            get { return client.Address; }
            set { client.Address = value; }
        }

        public int Port
        {
            get { return client.Port; }
            set { client.Port = value; }
        }

        private NetClient client;

        private Client()
        {
            client = new NetClient();
            client.RegisterInterface(this);
        }

        public void Start(string address, int port)
        {
            Console.WriteLine("Client Starting...");
            client.Address = address;
            client.Port = port;

            client.Connect();
            Console.WriteLine("Client Initialized");
            int counter = 0;
            while (true)
            {
                client.Update();
                Thread.Sleep(15);
                counter = UDPPacketLoop(counter);
            }
        }

        private int UDPPacketLoop(int counter)
        {
            // UDP Packet Test
            if (client.IsConnected && client.RemoteEndPoint != null)
            {
                NetPacket packet = new NetPacket();
                packet.Write("UDP: ");
                packet.Write(counter);
                counter++;
                client.RemoteEndPoint.Send(packet, PacketProtocol.UDP);
            }

            return counter;
        }

        public void OnConnected(NetEndPoint remoteEndPoint)
        {
            Console.WriteLine("Connected to " + remoteEndPoint.TCPEndPoint);
            NetPacket packet = new NetPacket();
            packet.Write("Hello Server!");
            remoteEndPoint.Send(packet, PacketProtocol.TCP);
            packet.Clear();
            packet.Write("I am a TCP Packet");
            remoteEndPoint.Send(packet, PacketProtocol.TCP);
        }

        public void OnDisconnected(NetEndPoint remoteEndPoint, NetDisconnect disconnect)
        {
            Console.WriteLine("Disconnected from " + remoteEndPoint.TCPEndPoint + ": " + disconnect.DisconnectCode.ToString() + (disconnect.DisconnectData != null ? ". Message: " + disconnect.DisconnectData.ReadString() : ""));
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
                remoteEndPoint.Send(responsePacket, PacketProtocol.TCP);
            }
        }

        public void OnNetworkError(SocketException socketException)
        {
            Console.WriteLine("Network Error: " + socketException.SocketErrorCode.ToString());
        }

        // Main Method
        static void Main(string[] args)
        {
            Client.Instance.Start("127.0.0.1", 7777);
            //Client.Instance.Start("165.227.90.160", 7777);
        }
    }
}
