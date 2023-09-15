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
        public string IPAddress { get; set; }
        public int Port { get; set; }

        public Client(string ipAddress, int port)
        {
            IPAddress = ipAddress;
            Port = port;
        }

        public void Start()
        {
            Console.WriteLine("Client Started...");

            NetSystem system = new NetSystem(IPAddress, Port);
            system.RegisterInterface(this);
            system.Start();
            system.Connect();

            Console.WriteLine("Client initialized...");

            while (true)
            {
                system.Update();
            }
        }

        public void OnConnected(NetEndPoint remoteEndPoint)
        {
            Console.WriteLine("Connected");
        }

        public void OnDisconnected()
        {
            Console.WriteLine("Disconnected");
        }

        public void OnPacketReceive(NetEndPoint remoteEndPoint, NetPacket packet, PacketProtocol protocol)
        {
            Console.WriteLine("Packet Receive");
        }

        public void OnNetworkError(SocketException socketException)
        {
            Console.WriteLine("Network Error: " + socketException.Message);
        }
    }
}
