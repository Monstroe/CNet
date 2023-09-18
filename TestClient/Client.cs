using MonstroeNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

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

        public string IPAddress { get; set; }
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
            Console.WriteLine("Client initialized...");
            while (true)
            {
                client.Update();
            }
        }

        public void OnConnected(NetEndPoint remoteEndPoint)
        {
            Console.WriteLine("Connected to " + remoteEndPoint.EndPoint);
        }

        public void OnDisconnected(NetEndPoint remoteEndPoint, NetDisconnect disconnect)
        {
            Console.WriteLine("Disconnected from " + remoteEndPoint.EndPoint + ": " + disconnect.DisconnectCode.ToString());
        }

        public void OnPacketReceived(NetEndPoint remoteEndPoint, NetPacket packet, PacketProtocol protocol)
        {
            Console.WriteLine("Packet Received: " + packet.ReadString());
        }

        public void OnNetworkError(SocketException socketException)
        {
            Console.WriteLine("Error: " + socketException.SocketErrorCode.ToString());
        }

        // Main Method
        static void Main(string[] args)
        {
            Client.Instance.Start("127.0.0.1", 7777);
        }
    }
}
