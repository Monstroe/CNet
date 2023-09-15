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
        public int Port { get; set; }

        public Server(int port)
        {
            Port = port;
        }

        public void Start()
        {
            Console.WriteLine("Server Started...");

            NetSystem system = new NetSystem(Port);
            system.RegisterInterface(this);
            system.Start();
            system.Listen();

            Console.WriteLine("Server initialized and is listening for connections...");

            while (true)
            {
                system.Update();
            }
        }

        public void OnConnectionRequest(NetRequest request)
        {
            Console.WriteLine("Connection Request");
            request.Deny();
        }

        public void OnClientConnected(NetEndPoint remoteEndPoint)
        {
            Console.WriteLine("Client Connected");
        }

        public void OnClientDisconnected()
        {
            Console.WriteLine("Connection Disconnected");
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
